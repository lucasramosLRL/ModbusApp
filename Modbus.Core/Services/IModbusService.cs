using Modbus.Core.Protocol.Models;

namespace Modbus.Core.Services;

public interface IModbusService : IDisposable
{
    bool IsConnected { get; }

    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync();

    Task<ushort[]> ReadHoldingRegistersAsync(byte slaveId, ushort startAddress, ushort quantity, CancellationToken cancellationToken = default);
    Task<ushort[]> ReadInputRegistersAsync(byte slaveId, ushort startAddress, ushort quantity, CancellationToken cancellationToken = default);

    Task WriteSingleRegisterAsync(byte slaveId, ushort address, ushort value, CancellationToken cancellationToken = default);
    Task WriteMultipleRegistersAsync(byte slaveId, ushort startAddress, ushort[] values, CancellationToken cancellationToken = default);

    /// <summary>FC17 — identifies the device model. Use to populate <see cref="Domain.Entities.ModbusDevice.DeviceModelId"/>.</summary>
    Task<ReportSlaveIdData> ReportSlaveIdAsync(byte slaveId, CancellationToken cancellationToken = default);
}
