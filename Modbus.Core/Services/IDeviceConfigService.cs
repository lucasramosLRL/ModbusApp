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

/// <summary>
/// Result of a <see cref="IDeviceConfigService.WriteBatchAsync"/> call.
/// <para>
/// When the KS-3000 reboots in the middle of a batch (typical after writing Wi-Fi / Ethernet
/// config), the IOException raised by the next op is treated as a graceful "device went away
/// for reboot" signal. Already-applied ops are counted in <see cref="Completed"/>; ops that
/// did not run land in <see cref="Remaining"/> so the caller can wait for the device to come
/// back and resume the batch.
/// </para>
/// </summary>
public sealed record WriteBatchResult(
    int Completed,
    IReadOnlyList<RegisterWrite> Remaining,
    bool DeviceRebooted,
    bool CoilResetSent);

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
    /// Sends the FC05 "commit/reset" coil (wire address 5) that KS-3000 / Konect 120 require
    /// after a configuration save; the meter reboots on receiving it. Idempotent — safe to call
    /// once per Save regardless of how many fields were modified.
    /// </summary>
    Task SendCoilResetAsync(ModbusDevice device, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a batch of writes against a single persistent connection (and, optionally,
    /// the commit coil at the end). Greatly faster than calling WriteAsync/WriteMultipleRegistersAsync
    /// in a loop because each of those reopens the TCP socket from scratch.
    /// <para>
    /// When <paramref name="iotBufferResetCoil"/> is set AND <paramref name="sendCoilResetAfter"/>
    /// is true, that coil (the IoT buffer / mass-memory reset) is pulsed first, followed by a
    /// settle delay, and only then the commit/reset coil — required after IoT grandezas / send
    /// interval changes so the meter doesn't fall into a mass-memory self-test.
    /// </para>
    /// </summary>
    Task<WriteBatchResult> WriteBatchAsync(
        ModbusDevice device,
        IReadOnlyList<RegisterWrite> writes,
        bool sendCoilResetAfter,
        ushort? iotBufferResetCoil = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Polls the device until a small probe read succeeds or <paramref name="maxWaitSeconds"/>
    /// elapses. Used after a write batch when the KS-3000 reboots (~23s boot + Wi-Fi rejoin)
    /// so the post-save re-read doesn't fire while the device is still offline.
    /// Returns true when the device responds, false on timeout.
    /// </summary>
    Task<bool> WaitForDeviceReachableAsync(
        ModbusDevice device,
        int maxWaitSeconds = 60,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends KRON FC 0x42 (configAddress) as a broadcast frame containing the device serial
    /// number and the new RTU slave address. The device applies the address and reboots (~20s);
    /// no response is returned. <paramref name="device"/> must have a valid
    /// <see cref="ModbusDevice.SerialNumber"/> and RTU config.
    /// </summary>
    Task WriteSlaveAddressAsync(
        ModbusDevice device,
        byte newSlaveId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the InOutCfg calibration byte from the device using KRON FC 0x79 (ReadConfigDisp).
    /// Returns a bitmask where bit 0=EDP1, 1=EDP2, 2=EDP3, 3=SD1, 4=SD2.
    /// Returns <c>null</c> for TCP devices (FC 0x79 is RTU-only) or if the read fails —
    /// callers must fall back to showing all I/O channels when null.
    /// </summary>
    Task<ushort?> ReadInOutCfgAsync(ModbusDevice device, CancellationToken cancellationToken = default);
}
