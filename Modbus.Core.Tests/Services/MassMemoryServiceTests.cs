using System.Buffers.Binary;
using FluentAssertions;
using Modbus.Core.Protocol.Exceptions;
using Modbus.Core.Protocol.Rtu;
using Modbus.Core.Protocol.Tcp;
using Modbus.Core.Services;

namespace Modbus.Core.Tests.Services;

public class MassMemoryServiceTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a raw data block: 5-byte BCD timestamp + GP*4 float bytes (LE) + 1 checksum byte.
    /// </summary>
    private static byte[] BuildBlockData(byte[] timestamp, float[] values)
    {
        var data = new byte[5 + values.Length * 4 + 1];
        timestamp.CopyTo(data, 0);
        for (int j = 0; j < values.Length; j++)
            BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(5 + j * 4), values[j]);

        byte cs = 0;
        for (int k = 0; k < 5 + values.Length * 4; k++)
            cs += data[k];
        data[5 + values.Length * 4] = cs;

        return data;
    }

    // ── ParseBlock — Timestamp ────────────────────────────────────────────────

    [Fact]
    public void ParseBlock_KnownTimestamp_DecodesCorrectly()
    {
        // Documented example: 43 24 54 84 22 → 14/10/2022 14:24:43
        var ts  = new byte[] { 0x43, 0x24, 0x54, 0x84, 0x22 };
        var data = BuildBlockData(ts, [0f]);

        var block = MassMemoryService.ParseBlock(data, gp: 1, blockIndex: 1, iterationIndex: 0);

        block.Timestamp.Should().Be(new DateTime(2022, 10, 14, 14, 24, 43));
    }

    [Fact]
    public void ParseBlock_MidnightTimestamp_DecodesCorrectly()
    {
        // Target: 00:00:00 01/01/2000
        // sec=0  → b0=0x00
        // min=0  → b1=0x00
        // hour=0 → b1[7]=0, b2[4:0]=0 → b2=0x00
        // day=1  → BCD(b2[7:5]>>2 | b3[2:0])=0x01 → b3[2:0]=1
        // mon=1  → BCD((b3>>3)&0x1F)=0x01       → b3[5:3]=0x01 → b3=0x09
        // year=0 → b4=0x00
        var ts   = new byte[] { 0x00, 0x00, 0x00, 0x09, 0x00 };
        var data = BuildBlockData(ts, [0f]);

        var block = MassMemoryService.ParseBlock(data, gp: 1, blockIndex: 1, iterationIndex: 0);

        block.Timestamp.Should().Be(new DateTime(2000, 1, 1, 0, 0, 0));
    }

    // ── ParseBlock — Float values ─────────────────────────────────────────────

    [Fact]
    public void ParseBlock_SingleFloat_DecodesLittleEndian()
    {
        var ts   = new byte[] { 0x43, 0x24, 0x54, 0x84, 0x22 };
        var data = BuildBlockData(ts, [60.0f]);

        var block = MassMemoryService.ParseBlock(data, gp: 1, blockIndex: 1, iterationIndex: 0);

        block.Values.Should().HaveCount(1);
        block.Values[0].Should().BeApproximately(60.0, 1e-4);
    }

    [Fact]
    public void ParseBlock_MultipleFloats_AllDecoded()
    {
        var ts     = new byte[] { 0x43, 0x24, 0x54, 0x84, 0x22 };
        var values = new float[] { 603.310f, 347.704f, 60.033f, 0.0f };
        var data   = BuildBlockData(ts, values);

        var block = MassMemoryService.ParseBlock(data, gp: 4, blockIndex: 1, iterationIndex: 0);

        block.Values.Should().HaveCount(4);
        block.Values[0].Should().BeApproximately(603.310, 0.001);
        block.Values[1].Should().BeApproximately(347.704, 0.001);
        block.Values[2].Should().BeApproximately(60.033,  0.001);
        block.Values[3].Should().BeApproximately(0.0,     1e-9);
    }

    // ── ParseBlock — Checksum ─────────────────────────────────────────────────

    [Fact]
    public void ParseBlock_ValidChecksum_SetsChecksumOkTrue()
    {
        var ts   = new byte[] { 0x43, 0x24, 0x54, 0x84, 0x22 };
        var data = BuildBlockData(ts, [60.0f]);

        var block = MassMemoryService.ParseBlock(data, gp: 1, blockIndex: 1, iterationIndex: 0);

        block.ChecksumOk.Should().BeTrue();
    }

    [Fact]
    public void ParseBlock_CorruptedChecksum_SetsChecksumOkFalse()
    {
        var ts   = new byte[] { 0x43, 0x24, 0x54, 0x84, 0x22 };
        var data = BuildBlockData(ts, [60.0f]);
        data[^1] ^= 0xFF; // flip checksum byte

        var block = MassMemoryService.ParseBlock(data, gp: 1, blockIndex: 1, iterationIndex: 0);

        block.ChecksumOk.Should().BeFalse();
    }

    // ── ParseBlock — BlockIndex / IterationIndex ──────────────────────────────

    [Fact]
    public void ParseBlock_PassthroughIndexes_ArePreserved()
    {
        var ts   = new byte[] { 0x43, 0x24, 0x54, 0x84, 0x22 };
        var data = BuildBlockData(ts, [0f]);

        var block = MassMemoryService.ParseBlock(data, gp: 1, blockIndex: 7, iterationIndex: 12);

        block.BlockIndex.Should().Be(7);
        block.IterationIndex.Should().Be(12);
    }

    // ── ComputeStartPosition ──────────────────────────────────────────────────

    [Fact]
    public void ComputeStartPosition_ZeroStart_ReturnsInitialSectorBlock()
    {
        var (sector, block) = MassMemoryService.ComputeStartPosition(ini: 2, ca: 5, qsf: 4, startFrom: 0);

        sector.Should().Be(2);
        block.Should().Be(0);
    }

    [Fact]
    public void ComputeStartPosition_WithinFirstSector_AdvancesBlockOnly()
    {
        var (sector, block) = MassMemoryService.ComputeStartPosition(ini: 0, ca: 10, qsf: 4, startFrom: 3);

        sector.Should().Be(0);
        block.Should().Be(3);
    }

    [Fact]
    public void ComputeStartPosition_ExactlyOneSectorBoundary_WrapsBlock()
    {
        // CA=5: after 5 iterations, block wraps to 0 and sector advances.
        var (sector, block) = MassMemoryService.ComputeStartPosition(ini: 0, ca: 5, qsf: 4, startFrom: 5);

        sector.Should().Be(1);
        block.Should().Be(0);
    }

    [Fact]
    public void ComputeStartPosition_SpansMultipleSectors_Correct()
    {
        // CA=3: after 7 iterations → 2 full sectors (6 iterations) + 1 block = sector 2, block 1
        var (sector, block) = MassMemoryService.ComputeStartPosition(ini: 0, ca: 3, qsf: 8, startFrom: 7);

        sector.Should().Be(2);
        block.Should().Be(1);
    }

    [Fact]
    public void ComputeStartPosition_FullCircle_WrapsBackToStart()
    {
        // QSF=4, CA=3 → 12 blocks = full circle from INI=1 back to INI=1
        var (sector, block) = MassMemoryService.ComputeStartPosition(ini: 1, ca: 3, qsf: 4, startFrom: 12);

        sector.Should().Be(1);
        block.Should().Be(0);
    }

    [Fact]
    public void ComputeStartPosition_NonZeroIni_PreservesOffset()
    {
        // With INI=2, CA=5, QSF=4, startFrom=6 → sector=(2+1)%4=3, block=1
        var (sector, block) = MassMemoryService.ComputeStartPosition(ini: 2, ca: 5, qsf: 4, startFrom: 6);

        sector.Should().Be(3);
        block.Should().Be(1);
    }

    // ── ParseReadFileRecord — TCP parser (rdl-2 fix) ──────────────────────────

    /// <summary>
    /// Builds a valid FC14 TCP response:
    /// MBAP(7) + FC=0x14(1) + RDL(1) + FRL(1) + RT=0x06(1) + data(QTD*2)
    /// </summary>
    private static byte[] BuildTcpFc14Response(byte slaveId, byte[] data)
    {
        int qtd2   = data.Length;           // QTD*2 bytes
        int rdl    = qtd2 + 2;              // RDL = FRL(1) + RT(1) + data
        int frl    = qtd2 + 1;              // FRL = RT(1) + data
        int pduLen = 1 + 1 + 1 + 1 + qtd2; // FC + RDL + FRL + RT + data = QTD*2+4
        int mbapLength = 1 + pduLen;        // UnitId + PDU

        var frame = new byte[7 + pduLen];
        BinaryPrimitives.WriteUInt16BigEndian(frame,           0x0001);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(2), 0x0000);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(4), (ushort)mbapLength);
        frame[6]  = slaveId;
        frame[7]  = 0x14;
        frame[8]  = (byte)rdl;
        frame[9]  = (byte)frl;
        frame[10] = 0x06;
        data.CopyTo(frame, 11);
        return frame;
    }

    [Fact]
    public void ParseReadFileRecord_Tcp_ReturnsExactDataBytes()
    {
        var data     = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A };
        var response = BuildTcpFc14Response(slaveId: 1, data);

        var result = ModbusTcpFrameParser.ParseReadFileRecord(response);

        result.Should().Equal(data);
    }

    [Fact]
    public void ParseReadFileRecord_Tcp_TruncatedFrame_Throws()
    {
        var data     = new byte[10];
        var response = BuildTcpFc14Response(slaveId: 1, data);
        var truncated = response[..^1]; // remove last byte

        var act = () => ModbusTcpFrameParser.ParseReadFileRecord(truncated);

        act.Should().Throw<InvalidDataException>().WithMessage("*truncated*");
    }

    [Fact]
    public void ParseReadFileRecord_Tcp_ErrorResponse_ThrowsModbusProtocolException()
    {
        var frame = new byte[9];
        BinaryPrimitives.WriteUInt16BigEndian(frame,           0x0001);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(2), 0x0000);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(4), 3);
        frame[6] = 1;
        frame[7] = 0x14 | 0x80;
        frame[8] = 0x02; // IllegalDataAddress

        var act = () => ModbusTcpFrameParser.ParseReadFileRecord(frame);

        act.Should().Throw<ModbusProtocolException>();
    }

    // ── ParseReadFileRecord — RTU parser (rdl-2 fix) ──────────────────────────

    /// <summary>
    /// Builds a valid FC14 RTU response:
    /// SlaveId(1) + FC=0x14(1) + RDL(1) + FRL(1) + RT=0x06(1) + data(QTD*2) + CRC(2)
    /// </summary>
    private static byte[] BuildRtuFc14Response(byte slaveId, byte[] data)
    {
        int qtd2     = data.Length;
        int rdl      = qtd2 + 2;
        int frl      = qtd2 + 1;
        int frameLen = 5 + qtd2; // without CRC

        var frame = new byte[frameLen + 2];
        frame[0] = slaveId;
        frame[1] = 0x14;
        frame[2] = (byte)rdl;
        frame[3] = (byte)frl;
        frame[4] = 0x06;
        data.CopyTo(frame, 5);
        Crc16.Append(frame, frameLen);
        return frame;
    }

    [Fact]
    public void ParseReadFileRecord_Rtu_ReturnsExactDataBytes()
    {
        var parser   = new ModbusRtuFrameParser();
        var data     = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A };
        var response = BuildRtuFc14Response(slaveId: 1, data);

        var result = parser.ParseReadFileRecord(response);

        result.Should().Equal(data);
    }

    [Fact]
    public void ParseReadFileRecord_Rtu_TruncatedFrame_Throws()
    {
        var parser   = new ModbusRtuFrameParser();
        var data     = new byte[10];
        var response = BuildRtuFc14Response(slaveId: 1, data);
        var truncated = response[..^1];

        var act = () => parser.ParseReadFileRecord(truncated);

        act.Should().Throw<Exception>();
    }
}
