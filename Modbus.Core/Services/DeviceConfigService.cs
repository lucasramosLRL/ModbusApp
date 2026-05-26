using System.Diagnostics;
using System.IO;
using Modbus.Core.Domain.Entities;
using Modbus.Core.Protocol.Exceptions;

namespace Modbus.Core.Services;

/// <summary>
/// Addresses passed to ReadAsync/WriteAsync must be Modicon-style register numbers:
///   4xxxx  →  Holding Register (FC03), raw address = modiconAddr - 40001
///   3xxxx  →  Input Register   (FC04), raw address = modiconAddr - 30001
/// The returned dictionary is keyed by the same Modicon numbers.
/// </summary>
public sealed class DeviceConfigService : IDeviceConfigService
{
    private const int MaxBlockSize    = 32;  // FC03/FC04 spec limit per request
    private const int MaxWriteBlockSize = 22; // KS-3000 FC16 limit per request
    private const ushort CoilResetAddress = 6; // KS-3000 / Konect 120 commit coil for string writes ≥ 43461
    private const int TimeoutSeconds  = 30;
    private const int MaxAttempts    = 3;   // Total attempts per block (1 initial + 2 retries)
    private const int RetryDelayMs   = 150; // Pause between retries to let the device settle

    private readonly IModbusServiceFactory _factory;

    public DeviceConfigService(IModbusServiceFactory factory) => _factory = factory;

