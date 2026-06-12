using System.Buffers.Binary;
using Modbus.Core.Protocol.Enums;
using Modbus.Core.Protocol.Exceptions;
using Modbus.Core.Protocol.Framing;
using Modbus.Core.Protocol.Models;

namespace Modbus.Core.Protocol.Tcp;

public class ModbusTcpFrameParser : IModbusFrameParser
{
    // MBAP header occupies the first 7 bytes; PDU starts at index 7.
    private const int MbapLength = 7;

    /// <summary>
    /// Validates minimum frame length and checks for a Modbus error response
    /// (function code with bit 7 set). Throws on either condition.
    /// </summary>
    private static void CheckForError(byte[] response)
    {
        if (response.Length < MbapLength + 2)
            throw new InvalidDataException(
                $"TCP response too short: {response.Length} bytes (minimum {MbapLength + 2}).");

        byte fc = response[MbapLength];
        if ((fc & 0x80) != 0)
            throw new ModbusProtocolException(
                (FunctionCode)(fc & 0x7F),
                (ModbusExceptionCode)response[MbapLength + 1]);
    }

    public ushort[] ParseReadRegisters(byte[] response)
    {
        CheckForError(response);
        // PDU: FC(1) + ByteCount(1) + Data(N*2)
        int byteCount = response[MbapLength + 1];
        var registers = new ushort[byteCount / 2];
        for (int i = 0; i < registers.Length; i++)
            registers[i] = BinaryPrimitives.ReadUInt16BigEndian(
                response.AsSpan(MbapLength + 2 + i * 2));
        return registers;
    }

    public void ValidateWriteSingleCoil(byte[] response) =>
        CheckForError(response);

    public void ValidateWriteSingleRegister(byte[] response) =>
        CheckForError(response);

    public void ValidateWriteMultipleRegisters(byte[] response) =>
        CheckForError(response);

    /// <summary>
    /// Parses an FC 0x07 (ReadExceptionStatus) TCP response.
    /// PDU: FC(1) + StatusByte(1). Total with MBAP = 9 bytes.
    /// Bit 7 of StatusByte (0x80) = memory module fault (non-fatal for reading).
    /// </summary>
    public static byte ParseReadExceptionStatus(byte[] response)
    {
        CheckForError(response);
        if (response.Length < MbapLength + 2)
            throw new InvalidDataException(
                $"FC 0x07 TCP response too short: {response.Length} bytes (expected {MbapLength + 2}).");
        if (response[MbapLength] != 0x07)
            throw new InvalidDataException(
                $"FC 0x07 TCP response has unexpected function code: 0x{response[MbapLength]:X2}.");
        return response[MbapLength + 1];
    }

    /// <summary>
    /// Parses an FC 0x14 (ReadFileRecord) TCP response.
    /// PDU: FC(1) + RDL(1) + FRL(1) + RT=0x06(1) + Data(QTD×2).
    /// Returns the raw data bytes (QTD×2 bytes after RT=0x06).
    /// </summary>
    public static byte[] ParseReadFileRecord(byte[] response)
    {
        CheckForError(response);
        // Minimum: MBAP(7) + FC + RDL + FRL + RT = 11 bytes
        if (response.Length < MbapLength + 4)
            throw new InvalidDataException(
                $"FC 0x14 TCP response too short: {response.Length} bytes (minimum {MbapLength + 4}).");
        if (response[MbapLength] != 0x14)
            throw new InvalidDataException(
                $"FC 0x14 TCP response has unexpected function code: 0x{response[MbapLength]:X2}.");
        // RDL at MbapLength+1; data starts at MbapLength+4 (after FC, RDL, FRL, RT)
        // RDL = FRL(1) + RT(1) + data(QTD*2) = QTD*2+2, so actual data = RDL-2.
        int rdl = response[MbapLength + 1];
        int dataLength = rdl - 2;
        int dataStart = MbapLength + 4;
        if (response.Length < dataStart + dataLength)
            throw new InvalidDataException(
                $"FC 0x14 TCP response truncated: {response.Length} bytes, expected {dataStart + dataLength}.");
        return response[dataStart..(dataStart + dataLength)];
    }

    public ReportSlaveIdData ParseReportSlaveId(byte[] response)
    {
        CheckForError(response);
        // PDU: FC(1) + ByteCount(1) + Data(ByteCount bytes)
        int byteCount = response[MbapLength + 1];
        var rawData = response[(MbapLength + 2)..(MbapLength + 2 + byteCount)];

        // Conventional layout: [0] = echoed slave ID, [1] = run indicator
        byte runIndicator = rawData.Length >= 2 ? rawData[1] : (byte)0x00;

        return new ReportSlaveIdData
        {
            RawData = rawData,
            RunIndicatorStatus = runIndicator
        };
    }
}
