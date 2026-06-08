using System.Buffers.Binary;
using Modbus.Core.Protocol.Enums;
using Modbus.Core.Protocol.Exceptions;
using Modbus.Core.Protocol.Framing;
using Modbus.Core.Protocol.Models;

namespace Modbus.Core.Protocol.Rtu;

public class ModbusRtuFrameParser : IModbusFrameParser
{
    /// <summary>
    /// Validates CRC and checks for a Modbus error response (FC with bit 7 set).
    /// RTU error frame: SlaveAddr(1) + FC|0x80(1) + ExceptionCode(1) + CRC(2) = 5 bytes minimum.
    /// </summary>
    private static void ValidateCrcAndErrors(byte[] response)
    {
        if (response.Length < 5)
            throw new InvalidDataException(
                $"RTU response too short: {response.Length} bytes (minimum 5).");

        if (!Crc16.Validate(response))
            throw new InvalidDataException("Modbus RTU CRC validation failed.");

        if ((response[1] & 0x80) != 0)
            throw new ModbusProtocolException(
                (FunctionCode)(response[1] & 0x7F),
                (ModbusExceptionCode)response[2]);
    }

    public ushort[] ParseReadRegisters(byte[] response)
    {
        ValidateCrcAndErrors(response);
        // ADU: SlaveAddr(1) + FC(1) + ByteCount(1) + Data(N*2) + CRC(2)
        int byteCount = response[2];
        var registers = new ushort[byteCount / 2];
        for (int i = 0; i < registers.Length; i++)
            registers[i] = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(3 + i * 2));
        return registers;
    }

    public void ValidateWriteSingleCoil(byte[] response) =>
        ValidateCrcAndErrors(response);

    public void ValidateWriteSingleRegister(byte[] response) =>
        ValidateCrcAndErrors(response);

    public void ValidateWriteMultipleRegisters(byte[] response) =>
        ValidateCrcAndErrors(response);

    /// <summary>
    /// Parses a KRON FC 0x79 (ReadConfigDisp) response.
    /// Response layout: SlaveAddr(1) + 0x79(1) + SN(4) + ?(1) + Count(1) + Data(Count) + CRC(2).
    /// Verifies CRC and FC byte; returns the raw data bytes.
    /// </summary>
    public byte[] ParseReadConfigDisp(byte[] response, byte expectedCount)
    {
        // Check for a Modbus exception frame first (5 bytes: slave+FC|0x80+code+CRC).
        // RtuModbusTransport shrinks the read buffer to 5 bytes when it detects bit7 set,
        // so an exception response arrives as a 5-byte frame, not a full-length response.
        if (response.Length >= 5 && (response[1] & 0x80) != 0 && Crc16.Validate(response))
            throw new ModbusProtocolException(
                (FunctionCode)(response[1] & 0x7F),
                (ModbusExceptionCode)response[2]);

        int minLength = 10 + expectedCount;
        if (response.Length < minLength)
            throw new InvalidDataException(
                $"FC 0x79 response too short: {response.Length} bytes (expected {minLength}).");

        if (!Crc16.Validate(response))
            throw new InvalidDataException("FC 0x79 CRC validation failed.");

        if (response[1] != 0x79)
            throw new InvalidDataException(
                $"FC 0x79 response has unexpected function code: 0x{response[1]:X2}.");

        byte count = response[7];
        return response[8..(8 + count)];
    }

    public ReportSlaveIdData ParseReportSlaveId(byte[] response)
    {
        ValidateCrcAndErrors(response);
        // ADU: SlaveAddr(1) + FC(1) + ByteCount(1) + Data(ByteCount bytes) + CRC(2)
        int byteCount = response[2];
        var rawData = response[3..(3 + byteCount)];

        byte runIndicator = rawData.Length >= 2 ? rawData[1] : (byte)0x00;

        return new ReportSlaveIdData
        {
            RawData = rawData,
            RunIndicatorStatus = runIndicator
        };
    }
}
