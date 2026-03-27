using Modbus.Core.Protocol.Models;

namespace Modbus.Core.Protocol.Framing;

public interface IModbusFrameParser
{
    /// <summary>
    /// Parses a read registers response (FC03/FC04).
    /// Throws <see cref="Modbus.Core.Protocol.Exceptions.ModbusProtocolException"/> on Modbus error responses.
    /// </summary>
    ushort[] ParseReadRegisters(byte[] response);

    void ValidateWriteSingleRegister(byte[] response);

    void ValidateWriteMultipleRegisters(byte[] response);

    ReportSlaveIdData ParseReportSlaveId(byte[] response);
}
