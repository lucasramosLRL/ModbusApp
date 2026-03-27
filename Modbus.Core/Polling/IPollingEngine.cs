using Modbus.Core.Domain.Entities;

namespace Modbus.Core.Polling;

public interface IPollingEngine : IAsyncDisposable
{
    event EventHandler<RegisterValuesUpdatedEventArgs>? RegisterValuesUpdated;
    event EventHandler<DeviceConnectionFailedEventArgs>? DeviceConnectionFailed;

    void AddDevice(ModbusDevice device);
    void RemoveDevice(int deviceId);

    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();
}
