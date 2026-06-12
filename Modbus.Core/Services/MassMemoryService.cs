using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Modbus.Core.Domain.Entities;
using Modbus.Core.Domain.Enums;
using Modbus.Core.Protocol.Rtu;
using Modbus.Core.Protocol.Tcp;
using Modbus.Core.Transport;
using Modbus.Core.Transport.Rtu;
using Modbus.Core.Transport.Tcp;

namespace Modbus.Core.Services;

public sealed class MassMemoryService : IMassMemoryService
{
    // FC04 raw address for control block (Modicon 33931 = raw 3930)
    private const ushort ControlBlockRawAddress = 3930;
    private const ushort ControlBlockCount      = 5;

    private readonly IModbusServiceFactory _factory;

    public MassMemoryService(IModbusServiceFactory factory) => _factory = factory;

    public async Task<MassMemoryControlBlock?> ReadControlBlockAsync(
        ModbusDevice device, CancellationToken ct = default)
    {
        try
        {
            using var svc = _factory.Create(device);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            await svc.ConnectAsync(cts.Token);
            try
            {
                var words = await svc.ReadInputRegistersAsync(
                    device.SlaveId, ControlBlockRawAddress, ControlBlockCount, cts.Token);
                return ParseControlBlock(words);
            }
            finally
            {
                try { await svc.DisconnectAsync(); } catch { }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MassMemoryService] ReadControlBlock failed: {ex.Message}");
            return null;
        }
    }

    public async IAsyncEnumerable<MassMemoryBlock> ReadBlocksAsync(
        ModbusDevice device,
        MassMemoryControlBlock ctrl,
        int startFrom = 0,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ushort qtd = (ushort)(3 + 2 * ctrl.GP);

        var (transport, buildFc14, parseFc14, expectedLength) = CreateSession(device, qtd);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromMinutes(10));

        using (transport)
        {
            await transport.ConnectAsync(cts.Token);

            // Fast-forward sector/block to the resume position.
            int sector = ctrl.INI;
            int block  = 0;
            for (int k = 0; k < startFrom; k++)
            {
                if (++block >= ctrl.CA) { block = 0; sector = (sector + 1) % ctrl.QSF; }
            }

            for (int i = startFrom; i < ctrl.BGS; i++)
            {
                cts.Token.ThrowIfCancellationRequested();

                MassMemoryBlock? mmBlock = null;
                try
                {
                    var request  = buildFc14((ushort)sector, (ushort)block, qtd);
                    var response = await transport.SendAsync(request, expectedLength, cts.Token);
                    var data     = parseFc14(response);
                    mmBlock = ParseBlock(data, ctrl.GP, i + 1, i);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MassMemoryService] Block {i + 1} ({sector},{block}) failed: {ex.Message}");
                }

                if (mmBlock is not null)
                    yield return mmBlock;

                if (++block >= ctrl.CA) { block = 0; sector = (sector + 1) % ctrl.QSF; }
            }

            try { await transport.DisconnectAsync(); } catch { }
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static MassMemoryControlBlock ParseControlBlock(ushort[] words)
    {
        // 5 registers = 10 bytes (MSB-first per register)
        Span<byte> raw = stackalloc byte[10];
        for (int i = 0; i < 5; i++)
        {
            raw[i * 2]     = (byte)(words[i] >> 8);
            raw[i * 2 + 1] = (byte)(words[i] & 0xFF);
        }

        ushort qsf = (ushort)((raw[0] << 8) | raw[1]);
        byte   gp  = raw[2];
        int    bgs = (raw[3] << 16) | (raw[4] << 8) | raw[5];
        ushort ini = (ushort)((raw[6] << 8) | raw[7]);
        ushort ca  = (ushort)((raw[8] << 8) | raw[9]);

        return new MassMemoryControlBlock(qsf, gp, bgs, ini, ca);
    }

    internal static MassMemoryBlock ParseBlock(byte[] data, byte gp, int blockIndex, int iterationIndex)
    {
        // DataHora: 5 bytes (b0..b4) packed BCD
        byte b0 = data[0], b1 = data[1], b2 = data[2], b3 = data[3], b4 = data[4];
        int sec  = Bcd(b0 & 0x7F);
        int min  = Bcd(b1 & 0x7F);
        int hour = Bcd(((b1 & 0x80) >> 2) | (b2 & 0x1F));
        int day  = Bcd(((b2 & 0xE0) >> 2) | (b3 & 0x07));
        int mon  = Bcd((b3 >> 3) & 0x1F);
        int year = Bcd(b4) + 2000;

        DateTime ts;
        try   { ts = new DateTime(year, mon, day, hour, min, sec); }
        catch { ts = DateTime.MinValue; }

        // GP × Float32 values — stored as little-endian IEEE 754 in mass memory blocks.
        var values = new double[gp];
        for (int j = 0; j < gp; j++)
            values[j] = BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(5 + j * 4, 4));

        // Checksum: sum of data bytes 0..(5+4*gp-1) mod 256
        int checksumOffset = 5 + 4 * gp;
        byte computed = 0;
        for (int k = 0; k < checksumOffset; k++)
            computed += data[k];
        bool checksumOk = computed == data[checksumOffset];

        return new MassMemoryBlock(ts, values, checksumOk, blockIndex, iterationIndex);
    }

    /// <summary>
    /// Computes the (sector, block) start position for a given loop iteration index,
    /// fast-forwarding through the circular sector/block structure.
    /// </summary>
    internal static (int sector, int block) ComputeStartPosition(
        int ini, int ca, int qsf, int startFrom)
    {
        int sector = ini;
        int block  = 0;
        for (int k = 0; k < startFrom; k++)
        {
            if (++block >= ca) { block = 0; sector = (sector + 1) % qsf; }
        }
        return (sector, block);
    }

    private static int Bcd(int v) => ((v >> 4) & 0xF) * 10 + (v & 0xF);

    private static (IModbusTransport transport,
                    Func<ushort, ushort, ushort, byte[]> buildFc14,
                    Func<byte[], byte[]> parseFc14,
                    int expectedLength)
        CreateSession(ModbusDevice device, ushort qtd)
    {
        if (device.TransportType == TransportType.Rtu && device.Rtu is not null)
        {
            var builder   = new ModbusRtuFrameBuilder();
            var parser    = new ModbusRtuFrameParser();
            var transport = new RtuModbusTransport(device.Rtu);
            return (transport,
                    (s, b, q) => builder.ReadFileRecord(device.SlaveId, s, b, q),
                    parser.ParseReadFileRecord,
                    qtd * 2 + 7);
        }
        else if (device.TransportType == TransportType.Tcp && device.Tcp is not null)
        {
            var builder   = new ModbusTcpFrameBuilder();
            var transport = new TcpModbusTransport(device.Tcp);
            return (transport,
                    (s, b, q) => builder.ReadFileRecord(device.SlaveId, s, b, q),
                    ModbusTcpFrameParser.ParseReadFileRecord,
                    qtd * 2 + 11);
        }
        else
        {
            throw new InvalidOperationException(
                $"Device '{device.Name}' has no valid transport configuration for mass memory reading.");
        }
    }
}
