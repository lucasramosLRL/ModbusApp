using Modbus.Core.Domain.Entities;

namespace Modbus.Core.Services;

public interface IDeviceConfigService
{
    /// <summary>
    /// Opens a temporary connection, reads the given Modicon-style register numbers
    /// (4xxxx → FC03 holding, 3xxxx → FC04 input), groups them into contiguous blocks,
    /// and returns a map of Modicon number → raw value.
    /// Multi-word fields are included automatically; callers look up consecutive Modicon
    /// numbers from the returned dictionary.
    /// </summary>
    Task<IReadOnlyDictionary<ushort, ushort>> ReadAsync(
        ModbusDevice device,
        IEnumerable<ushort> addresses,
        CancellationToken cancellationToken = default);

    /// <summary>Opens a temporary connection and writes a single holding register (FC06).</summary>
    Task WriteAsync(
        ModbusDevice device,
        ushort address,
        ushort value,
        CancellationToken cancellationToken = default);
}
