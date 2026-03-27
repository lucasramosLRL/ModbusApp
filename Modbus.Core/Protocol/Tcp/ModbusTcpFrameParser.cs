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

    public void ValidateWriteSingleRegister(byte[] response) =>
        CheckForError(response);

    public void ValidateWriteMultipleRegisters(byte[] response) =>
        CheckForError(response);

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
