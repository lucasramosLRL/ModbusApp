using Modbus.Core.Protocol.Enums;

namespace Modbus.Core.Protocol.Framing;

public interface IModbusFrameBuilder
{
    /// <summary>Builds a read request frame for FC03 (holding) or FC04 (input) registers.</summary>
    byte[] ReadRegisters(byte slaveId, FunctionCode functionCode, ushort startAddress, ushort quantity);

    byte[] WriteSingleRegister(byte slaveId, ushort address, ushort value);

    byte[] WriteMultipleRegisters(byte slaveId, ushort startAddress, ushort[] values);

    /// <summary>Builds an FC17 Report Slave ID request frame.</summary>
    byte[] ReportSlaveId(byte slaveId);
}
