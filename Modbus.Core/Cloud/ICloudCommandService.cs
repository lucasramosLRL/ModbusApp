using Modbus.Core.Domain.Entities;
using Modbus.Core.Domain.Enums;

namespace Modbus.Core.Cloud;

/// <summary>
/// Request/response (RPC) channel over MQTT: publishes a JSON command to a device's command topic
/// and awaits the correlated reply. Used for on-demand reads and configuration writes on cloud devices.
/// Operations the firmware does not support surface as <see cref="NotSupportedException"/>.
/// </summary>
public interface ICloudCommandService
{
    Task<ushort[]> ReadRegistersAsync(
        ModbusDevice device, RegisterType registerType, ushort startAddress, ushort quantity,
        CancellationToken cancellationToken = default);

    Task WriteRegistersAsync(
        ModbusDevice device, ushort startAddress, ushort[] values, CancellationToken cancellationToken = default);

    Task WriteCoilAsync(
        ModbusDevice device, ushort address, bool value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a "999-999" named-config command (e.g. <c>{"TP":"1.00","IA":"1","G1":"30003"}</c>).
    /// Fire-and-forget: TL/IA/grandeza changes reboot the meter and may not reply, so no response is awaited.
    /// </summary>
    Task SendConfigAsync(
        ModbusDevice device, IReadOnlyDictionary<string, string> fields, CancellationToken cancellationToken = default);
}
