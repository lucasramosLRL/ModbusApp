using Modbus.Core.Domain.ValueObjects;

namespace Modbus.Core.Services.Scanning;

public interface IDeviceScanService
{
    IAsyncEnumerable<DeviceScanResult> ScanRtuAsync(
        RtuConfig rtuConfig,
        byte startAddress,
        byte endAddress,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<DeviceScanResult> ScanTcpAsync(
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Connects to a single TCP device and reads its identity (ReportSlaveId → device code +
    /// firmware version, plus the NS serial register). Used at save-time so the discovery scan
    /// itself stays lightweight (UDP broadcast only). Returns <c>null</c> if the device can't be
    /// reached or doesn't answer.
    /// </summary>
    Task<DeviceScanResult?> ProbeTcpDeviceAsync(
        string ipAddress,
        int port = 502,
        CancellationToken cancellationToken = default);
}