    public async Task<ConfigReadResult> ReadAsync(
        ModbusDevice device,
        IEnumerable<RegisterField> fields,
        CancellationToken cancellationToken = default)
    {
        var allFields = fields.ToArray();
        if (allFields.Length == 0)
            return new ConfigReadResult(new Dictionary<ushort, ushort>(), Array.Empty<string>());

        var result = new Dictionary<ushort, ushort>(allFields.Length * 2);
        var failed = new List<string>();

        using var svc = _factory.Create(device);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));

        await svc.ConnectAsync(cts.Token);
        try
        {
            foreach (var blk in BuildBlocks(allFields))
                await ReadBlockWithRetryAsync(
                    svc, device.SlaveId,
                    blk.RawStart, blk.Count, blk.ModiconBase,
                    blk.IsHolding, result, failed, cts.Token);
        }
        finally
        {
            try { await svc.DisconnectAsync(); } catch { }
        }

        if (failed.Count > 0)
            Debug.WriteLine($"[DeviceConfigService] {failed.Count} block(s) failed after {MaxAttempts} attempts:\n  - " +
                            string.Join("\n  - ", failed));

        return new ConfigReadResult(result, failed);
    }

    // Tries to read one block up to MaxAttempts times. Retries on transient failures
    // (TimeoutException, IO errors); does NOT retry on ModbusProtocolException — those
    // mean the device replied with an Illegal Data Address or similar deterministic
    // error and retrying would just burn time. After all attempts, the block is recorded
    // in `failed` and the dictionary keeps whatever was already accumulated.
    //
    // When an IO error fires, the underlying TCP socket is almost always dead — the
    // KS-3000 has been observed to drop the socket mid-read after a handful of FC03
    // requests. Retrying on the SAME svc just hits a dead pipe; we disconnect and
    // reconnect between attempts so the next try gets a fresh socket.
    private static async Task ReadBlockWithRetryAsync(
        IModbusService svc, byte slaveId, ushort rawStart, int count, int modiconBase,
        bool isHolding,
        Dictionary<ushort, ushort> result, List<string> failed,
        CancellationToken cancellationToken)
    {
        int firstModicon = modiconBase + rawStart;
        int lastModicon  = firstModicon + count - 1;
        string range     = $"{(isHolding ? "FC03" : "FC04")} {firstModicon}-{lastModicon}";

        Exception? lastError = null;
        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                if (!svc.IsConnected) await svc.ConnectAsync(cancellationToken);

                ushort[] words = isHolding
                    ? await svc.ReadHoldingRegistersAsync(slaveId, rawStart, (ushort)count, cancellationToken)
                    : await svc.ReadInputRegistersAsync(slaveId, rawStart, (ushort)count, cancellationToken);

                for (int i = 0; i < words.Length; i++)
                    result[(ushort)(modiconBase + rawStart + i)] = words[i];
                return;
            }
            catch (OperationCanceledException) { throw; }
            catch (ModbusProtocolException ex)
            {
                // Device explicitly replied with an exception — don't retry.
                lastError = ex;
                Debug.WriteLine($"[DeviceConfigService] {range} attempt {attempt}: protocol error ({ex.Message}); not retrying.");
                break;
            }
            catch (Exception ex)
            {
                lastError = ex;
                Debug.WriteLine($"[DeviceConfigService] {range} attempt {attempt}/{MaxAttempts} failed: {ex.Message}");
                // Socket likely dead — drop it so the next attempt opens a fresh one.
                try { await svc.DisconnectAsync(); } catch { }
                if (attempt < MaxAttempts)
                    await Task.Delay(RetryDelayMs, cancellationToken);
            }
        }

        failed.Add($"{range}: {lastError?.GetType().Name} — {lastError?.Message}");
    }

    public async Task WriteAsync(
        ModbusDevice device,
        ushort address,
        ushort value,
        CancellationToken cancellationToken = default)
    {
        if (!IsHolding(address))
            throw new ArgumentException($"Write requires a 4xxxx holding-register address; got {address}.");

        using var svc = _factory.Create(device);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));

        await svc.ConnectAsync(cts.Token);
        try
        {
            await svc.WriteSingleRegisterAsync(device.SlaveId, (ushort)(address - 40001), value, cts.Token);
        }
        finally
        {
            try { await svc.DisconnectAsync(); } catch { }
        }
    }

    public async Task WriteMultipleRegistersAsync(
        ModbusDevice device,
        ushort modiconAddress,
        ushort[] values,
        CancellationToken cancellationToken = default)
    {
        if (!IsHolding(modiconAddress))
            throw new ArgumentException($"Write requires a 4xxxx holding-register address; got {modiconAddress}.");
        if (values is null || values.Length == 0)
            return;

        using var svc = _factory.Create(device);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));

        await svc.ConnectAsync(cts.Token);
        try
        {
            ushort rawStart = (ushort)(modiconAddress - 40001);
            for (int offset = 0; offset < values.Length; offset += MaxWriteBlockSize)
            {
                int count = Math.Min(MaxWriteBlockSize, values.Length - offset);
                var chunk = new ushort[count];
                Array.Copy(values, offset, chunk, 0, count);
                await svc.WriteMultipleRegistersAsync(
                    device.SlaveId, (ushort)(rawStart + offset), chunk, cts.Token);
            }
        }
        finally
        {
            try { await svc.DisconnectAsync(); } catch { }
        }
    }

    public Task SendCoilResetAsync(ModbusDevice device, CancellationToken cancellationToken = default) =>
        WriteCoilAsync(device, CoilResetAddress, true, cancellationToken);

    public async Task WriteBatchAsync(
        ModbusDevice device,
        IReadOnlyList<RegisterWrite> writes,
        bool sendCoilResetAfter,
        CancellationToken cancellationToken = default)
    {
        if ((writes is null || writes.Count == 0) && !sendCoilResetAfter) return;

        // KS-3000 / Konect 120 frequently drop the TCP socket between writes (and always
        // after the commit coil). We use ONE IModbusService instance and reconnect lazily
        // whenever the previous op left the connection dead — this avoids the per-op
        // 30s timeout overhead of WriteAsync/WriteMultipleRegistersAsync without assuming
        // the device keeps the socket alive across the whole batch.
        int totalSeconds = Math.Max(TimeoutSeconds, (writes?.Count ?? 0) * 5 + 15);
        using var svc = _factory.Create(device);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(totalSeconds));

        bool anyWriteSucceeded = false;
        try
        {
            if (writes is not null)
            {
                foreach (var w in writes)
                {
                    if (!IsHolding(w.ModiconAddress))
                        throw new ArgumentException(
                            $"WriteBatch requires 4xxxx holding-register addresses; got {w.ModiconAddress}.");
                    if (w.Values is null || w.Values.Length == 0) continue;

                    ushort rawStart = (ushort)(w.ModiconAddress - 40001);

                    try
                    {
                        if (w.Values.Length == 1)
                        {
                            await RunWithReconnectAsync(svc, cts.Token,
                                (s, ct) => s.WriteSingleRegisterAsync(device.SlaveId, rawStart, w.Values[0], ct),
                                $"FC06 {w.ModiconAddress}");
                        }
                        else
                        {
                            for (int offset = 0; offset < w.Values.Length; offset += MaxWriteBlockSize)
                            {
                                int count = Math.Min(MaxWriteBlockSize, w.Values.Length - offset);
                                var chunk = new ushort[count];
                                Array.Copy(w.Values, offset, chunk, 0, count);
                                ushort blockStart = (ushort)(rawStart + offset);
                                await RunWithReconnectAsync(svc, cts.Token,
                                    (s, ct) => s.WriteMultipleRegistersAsync(device.SlaveId, blockStart, chunk, ct),
                                    $"FC16 {w.ModiconAddress + offset}+{count}");
                            }
                        }
                        anyWriteSucceeded = true;
                    }
                    catch (Exception ex) when (anyWriteSucceeded && ex is IOException)
                    {
                        // After at least one successful write, an IO failure almost certainly
                        // means the KS-3000 accepted the write and is now rebooting (~23s boot
                        // + Wi-Fi rejoin). Stop the batch here and let the caller poll for the
                        // device to come back. The remaining writes are abandoned silently
                        // because we have no way to resume after a reboot — caller's Save
                        // success message should reflect "partial" / "device rebooted".
                        Debug.WriteLine(
                            $"[DeviceConfigService] WriteBatch: device dropped socket after partial success — assuming reboot. Skipping remaining writes ({ex.GetType().Name}).");
                        sendCoilResetAfter = false; // pointless if device is rebooting
                        break;
                    }
                }
            }

            if (sendCoilResetAfter)
            {
                // KS-3000 closes the socket after applying the commit coil and never echoes
                // FC05 — treat timeout / IO / EndOfStream as success here.
                try
                {
                    if (!svc.IsConnected) await svc.ConnectAsync(cts.Token);
                    await svc.WriteSingleCoilAsync(device.SlaveId, CoilResetAddress, true, cts.Token);
                }
                catch (ModbusProtocolException ex)
                {
                    Debug.WriteLine($"[DeviceConfigService] Coil reset {CoilResetAddress} rejected — {ex.Message}");
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    Debug.WriteLine($"[DeviceConfigService] Coil reset {CoilResetAddress} — no echo, treated as success.");
                }
                catch (IOException)
                {
                    // IOException covers EndOfStreamException as well.
                    Debug.WriteLine($"[DeviceConfigService] Coil reset {CoilResetAddress} — connection closed, treated as success.");
                }
            }
        }
        finally
        {
            try { await svc.DisconnectAsync(); } catch { }
        }
    }

    public async Task<bool> WaitForDeviceReachableAsync(
        ModbusDevice device,
        int maxWaitSeconds = 60,
        CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow.AddSeconds(maxWaitSeconds);
        int delayMs = 1000;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var probeSvc = _factory.Create(device);
            using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            probeCts.CancelAfter(TimeSpan.FromSeconds(3)); // each attempt fails fast
            try
            {
                await probeSvc.ConnectAsync(probeCts.Token);
                // FC03 1 word from 40001 — every KS-3000 / Konect 120 has this register.
                _ = await probeSvc.ReadHoldingRegistersAsync(device.SlaveId, 0, 1, probeCts.Token);
                try { await probeSvc.DisconnectAsync(); } catch { }
                Debug.WriteLine($"[DeviceConfigService] WaitForDeviceReachable: device is back.");
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                try { await probeSvc.DisconnectAsync(); } catch { }
            }

            await Task.Delay(delayMs, cancellationToken);
            delayMs = Math.Min(delayMs + 500, 3000); // backoff up to 3s between probes
        }
        Debug.WriteLine($"[DeviceConfigService] WaitForDeviceReachable: gave up after {maxWaitSeconds}s.");
        return false;
    }

    // Runs one write op, reconnecting the underlying transport when the device dropped the
    // socket between operations (KS-3000 often does this). One retry is enough — the new
    // socket is fresh, and a second back-to-back failure means the device is genuinely down.
    private static async Task RunWithReconnectAsync(
        IModbusService svc, CancellationToken ct,
        Func<IModbusService, CancellationToken, Task> op,
        string label)
    {
        const int MaxAttempts = 2;
        Exception? lastError = null;
        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                if (!svc.IsConnected) await svc.ConnectAsync(ct);
                await op(svc, ct);
                return;
            }
            catch (OperationCanceledException) { throw; }
            catch (ModbusProtocolException) { throw; }
            catch (Exception ex) when (ex is IOException or EndOfStreamException)
            {
                lastError = ex;
                Debug.WriteLine($"[DeviceConfigService] {label} attempt {attempt}: {ex.GetType().Name} — reconnecting and retrying.");
                try { await svc.DisconnectAsync(); } catch { }
            }
        }
        throw lastError ?? new IOException($"{label}: unknown write failure after {MaxAttempts} attempts.");
    }

    public async Task WriteCoilAsync(
        ModbusDevice device,
        ushort coilAddress,
        bool value,
        CancellationToken cancellationToken = default)
    {
        using var svc = _factory.Create(device);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5)); // coil writes are fast; shorter than bulk-read timeout

        await svc.ConnectAsync(cts.Token);
        try
        {
            await svc.WriteSingleCoilAsync(device.SlaveId, coilAddress, value, cts.Token);
        }
        catch (ModbusProtocolException ex)
        {
            // Device rejected the coil with a Modbus exception (e.g. IllegalDataValue 0x03
            // when digital I/O is disabled on the meter). Log and swallow — the UI must not crash.
            Debug.WriteLine($"[DeviceConfigService] WriteCoilAsync: coil {coilAddress} rejected — {ex.Message}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Our own 5s timeout fired. Some KRON devices (e.g. reset coils 021-023) close
            // the TCP connection after processing without echoing the FC05 response. The coil
            // was almost certainly applied — suppress the exception so the UI doesn't crash.
            Debug.WriteLine($"[DeviceConfigService] WriteCoilAsync: no echo for coil {coilAddress} — treated as success.");
        }
        catch (IOException)
        {
            // Device closed the connection right after processing — coil was applied.
            Debug.WriteLine($"[DeviceConfigService] WriteCoilAsync: connection closed after coil {coilAddress} — treated as success.");
        }
        finally
        {
            try { await svc.DisconnectAsync(); } catch { }
        }
    }

    private static bool IsHolding(ushort a) => a is >= 40001 and <= 49999;
    private static bool IsInput(ushort a)   => a is >= 30001 and <= 39999;

    private readonly record struct ReadBlock(ushort RawStart, int Count, int ModiconBase, bool IsHolding);

    // Builds one block per RegisterField. Fields sharing the same starting address
    // (typically bit-fields on the same register) are merged into a single block whose
    // size is the max WordCount among them. Fields at different addresses are NEVER
    // coalesced — even when physically adjacent — because some devices reject reads
    // that span multiple logical fields with IllegalDataValue. Multi-word fields longer
    // than MaxBlockSize are split into MaxBlockSize-word chunks.
    private static IEnumerable<ReadBlock> BuildBlocks(IEnumerable<RegisterField> fields)
    {
        var groups = fields
            .GroupBy(f => f.Addr)
            .Select(g => new { Addr = g.Key, MaxCount = g.Max(f => f.WordCount) })
            .OrderBy(g => g.Addr);

        foreach (var g in groups)
        {
            bool isHolding = IsHolding(g.Addr);
            int modiconBase = isHolding ? 40001 : 30001;
            ushort rawStart = (ushort)(g.Addr - modiconBase);

            for (int offset = 0; offset < g.MaxCount; offset += MaxBlockSize)
            {
                int count = Math.Min(MaxBlockSize, g.MaxCount - offset);
                yield return new ReadBlock(
                    (ushort)(rawStart + offset), count, modiconBase, isHolding);
            }
        }
    }
}
