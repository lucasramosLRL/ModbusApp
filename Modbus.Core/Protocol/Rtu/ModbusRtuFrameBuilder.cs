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

    public byte[] WriteSingleCoil(byte slaveId, ushort address, bool value) =>
        BuildFixedRequest(slaveId, (byte)FunctionCode.WriteSingleCoil, address, value ? (ushort)0xFF00 : (ushort)0x0000);

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

    /// <summary>
    /// Builds a KRON FC 0x79 (ReadConfigDisp) broadcast frame.
    /// ADU: 0x00(broadcast) + 0x79 + SerialNumber(4 BE) + StartReg(1) + Count(1) + CRC(2).
    /// Device responds with its slave address, echoes the SN, and returns Count data bytes.
    /// </summary>
    public byte[] ReadConfigDisp(uint serialNumber, byte startReg, byte count)
    {
        var frame = new byte[10];
        frame[0] = 0x00; // broadcast
        frame[1] = 0x79;
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(2), serialNumber);
        frame[6] = startReg;
        frame[7] = count;
        Crc16.Append(frame, 8);
        return frame;
    }

    /// <summary>
    /// Builds an FC 0x07 (ReadExceptionStatus) RTU frame.
    /// ADU: SlaveAddr(1) + 0x07(1) + CRC(2) = 4 bytes.
    /// </summary>
    public byte[] ReadExceptionStatus(byte slaveId)
    {
        var frame = new byte[4];
        frame[0] = slaveId;
        frame[1] = 0x07;
        Crc16.Append(frame, 2);
        return frame;
    }

    /// <summary>
    /// Builds an FC 0x14 (ReadFileRecord) RTU frame for KRON mass memory blocks.
    /// ADU: SlaveAddr(1) + 0x14(1) + BC=0x07(1) + RT=0x06(1) + SET(2) + BLC(2) + QTD(2) + CRC(2) = 12 bytes.
    /// QTD = 3 + 2*GP where GP = number of grandezas programmed.
    /// </summary>
    public byte[] ReadFileRecord(byte slaveId, ushort sector, ushort block, ushort qtd)
    {
        var frame = new byte[12];
        frame[0] = slaveId;
        frame[1] = 0x14;
        frame[2] = 0x07; // byte count of request sub-data
        frame[3] = 0x06; // reference type
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(4), sector);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(6), block);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(8), qtd);
        Crc16.Append(frame, 10);
        return frame;
    }

    /// <summary>
    /// Builds a KRON FC 0x42 (configAddress) broadcast frame.
    /// ADU: 0x00(broadcast) + 0x42 + SerialNumber(4 BE) + NewSlaveId(1) + CRC(2).
    /// No response is expected — the device reboots after applying the new address.
    /// </summary>
    public byte[] ConfigureAddress(uint serialNumber, byte newSlaveId)
    {
        var frame = new byte[9];
        frame[0] = 0x00; // broadcast
        frame[1] = 0x42;
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(2), serialNumber);
        frame[6] = newSlaveId;
        Crc16.Append(frame, 7);
        return frame;
    }
}
