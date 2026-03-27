namespace Modbus.Core.Protocol.Enums;

public enum FunctionCode : byte
{
    ReadHoldingRegisters = 0x03,
    ReadInputRegisters   = 0x04,
    WriteSingleRegister  = 0x06,
    WriteMultipleRegisters = 0x10,
    ReportSlaveId        = 0x11
}
