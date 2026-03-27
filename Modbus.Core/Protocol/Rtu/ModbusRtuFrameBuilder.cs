using System.Buffers.Binary;
using Modbus.Core.Protocol.Enums;
using Modbus.Core.Protocol.Framing;

namespace Modbus.Core.Protocol.Rtu;

public class ModbusRtuFrameBuilder : IModbusFrameBuilder
{
    /// <summary>
    /// Builds an 8-byte RTU ADU for requests with a 4-byte PDU (FC + addr + qty/value).
    /// ADU: SlaveAddr(1) + FC(1) + Param1(2) + Param2(2) + CRC(2).
    /// </summary>
    private static byte[] BuildFixedRequest(byte slaveId, byte fc, ushort param1, ushort param2)
    {
        var frame = new byte[8];
        frame[0] = slaveId;
        frame[1] = fc;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(2), param1);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(4), param2);
        Crc16.Append(frame, 6);
        return frame;
    }

    public byte[] ReadRegisters(byte slaveId, FunctionCode functionCode, ushort startAddress, ushort quantity) =>
        BuildFixedRequest(slaveId, (byte)functionCode, startAddress, quantity);

    public byte[] WriteSingleRegister(byte slaveId, ushort address, ushort value) =>
        BuildFixedRequest(slaveId, (byte)FunctionCode.WriteSingleRegister, address, value);

    public byte[] WriteMultipleRegisters(byte slaveId, ushort startAddress, ushort[] values)
    {
        // ADU: SlaveAddr(1) + FC(1) + StartAddr(2) + Quantity(2) + ByteCount(1) + Data(N*2) + CRC(2)
        int messageLength = 7 + values.Length * 2;
        var frame = new byte[messageLength + 2];
        frame[0] = slaveId;
        frame[1] = (byte)FunctionCode.WriteMultipleRegisters;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(2), startAddress);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(4), (ushort)values.Length);
        frame[6] = (byte)(values.Length * 2);
        for (int i = 0; i < values.Length; i++)
            BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(7 + i * 2), values[i]);
        Crc16.Append(frame, messageLength);
        return frame;
    }

    public byte[] ReportSlaveId(byte slaveId)
    {
        // ADU: SlaveAddr(1) + FC(1) + CRC(2)
        var frame = new byte[4];
        frame[0] = slaveId;
        frame[1] = (byte)FunctionCode.ReportSlaveId;
        Crc16.Append(frame, 2);
        return frame;
    }
}
