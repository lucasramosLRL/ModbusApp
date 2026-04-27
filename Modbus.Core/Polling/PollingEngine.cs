using System.Collections.Concurrent;
using Modbus.Core.Domain.Entities;
using Modbus.Core.Domain.Enums;
using Modbus.Core.Services;

namespace Modbus.Core.Polling;

public class PollingEngine : IPollingEngine
{
    private readonly IModbusServiceFactory _factory;
    private readonly TimeSpan _pollInterval;

    private readonly ConcurrentDictionary<int, DeviceContext> _devices = new();
    private readonly SemaphoreSlim _rtuGate = new(1, 1);
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public event EventHandler<RegisterValuesUpdatedEventArgs>? RegisterValuesUpdated;
    public event EventHandler<DeviceConnectionFailedEventArgs>? DeviceConnectionFailed;

    public PollingEngine(IModbusServiceFactory factory, TimeSpan pollInterval)
    {
        _factory      = factory;
        _pollInterval = pollInterval;
    }

    public void AddDevice(ModbusDevice device)
    {
        if (_devices.ContainsKey(device.Id))
            return; // Already tracked — keep the active connection.

        var ctx = new DeviceContext(device, _factory.Create(device));
        _devices[device.Id] = ctx;
    }

    public void RemoveDevice(int deviceId)
    {
        if (_devices.TryRemove(deviceId, out var ctx))
            ctx.Service.Dispose();
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _cts      = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loopTask = RunLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_cts is null) return;

        await _cts.CancelAsync();

        if (_loopTask is not null)
        {
            try { await _loopTask; }
            catch (OperationCanceledException) { }
        }

        foreach (var ctx in _devices.Values)
            ctx.Service.Dispose();

