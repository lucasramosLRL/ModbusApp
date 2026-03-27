using Modbus.Core.Protocol.Enums;
using Modbus.Core.Protocol.Framing;
using Modbus.Core.Protocol.Models;
using Modbus.Core.Transport;

namespace Modbus.Core.Services;

public class ModbusService : IModbusService
{
    private readonly IModbusTransport _transport;
    private readonly IModbusFrameBuilder _builder;
    private readonly IModbusFrameParser _parser;

    public ModbusService(IModbusTransport transport, IModbusFrameBuilder builder, IModbusFrameParser parser)
    {
        _transport = transport;
        _builder   = builder;
        _parser    = parser;
    }

    public bool IsConnected => _transport.IsConnected;

    public Task ConnectAsync(CancellationToken cancellationToken = default) =>
        _transport.ConnectAsync(cancellationToken);

    public Task DisconnectAsync() =>
        _transport.DisconnectAsync();

    public async Task<ushort[]> ReadHoldingRegistersAsync(byte slaveId, ushort startAddress, ushort quantity, CancellationToken cancellationToken = default)
    {
        var request  = _builder.ReadRegisters(slaveId, FunctionCode.ReadHoldingRegisters, startAddress, quantity);
        var response = await _transport.SendAsync(request, RtuReadLength(quantity), cancellationToken);
        return _parser.ParseReadRegisters(response);
    }

    public async Task<ushort[]> ReadInputRegistersAsync(byte slaveId, ushort startAddress, ushort quantity, CancellationToken cancellationToken = default)
    {
        var request  = _builder.ReadRegisters(slaveId, FunctionCode.ReadInputRegisters, startAddress, quantity);
        var response = await _transport.SendAsync(request, RtuReadLength(quantity), cancellationToken);
        return _parser.ParseReadRegisters(response);
    }

    public async Task WriteSingleRegisterAsync(byte slaveId, ushort address, ushort value, CancellationToken cancellationToken = default)
    {
        // RTU response: SlaveAddr(1) + FC(1) + Addr(2) + Value(2) + CRC(2) = 8
        var request  = _builder.WriteSingleRegister(slaveId, address, value);
        var response = await _transport.SendAsync(request, 8, cancellationToken);
        _parser.ValidateWriteSingleRegister(response);
    }

    public async Task WriteMultipleRegistersAsync(byte slaveId, ushort startAddress, ushort[] values, CancellationToken cancellationToken = default)
    {
        // RTU response: SlaveAddr(1) + FC(1) + StartAddr(2) + Quantity(2) + CRC(2) = 8
        var request  = _builder.WriteMultipleRegisters(slaveId, startAddress, values);
        var response = await _transport.SendAsync(request, 8, cancellationToken);
        _parser.ValidateWriteMultipleRegisters(response);
    }

    public async Task<ReportSlaveIdData> ReportSlaveIdAsync(byte slaveId, CancellationToken cancellationToken = default)
    {
        // Pass 0 so RTU uses timeout-based read (response length is variable)
        var request  = _builder.ReportSlaveId(slaveId);
        var response = await _transport.SendAsync(request, 0, cancellationToken);
        return _parser.ParseReportSlaveId(response);
    }

    /// <summary>
    /// Expected RTU response byte count for FC03/FC04.
    /// Formula: SlaveAddr(1) + FC(1) + ByteCount(1) + Data(quantity×2) + CRC(2).
    /// TCP transports ignore this value and read from the MBAP Length field instead.
    /// </summary>
    private static int RtuReadLength(ushort quantity) => 5 + quantity * 2;

    public void Dispose() => _transport.Dispose();
}
