using Modbus.Core.Protocol.Enums;

namespace Modbus.Core.Protocol.Exceptions;

public class ModbusProtocolException : Exception
{
    public FunctionCode FunctionCode { get; }
    public ModbusExceptionCode ExceptionCode { get; }

    public ModbusProtocolException(FunctionCode functionCode, ModbusExceptionCode exceptionCode)
        : base($"Modbus exception on FC 0x{functionCode:X2}: {exceptionCode} (0x{(byte)exceptionCode:X2})")
    {
        FunctionCode = functionCode;
        ExceptionCode = exceptionCode;
    }
}
