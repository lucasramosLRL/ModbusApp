using Modbus.Core.Domain.Entities;

namespace Modbus.Core.Services;

/// <summary>
/// Addresses passed to ReadAsync/WriteAsync must be Modicon-style register numbers:
///   4xxxx  →  Holding Register (FC03), raw address = modiconAddr - 40001
///   3xxxx  →  Input Register   (FC04), raw address = modiconAddr - 30001
/// The returned dictionary is keyed by the same Modicon numbers.
/// </summary>
public sealed class DeviceConfigService : IDeviceConfigService
{
    private const int MaxGap       = 5;
    private const int MaxBlockSize = 32;  // FC03/FC04 limit per request
    private const int TimeoutSeconds = 10;

    private readonly IModbusServiceFactory _factory;

    public DeviceConfigService(IModbusServiceFactory factory) => _factory = factory;

    public async Task<IReadOnlyDictionary<ushort, ushort>> ReadAsync(
        ModbusDevice device,
        IEnumerable<ushort> addresses,
        CancellationToken cancellationToken = default)
    {
        var all = addresses.Distinct().ToArray();
        if (all.Length == 0) return new Dictionary<ushort, ushort>();

        var holdingAddrs = all.Where(IsHolding).OrderBy(a => a).ToArray();
        var inputAddrs   = all.Where(IsInput).OrderBy(a => a).ToArray();

        var result = new Dictionary<ushort, ushort>(all.Length * 2);

        using var svc = _factory.Create(device);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));

        await svc.ConnectAsync(cts.Token);
        try
        {
            foreach (var (rawStart, count, modiconBase) in BuildBlocks(holdingAddrs, 40001))
            {
                var words = await svc.ReadHoldingRegistersAsync(
                    device.SlaveId, rawStart, (ushort)count, cts.Token);

                for (int i = 0; i < words.Length; i++)
                    result[(ushort)(modiconBase + rawStart + i)] = words[i];
            }

            foreach (var (rawStart, count, modiconBase) in BuildBlocks(inputAddrs, 30001))
            {
                var words = await svc.ReadInputRegistersAsync(
                    device.SlaveId, rawStart, (ushort)count, cts.Token);

                for (int i = 0; i < words.Length; i++)
                    result[(ushort)(modiconBase + rawStart + i)] = words[i];
            }
        }
        finally
        {
            try { await svc.DisconnectAsync(); } catch { }
        }

        return result;
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

    private static bool IsHolding(ushort a) => a is >= 40001 and <= 49999;
    private static bool IsInput(ushort a)   => a is >= 30001 and <= 39999;

    // Groups Modicon addresses into (rawStart, wordCount, modiconBase) blocks,
    // coalescing raw addresses within MaxGap and splitting at MaxBlockSize (32).
    private static IEnumerable<(ushort RawStart, int Count, int ModiconBase)> BuildBlocks(
        ushort[] sortedModicon, int modiconBase)
    {
        if (sortedModicon.Length == 0) yield break;

        ushort ToRaw(ushort m) => (ushort)(m - modiconBase);

        ushort blockRawStart = ToRaw(sortedModicon[0]);
        ushort blockRawEnd   = blockRawStart;

        for (int i = 1; i < sortedModicon.Length; i++)
        {
            ushort raw = ToRaw(sortedModicon[i]);
            if (raw - blockRawEnd <= MaxGap)
            {
                blockRawEnd = raw;
            }
            else
            {
                foreach (var chunk in SplitBlock(blockRawStart, blockRawEnd, modiconBase))
                    yield return chunk;
                blockRawStart = raw;
                blockRawEnd   = raw;
            }
        }

        foreach (var chunk in SplitBlock(blockRawStart, blockRawEnd, modiconBase))
            yield return chunk;
    }

    // Splits a single coalesced block into MaxBlockSize-word chunks.
    private static IEnumerable<(ushort RawStart, int Count, int ModiconBase)> SplitBlock(
        ushort rawStart, ushort rawEnd, int modiconBase)
    {
        int total = rawEnd - rawStart + 1;
        for (int offset = 0; offset < total; offset += MaxBlockSize)
        {
            int count = Math.Min(MaxBlockSize, total - offset);
            yield return ((ushort)(rawStart + offset), count, modiconBase);
        }
    }
}
