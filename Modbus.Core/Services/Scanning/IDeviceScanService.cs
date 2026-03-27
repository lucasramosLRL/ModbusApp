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
        int port,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
