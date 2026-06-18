using Modbus.Core.Domain.Entities;
using Modbus.Core.Domain.Enums;
using Modbus.Core.Protocol.Models;
using Modbus.Core.Services;

namespace Modbus.Core.Cloud;

/// <summary>
/// <see cref="IModbusService"/> implementation for cloud devices. Read/write requests are tunnelled
/// to the field meter as JSON commands over MQTT (<see cref="ICloudCommandService"/>), so the existing
/// configuration and mass-memory screens work over the cloud without changes. Operations the firmware
/// does not expose surface as <see cref="NotSupportedException"/>.
/// </summary>
/// <remarks>
/// The <c>slaveId</c> arguments are ignored: a cloud device is addressed by its topic/serial, not a bus id.
/// Live readings are delivered through the telemetry pipeline (see PollingEngine), not this service.
/// </remarks>
public sealed class CloudModbusService : IModbusService
{
    private readonly ModbusDevice _device;
    private readonly ICloudCommandService _commands;

    public CloudModbusService(ModbusDevice device, ICloudCommandService commands)
    {
        _device   = device;
        _commands = commands;
    }

    public bool IsConnected { get; private set; }

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        IsConnected = true;
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        IsConnected = false;
        return Task.CompletedTask;
    }

    public Task<ushort[]> ReadHoldingRegistersAsync(byte slaveId, ushort startAddress, ushort quantity, CancellationToken cancellationToken = default) =>
        _commands.ReadRegistersAsync(_device, RegisterType.Holding, startAddress, quantity, cancellationToken);

    public Task<ushort[]> ReadInputRegistersAsync(byte slaveId, ushort startAddress, ushort quantity, CancellationToken cancellationToken = default) =>
        _commands.ReadRegistersAsync(_device, RegisterType.Input, startAddress, quantity, cancellationToken);

    public Task WriteSingleRegisterAsync(byte slaveId, ushort address, ushort value, CancellationToken cancellationToken = default) =>
        _commands.WriteRegistersAsync(_device, address, [value], cancellationToken);

    public Task WriteMultipleRegistersAsync(byte slaveId, ushort startAddress, ushort[] values, CancellationToken cancellationToken = default) =>
        _commands.WriteRegistersAsync(_device, startAddress, values, cancellationToken);

    public Task WriteSingleCoilAsync(byte slaveId, ushort address, bool value, CancellationToken cancellationToken = default) =>
        _commands.WriteCoilAsync(_device, address, value, cancellationToken);

    public Task<ReportSlaveIdData> ReportSlaveIdAsync(byte slaveId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("FC17 Report Slave ID is not available over the cloud; the device model is known at registration time.");

    public void Dispose() { }
}
