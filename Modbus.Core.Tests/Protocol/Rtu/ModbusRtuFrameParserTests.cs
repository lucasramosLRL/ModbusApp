using System.Buffers.Binary;
using FluentAssertions;
using Modbus.Core.Protocol.Enums;
using Modbus.Core.Protocol.Exceptions;
using Modbus.Core.Protocol.Rtu;

namespace Modbus.Core.Tests.Protocol.Rtu;

public class ModbusRtuFrameParserTests
{
    private readonly ModbusRtuFrameParser _parser = new();

    /// <summary>
    /// Builds a valid RTU read response: SlaveAddr(1) + FC(1) + ByteCount(1) + Data(N*2) + CRC(2)
    /// </summary>
    private static byte[] BuildReadResponse(byte slaveId, byte fc, ushort[] values)
    {
        int byteCount = values.Length * 2;
        int messageLen = 3 + byteCount;  // without CRC
        var frame = new byte[messageLen + 2];
        frame[0] = slaveId;
        frame[1] = fc;
        frame[2] = (byte)byteCount;
        for (int i = 0; i < values.Length; i++)
            BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(3 + i * 2), values[i]);
        Crc16.Append(frame, messageLen);
        return frame;
    }

    /// <summary>
    /// Builds a valid RTU error response: SlaveAddr(1) + FC|0x80(1) + ExceptionCode(1) + CRC(2)
    /// </summary>
    private static byte[] BuildErrorResponse(byte slaveId, byte fc, byte exceptionCode)
    {
        var frame = new byte[5];
        frame[0] = slaveId;
        frame[1] = (byte)(fc | 0x80);
        frame[2] = exceptionCode;
        Crc16.Append(frame, 3);
        return frame;
    }

    // ── ParseReadRegisters ───────────────────────────────────────────────────

    [Fact]
    public void ParseReadRegisters_TwoRegisters_ReturnsParsedValues()
    {
        var response = BuildReadResponse(1, 0x03, [0x1234, 0x5678]);

        var result = _parser.ParseReadRegisters(response);

        result.Should().HaveCount(2);
        result[0].Should().Be(0x1234);
        result[1].Should().Be(0x5678);
    }

    [Fact]
    public void ParseReadRegisters_SingleRegister_ReturnsSingleValue()
    {
        var response = BuildReadResponse(1, 0x04, [0xABCD]);

        var result = _parser.ParseReadRegisters(response);

        result.Should().HaveCount(1);
        result[0].Should().Be(0xABCD);
    }

    [Fact]
    public void ParseReadRegisters_FiveRegisters_ReturnsAllValues()
    {
        ushort[] values = [0x0001, 0x0002, 0x0003, 0x0004, 0x0005];
        var response = BuildReadResponse(1, 0x03, values);

        var result = _parser.ParseReadRegisters(response);

        result.Should().Equal(values);
    }

    // ── Error handling ───────────────────────────────────────────────────────

    [Fact]
    public void ParseReadRegisters_FrameTooShort_ThrowsInvalidDataException()
    {
        byte[] response = [0x01, 0x03, 0x00, 0x00];  // 4 bytes, minimum is 5

        var act = () => _parser.ParseReadRegisters(response);

        act.Should().Throw<InvalidDataException>()
            .WithMessage("*too short*");
    }

    [Fact]
    public void ParseReadRegisters_BadCrc_ThrowsInvalidDataException()
    {
        var response = BuildReadResponse(1, 0x03, [0x1234]);
        // Corrupt CRC
        response[^1] ^= 0xFF;

        var act = () => _parser.ParseReadRegisters(response);

        act.Should().Throw<InvalidDataException>()
            .WithMessage("*CRC*");
    }

    [Fact]
    public void ParseReadRegisters_ErrorResponse_ThrowsModbusProtocolException()
    {
        var response = BuildErrorResponse(1, 0x03, (byte)ModbusExceptionCode.IllegalDataAddress);

        var act = () => _parser.ParseReadRegisters(response);

        act.Should().Throw<ModbusProtocolException>()
            .Where(e => e.FunctionCode == FunctionCode.ReadHoldingRegisters)
            .Where(e => e.ExceptionCode == ModbusExceptionCode.IllegalDataAddress);
    }

    [Fact]
    public void ParseReadRegisters_ErrorResponse_FC04_ThrowsCorrectFunctionCode()
    {
        var response = BuildErrorResponse(1, 0x04, (byte)ModbusExceptionCode.IllegalFunction);

        var act = () => _parser.ParseReadRegisters(response);

        act.Should().Throw<ModbusProtocolException>()
            .Where(e => e.FunctionCode == FunctionCode.ReadInputRegisters);
    }

    // ── ValidateWrite ────────────────────────────────────────────────────────

    [Fact]
    public void ValidateWriteSingleRegister_ErrorResponse_Throws()
    {
        var response = BuildErrorResponse(1, 0x06, (byte)ModbusExceptionCode.IllegalDataValue);

        var act = () => _parser.ValidateWriteSingleRegister(response);

        act.Should().Throw<ModbusProtocolException>()
            .Where(e => e.FunctionCode == FunctionCode.WriteSingleRegister);
    }

    [Fact]
    public void ValidateWriteMultipleRegisters_ErrorResponse_Throws()
    {
        var response = BuildErrorResponse(1, 0x10, (byte)ModbusExceptionCode.ServerDeviceFailure);

        var act = () => _parser.ValidateWriteMultipleRegisters(response);

        act.Should().Throw<ModbusProtocolException>()
            .Where(e => e.FunctionCode == FunctionCode.WriteMultipleRegisters);
    }

    // ── ParseReportSlaveId ───────────────────────────────────────────────────

    [Fact]
    public void ParseReportSlaveId_ValidResponse_ReturnsParsedData()
    {
        // Build: SlaveAddr(1) + FC(1) + ByteCount(1) + [SlaveId, RunIndicator, ...] + CRC(2)
        byte[] payload = [0xF2, 0xFF, 0x01, 0x02];  // device code, run indicator, extra data
        int messageLen = 3 + payload.Length;
        var frame = new byte[messageLen + 2];
        frame[0] = 1;
        frame[1] = 0x11;
        frame[2] = (byte)payload.Length;
        Array.Copy(payload, 0, frame, 3, payload.Length);
        Crc16.Append(frame, messageLen);

        var result = _parser.ParseReportSlaveId(frame);

        result.RawData.Should().Equal(payload);
        result.RunIndicatorStatus.Should().Be(0xFF);
    }

    [Fact]
    public void ParseReportSlaveId_MinimalPayload_RunIndicatorDefaultsToZero()
    {
        // Single-byte payload: only slave ID, no run indicator
        byte[] payload = [0xF2];
        int messageLen = 3 + payload.Length;
        var frame = new byte[messageLen + 2];
        frame[0] = 1;
        frame[1] = 0x11;
        frame[2] = (byte)payload.Length;
        frame[3] = payload[0];
        Crc16.Append(frame, messageLen);

        var result = _parser.ParseReportSlaveId(frame);

        result.RawData.Should().Equal(payload);
        result.RunIndicatorStatus.Should().Be(0x00);
    }

    // ── ParseReadConfigDisp (FC 0x79) ────────────────────────────────────────

    // Builds a valid FC 0x79 response frame.
    // Layout: SlaveAddr(1) + 0x79(1) + SN(4) + ?(1) + Count(1) + Data(Count) + CRC(2)
    private static byte[] BuildReadConfigDispResponse(byte slaveAddr, uint sn, byte[] data)
    {
        byte count = (byte)data.Length;
        int messageLen = 8 + count; // without CRC
        var frame = new byte[messageLen + 2];
        frame[0] = slaveAddr;
        frame[1] = 0x79;
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(2), sn);
        frame[6] = 0x00; // reserved byte
        frame[7] = count;
        Array.Copy(data, 0, frame, 8, count);
        Crc16.Append(frame, messageLen);
        return frame;
    }

    [Fact]
    public void ParseReadConfigDisp_ValidResponse_ReturnsDataBytes()
    {
        byte[] data   = [0x00, 0x0B]; // InOutCfg = 0x000B (EDP1+EDP2+SD1)
        var    frame  = BuildReadConfigDispResponse(slaveAddr: 2, sn: 4002892u, data: data);

        var result = _parser.ParseReadConfigDisp(frame, expectedCount: 2);

        result.Should().Equal(data);
    }

    [Fact]
    public void ParseReadConfigDisp_AllChannelsEnabled_ReturnsBitmask001F()
    {
        byte[] data  = [0x00, 0x1F]; // InOutCfg = 0x001F — all five channels
        var    frame = BuildReadConfigDispResponse(slaveAddr: 3, sn: 1u, data: data);

        var result = _parser.ParseReadConfigDisp(frame, expectedCount: 2);

        ushort inOutCfg = (ushort)((result[0] << 8) | result[1]);
        inOutCfg.Should().Be(0x001F);
    }

    [Fact]
    public void ParseReadConfigDisp_BadCrc_ThrowsInvalidDataException()
    {
        byte[] data  = [0x00, 0x0B];
        var    frame = BuildReadConfigDispResponse(slaveAddr: 2, sn: 4002892u, data: data);
        frame[^1] ^= 0xFF; // corrupt CRC

        var act = () => _parser.ParseReadConfigDisp(frame, expectedCount: 2);

        act.Should().Throw<InvalidDataException>().WithMessage("*CRC*");
    }

    [Fact]
    public void ParseReadConfigDisp_TooShortResponse_ThrowsInvalidDataException()
    {
        var frame = new byte[3]; // way too short

        var act = () => _parser.ParseReadConfigDisp(frame, expectedCount: 2);

        act.Should().Throw<InvalidDataException>().WithMessage("*too short*");
    }

    [Fact]
    public void ParseReadConfigDisp_ErrorResponse_ThrowsModbusProtocolException()
    {
        // FC 0x79 | 0x80 = 0xF9 exception frame
        var frame = BuildErrorResponse(slaveId: 2, fc: 0x79, exceptionCode: (byte)ModbusExceptionCode.IllegalFunction);

        var act = () => _parser.ParseReadConfigDisp(frame, expectedCount: 2);

        act.Should().Throw<ModbusProtocolException>();
    }
}
