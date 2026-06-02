using System.Buffers.Binary;
using FluentAssertions;
using Modbus.Core.Protocol.Enums;
using Modbus.Core.Protocol.Rtu;

namespace Modbus.Core.Tests.Protocol.Rtu;

public class ModbusRtuFrameBuilderTests
{
    private readonly ModbusRtuFrameBuilder _builder = new();

    // ── ReadRegisters (FC03 / FC04) ──────────────────────────────────────────

    [Fact]
    public void ReadRegisters_FC03_BuildsCorrect8ByteFrame()
    {
        var frame = _builder.ReadRegisters(1, FunctionCode.ReadHoldingRegisters, 0, 10);

        frame.Should().HaveCount(8);
        frame[0].Should().Be(1, "slave ID");
        frame[1].Should().Be(0x03, "function code FC03");
        BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(2)).Should().Be(0, "start address");
        BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(4)).Should().Be(10, "quantity");
        Crc16.Validate(frame).Should().BeTrue("CRC must be valid");
    }

    [Fact]
    public void ReadRegisters_FC04_BuildsCorrect8ByteFrame()
    {
        var frame = _builder.ReadRegisters(1, FunctionCode.ReadInputRegisters, 100, 5);

        frame.Should().HaveCount(8);
        frame[1].Should().Be(0x04, "function code FC04");
        BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(2)).Should().Be(100, "start address");
        BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(4)).Should().Be(5, "quantity");
        Crc16.Validate(frame).Should().BeTrue();
    }

    [Fact]
    public void ReadRegisters_MaxSlaveId247_SetsCorrectByte()
    {
        var frame = _builder.ReadRegisters(247, FunctionCode.ReadHoldingRegisters, 0, 1);
        frame[0].Should().Be(247);
        Crc16.Validate(frame).Should().BeTrue();
    }

    [Fact]
    public void ReadRegisters_MaxAddress_SetsCorrectBytes()
    {
        var frame = _builder.ReadRegisters(1, FunctionCode.ReadHoldingRegisters, 0xFFFF, 125);

        BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(2)).Should().Be(0xFFFF);
        BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(4)).Should().Be(125);
        Crc16.Validate(frame).Should().BeTrue();
    }

    // ── WriteSingleRegister (FC06) ───────────────────────────────────────────

    [Fact]
    public void WriteSingleRegister_BuildsCorrect8ByteFrame()
    {
        var frame = _builder.WriteSingleRegister(1, 100, 0x1234);

        frame.Should().HaveCount(8);
        frame[0].Should().Be(1);
        frame[1].Should().Be(0x06, "function code FC06");
        BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(2)).Should().Be(100, "address");
        BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(4)).Should().Be(0x1234, "value");
        Crc16.Validate(frame).Should().BeTrue();
    }

    [Fact]
    public void WriteSingleRegister_ZeroValue_EncodesCorrectly()
    {
        var frame = _builder.WriteSingleRegister(1, 0, 0);

        BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(2)).Should().Be(0);
        BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(4)).Should().Be(0);
        Crc16.Validate(frame).Should().BeTrue();
    }

    // ── WriteSingleCoil (FC05) ───────────────────────────────────────────────

    [Fact]
    public void WriteSingleCoil_ResetCoil_MatchesCapturedFrame()
    {
        // Ground-truth reset frame captured from the reference Modbus client (slave 2):
        //   02 05 00 05 FF 00 9C 08   (FC05, coil 0x0005, value ON 0xFF00).
        var frame = _builder.WriteSingleCoil(2, 5, true);

        frame.Should().Equal(0x02, 0x05, 0x00, 0x05, 0xFF, 0x00, 0x9C, 0x08);
        Crc16.Validate(frame).Should().BeTrue();
    }

    [Fact]
    public void WriteSingleCoil_Off_EncodesZeroValue()
    {
        var frame = _builder.WriteSingleCoil(1, 5, false);

        frame[1].Should().Be(0x05, "function code FC05");
        BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(2)).Should().Be(5, "coil address");
        BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(4)).Should().Be(0x0000, "OFF value");
        Crc16.Validate(frame).Should().BeTrue();
    }

    // ── WriteMultipleRegisters (FC16) ────────────────────────────────────────

    [Fact]
    public void WriteMultipleRegisters_TwoValues_BuildsCorrectFrame()
    {
        var frame = _builder.WriteMultipleRegisters(1, 0, [0x000A, 0x0102]);

        // Length: 7 (header) + 4 (data) + 2 (CRC) = 13
        frame.Should().HaveCount(13);
        frame[0].Should().Be(1);
        frame[1].Should().Be(0x10, "function code FC16");
        BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(2)).Should().Be(0, "start address");
        BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(4)).Should().Be(2, "quantity");
        frame[6].Should().Be(4, "byte count = 2 registers * 2 bytes");
        BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(7)).Should().Be(0x000A, "first value");
        BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(9)).Should().Be(0x0102, "second value");
        Crc16.Validate(frame).Should().BeTrue();
    }

    [Fact]
    public void WriteMultipleRegisters_SingleValue_BuildsCorrectFrame()
    {
        var frame = _builder.WriteMultipleRegisters(1, 50, [0xABCD]);

        // Length: 7 + 2 + 2 = 11
        frame.Should().HaveCount(11);
        frame[6].Should().Be(2, "byte count = 1 register * 2 bytes");
        BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(7)).Should().Be(0xABCD);
        Crc16.Validate(frame).Should().BeTrue();
    }

    // ── ConfigureAddress (FC 0x42 — KRON custom) ────────────────────────────

    [Fact]
    public void ConfigureAddress_KnownVector_MatchesCapturedFrame()
    {
        // Ground-truth frame captured from KRON internal software:
        //   MST: 00 42 00 3D 14 4C 06 ED 48
        // Serial 4002892 (0x003D14_4C), new address 6.
        var frame = _builder.ConfigureAddress(4002892u, 6);

        frame.Should().Equal(
            [0x00, 0x42, 0x00, 0x3D, 0x14, 0x4C, 0x06, 0xED, 0x48],
            "frame must match the byte sequence captured from KRON software");
    }

    [Fact]
    public void ConfigureAddress_BroadcastSlaveAndCorrectFc_CrcValid()
    {
        var frame = _builder.ConfigureAddress(1u, 5);

        frame.Should().HaveCount(9, "broadcast(1)+FC(1)+SN(4)+newAddr(1)+CRC(2)");
        frame[0].Should().Be(0x00, "broadcast slave address");
        frame[1].Should().Be(0x42, "KRON configAddress function code");
        BinaryPrimitives.ReadUInt32BigEndian(frame.AsSpan(2)).Should().Be(1u, "serial number big-endian");
        frame[6].Should().Be(5, "new slave address");
        Crc16.Validate(frame).Should().BeTrue("CRC must cover all 7 data bytes");
    }

    // ── ReportSlaveId (FC17) ─────────────────────────────────────────────────

    [Fact]
    public void ReportSlaveId_BuildsCorrect4ByteFrame()
    {
        var frame = _builder.ReportSlaveId(1);

        frame.Should().HaveCount(4);
        frame[0].Should().Be(1, "slave ID");
        frame[1].Should().Be(0x11, "function code FC17");
        Crc16.Validate(frame).Should().BeTrue();
    }

    [Fact]
    public void ReportSlaveId_DifferentSlaveIds_ProduceDifferentFrames()
    {
        var frame1 = _builder.ReportSlaveId(1);
        var frame2 = _builder.ReportSlaveId(2);

        frame1[0].Should().NotBe(frame2[0]);
        // CRC should also differ
        frame1[2..].Should().NotEqual(frame2[2..]);
    }
}
