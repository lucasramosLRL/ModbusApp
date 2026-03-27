using System.Buffers.Binary;
using Modbus.Core.Protocol.Enums;
using Modbus.Core.Protocol.Framing;

namespace Modbus.Core.Protocol.Tcp;

public class ModbusTcpFrameBuilder : IModbusFrameBuilder
{
    private int _transactionId;

    private ushort NextTransactionId() =>
        (ushort)(Interlocked.Increment(ref _transactionId) & 0xFFFF);

    /// <summary>
    /// Writes a 12-byte TCP ADU for requests with a 5-byte PDU (FC + addr + qty/value).
    /// MBAP: TxId(2) + ProtocolId(2) + Length(2) + UnitId(1) = 7 bytes.
    /// PDU:  FC(1) + StartAddr(2) + Param(2) = 5 bytes.
    /// </summary>
    private byte[] BuildFixedRequest(byte slaveId, byte fc, ushort param1, ushort param2)
    {
        var frame = new byte[12];
        BinaryPrimitives.WriteUInt16BigEndian(frame,              NextTransactionId());
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(2),   0x0000);  // Protocol ID
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(4),   0x0006);  // Length = UnitId + 5-byte PDU
        frame[6] = slaveId;
        frame[7] = fc;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(8),  param1);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(10), param2);
        return frame;
    }

    public byte[] ReadRegisters(byte slaveId, FunctionCode functionCode, ushort startAddress, ushort quantity) =>
        BuildFixedRequest(slaveId, (byte)functionCode, startAddress, quantity);

    public byte[] WriteSingleRegister(byte slaveId, ushort address, ushort value) =>
        BuildFixedRequest(slaveId, (byte)FunctionCode.WriteSingleRegister, address, value);

    public byte[] WriteMultipleRegisters(byte slaveId, ushort startAddress, ushort[] values)
    {
        // PDU: FC(1) + StartAddr(2) + Quantity(2) + ByteCount(1) + Data(N*2)
        int pduLength = 6 + values.Length * 2;
        var frame = new byte[7 + pduLength];
        BinaryPrimitives.WriteUInt16BigEndian(frame,             NextTransactionId());
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(2),  0x0000);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(4),  (ushort)(1 + pduLength));
        frame[6]  = slaveId;
        frame[7]  = (byte)FunctionCode.WriteMultipleRegisters;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(8),  startAddress);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(10), (ushort)values.Length);
        frame[12] = (byte)(values.Length * 2);
        for (int i = 0; i < values.Length; i++)
            BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(13 + i * 2), values[i]);
        return frame;
    }

    public byte[] ReportSlaveId(byte slaveId)
    {
        // PDU is just the function code byte — Length field = UnitId(1) + FC(1) = 2
        var frame = new byte[8];
        BinaryPrimitives.WriteUInt16BigEndian(frame,            NextTransactionId());
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(2), 0x0000);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(4), 0x0002);
        frame[6] = slaveId;
        frame[7] = (byte)FunctionCode.ReportSlaveId;
        return frame;
    }
}
