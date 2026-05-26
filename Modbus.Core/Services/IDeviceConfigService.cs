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

/// <summary>
/// One write operation in a batch. <see cref="Values"/> with length 1 is dispatched as FC06
/// (single register); longer arrays go through FC16 with automatic 22-register chunking.
/// </summary>
public sealed record RegisterWrite(ushort ModiconAddress, ushort[] Values);

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

    /// <summary>Opens a temporary connection and writes a single coil (FC05).</summary>
    Task WriteCoilAsync(
        ModbusDevice device,
        ushort coilAddress,
        bool value,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a temporary connection and writes contiguous holding registers (FC16),
    /// splitting into 22-register chunks so it fits the KS-3000/Konect 120 FC16 limit.
    /// Address must be a 4xxxx Modicon number.
    /// </summary>
    Task WriteMultipleRegistersAsync(
        ModbusDevice device,
        ushort modiconAddress,
        ushort[] values,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the FC05 "commit/reset" coil (address 6) that KS-3000 / Konect 120 require
    /// after writing string fields in the 43461+ range. Idempotent — safe to call once
    /// per Save regardless of how many strings were modified.
    /// </summary>
    Task SendCoilResetAsync(ModbusDevice device, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a batch of writes against a single persistent connection (and, optionally,
    /// the commit coil at the end). Greatly faster than calling WriteAsync/WriteMultipleRegistersAsync
    /// in a loop because each of those reopens the TCP socket from scratch.
    /// </summary>
    Task WriteBatchAsync(
        ModbusDevice device,
        IReadOnlyList<RegisterWrite> writes,
        bool sendCoilResetAfter,
        CancellationToken cancellationToken = default);
}