        _devices.Clear();
        _rtuGate.Dispose();
    }

    public ValueTask DisposeAsync() =>
        new(StopAsync());

    public Task SuspendRtuPollingAsync(CancellationToken cancellationToken = default) =>
        _rtuGate.WaitAsync(cancellationToken);

    public void ResumeRtuPolling() =>
        _rtuGate.Release();

    // ── Main loop ────────────────────────────────────────────────────────────

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_pollInterval);

        try
        {
            do
            {
                foreach (var ctx in _devices.Values)
                {
                    if (cancellationToken.IsCancellationRequested) return;
                    if (!ctx.Device.IsActive) continue;

                    try
                    {
                        await PollDeviceAsync(ctx, cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        return; // Shutdown — exit.
                    }
                    catch
                    {
                        // Unexpected error for this device — continue polling others.
                    }
                }
            }
            while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown — swallow and exit.
        }
    }

    /// <summary>Maximum time allowed for a single device poll (connect + read).</summary>
    private static readonly TimeSpan PollTimeout = TimeSpan.FromSeconds(4);

    private async Task PollDeviceAsync(DeviceContext ctx, CancellationToken cancellationToken)
    {
        using var pollCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        pollCts.CancelAfter(PollTimeout);

        if (ctx.Device.TransportType == TransportType.Rtu)
        {
            await _rtuGate.WaitAsync(pollCts.Token);
            try   { await DoPollAsync(ctx, cancellationToken, pollCts); }
            finally { _rtuGate.Release(); }
            return;
        }

        await DoPollAsync(ctx, cancellationToken, pollCts);
    }

    private async Task DoPollAsync(DeviceContext ctx, CancellationToken cancellationToken, CancellationTokenSource pollCts)
    {
        try
        {
            // Always disconnect and reconnect to get a fresh connection state.
            // TCP sockets don't reliably detect remote disconnection without I/O.
            try { await ctx.Service.DisconnectAsync(); } catch { }
            await ctx.Service.ConnectAsync(pollCts.Token);

            var timestamp = DateTime.UtcNow;

            if (ctx.Device.DeviceModel is null || ctx.Device.DeviceModel.Registers.Count == 0)
            {
                // No register map (or empty model) — heartbeat read to verify device is still alive.
                await ctx.Service.ReadInputRegistersAsync(ctx.Device.SlaveId, 0, 1, pollCts.Token);

                RegisterValuesUpdated?.Invoke(this, new RegisterValuesUpdatedEventArgs
                {
                    Device    = ctx.Device,
                    Values    = Array.Empty<RegisterValue>(),
                    Timestamp = timestamp
                });
            }
            else
            {
                var values = await ReadAllRegistersAsync(ctx.Service, ctx.Device, timestamp, pollCts.Token);

                RegisterValuesUpdated?.Invoke(this, new RegisterValuesUpdatedEventArgs
                {
                    Device    = ctx.Device,
                    Values    = values,
                    Timestamp = timestamp
                });
            }

            // RTU: release the COM port immediately after the poll so other
            // operations (e.g. device scan) can open the same port between cycles.
            if (ctx.Device.TransportType == TransportType.Rtu)
                try { await ctx.Service.DisconnectAsync(); } catch { }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // Shutdown requested — propagate.
        }
        catch (Exception ex)
        {
            // Disconnect so the next poll attempt starts clean.
            try { await ctx.Service.DisconnectAsync(); } catch { }

            DeviceConnectionFailed?.Invoke(this, new DeviceConnectionFailedEventArgs
            {
                Device    = ctx.Device,
                Exception = ex
            });
        }
    }

    // ── Register reading ─────────────────────────────────────────────────────

    private static async Task<IReadOnlyList<RegisterValue>> ReadAllRegistersAsync(
        IModbusService service,
        ModbusDevice device,
        DateTime timestamp,
        CancellationToken cancellationToken)
    {
        // Read SQPF once per poll cycle; applies only to Float32 input registers marked UseSqpf.
        // Falls back to 0x3210 (Padrão KRON) if the register is unavailable.
        ushort sqpfValue = 0x3210;
        if (device.DeviceModel?.SqpfRegisterAddress is { } sqpfAddr)
        {
            try
            {
                var sqpfWords = await service.ReadHoldingRegistersAsync(device.SlaveId, sqpfAddr, 1, cancellationToken);
                sqpfValue = sqpfWords[0];
            }
            catch (OperationCanceledException) { throw; }
            catch { /* device does not support SQPF register — keep default */ }
        }

        var results = new List<RegisterValue>();
        var registers = device.DeviceModel!.Registers;

        foreach (var registerType in new[] { RegisterType.Holding, RegisterType.Input })
        {
            foreach (var block in GroupRegisters(registers, registerType))
            {
                ushort[] words = registerType == RegisterType.Holding
                    ? await service.ReadHoldingRegistersAsync(device.SlaveId, block.Start, block.Count, cancellationToken)
                    : await service.ReadInputRegistersAsync(device.SlaveId, block.Start, block.Count, cancellationToken);

                foreach (var reg in block.Registers)
                {
                    int offset   = reg.Address - block.Start;
                    var regWords = words[offset..(offset + reg.RegisterCount)];

                    bool useSqpf = reg.WordOrder == WordOrder.UseSqpf &&
                                   reg.RegisterType == RegisterType.Input &&
                                   reg.DataType == DataType.Float32;

                    double value = useSqpf
                        ? RegisterDecoder.DecodeFloat32WithSqpf(regWords, sqpfValue, reg.ScaleFactor)
                        : RegisterDecoder.Decode(regWords, reg.DataType, reg.WordOrder, reg.ScaleFactor);

                    results.Add(new RegisterValue
                    {
                        DeviceId     = device.Id,
                        Address      = reg.Address,
                        RegisterType = reg.RegisterType,
                        Value        = value,
                        RawWords     = regWords,
                        Timestamp    = timestamp
                    });
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Groups registers of a given type into contiguous read blocks.
    /// Registers within <paramref name="maxGap"/> addresses of each other are merged into
    /// one block to reduce the number of Modbus requests.
    /// </summary>
    private static IEnumerable<ReadBlock> GroupRegisters(
        IEnumerable<RegisterDefinition> registers,
        RegisterType type,
        int maxGap = 5)
    {
        var sorted = registers
            .Where(r => r.RegisterType == type)
            .OrderBy(r => r.Address)
            .ToList();

        if (sorted.Count == 0) yield break;

        var group = new List<RegisterDefinition> { sorted[0] };

        for (int i = 1; i < sorted.Count; i++)
        {
            var prev = sorted[i - 1];
            var curr = sorted[i];
            int gap  = curr.Address - (prev.Address + prev.RegisterCount);

            if (gap <= maxGap)
                group.Add(curr);
            else
            {
                yield return ToBlock(group);
                group = [curr];
            }
        }

        yield return ToBlock(group);
    }

    private static ReadBlock ToBlock(List<RegisterDefinition> group)
    {
        ushort start = group[0].Address;
        var last     = group[^1];
        ushort count = (ushort)(last.Address + last.RegisterCount - start);
        return new ReadBlock(start, count, group);
    }

    // ── Inner types ──────────────────────────────────────────────────────────

    private sealed class DeviceContext(ModbusDevice device, IModbusService service)
    {
        public ModbusDevice  Device  { get; } = device;
        public IModbusService Service { get; } = service;
    }

    private readonly record struct ReadBlock(
        ushort Start,
        ushort Count,
        IReadOnlyList<RegisterDefinition> Registers);
}
