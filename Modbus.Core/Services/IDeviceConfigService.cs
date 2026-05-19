using Modbus.Core.Domain.Entities;

namespace Modbus.Core.Services;

/// <summary>
/// Result of a bulk configuration read. <see cref="Values"/> always contains whatever
/// blocks succeeded; <see cref="FailedBlocks"/> lists any ranges that exhausted all
/// retry attempts. Callers must check <c>FailedBlocks.Count == 0</c> before treating
/// the read as complete — partial data must not be written back to the device.
/// </summary>
public sealed record ConfigReadResult(
    IReadOnlyDictionary<ushort, ushort> Values,
    IReadOnlyList<string> FailedBlocks);

public interface IDeviceConfigService
{
    /// <summary>
    /// Opens a temporary connection, reads each <see cref="RegisterField"/> as an atomic
    /// block (fields sharing the same starting address are merged; fields at different
    /// addresses are NOT coalesced even when physically adjacent, because some devices
    /// reject multi-field reads with IllegalDataValue). Multi-word fields longer than
    /// 32 registers are split into 32-word chunks. Each block is retried on transient
    /// (timeout) failures before being marked failed.
    ///
    /// Returns a map of Modicon number → raw value plus a list of any failed blocks.
    /// Callers must check <c>FailedBlocks.Count == 0</c> before writing data back.
    /// </summary>
    Task<ConfigReadResult> ReadAsync(
        ModbusDevice device,
        IEnumerable<RegisterField> fields,
        CancellationToken cancellationToken = default);

    /// <summary>Opens a temporary connection and writes a single holding register (FC06).</summary>
    Task WriteAsync(
        ModbusDevice device,
        ushort address,
        ushort value,
        CancellationToken cancellationToken = default);
}
