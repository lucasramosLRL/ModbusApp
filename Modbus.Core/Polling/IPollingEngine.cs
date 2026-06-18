using Modbus.Core.Domain.Entities;

namespace Modbus.Core.Polling;

public interface IPollingEngine : IAsyncDisposable
{
    event EventHandler<RegisterValuesUpdatedEventArgs>? RegisterValuesUpdated;
    event EventHandler<DeviceConnectionFailedEventArgs>? DeviceConnectionFailed;

    /// <summary>Raised for each cloud telemetry message, carrying every published field (see <see cref="TelemetryReceivedEventArgs"/>).</summary>
    event EventHandler<TelemetryReceivedEventArgs>? TelemetryReceived;

    void AddDevice(ModbusDevice device);
    void RemoveDevice(int deviceId);

    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();

    Task SuspendRtuPollingAsync(CancellationToken cancellationToken = default);
    void ResumeRtuPolling();

    /// <summary>
    /// Acquires exclusive access to a device's poll slot and disconnects its transport,
    /// so callers (e.g. DeviceConfigService) can open a clean connection without conflict.
    /// Must be paired with <see cref="ReleaseDeviceLock"/>.
    /// </summary>
    Task AcquireDeviceLockAsync(int deviceId, CancellationToken cancellationToken = default);
    void ReleaseDeviceLock(int deviceId);
}
