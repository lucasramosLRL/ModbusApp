using Modbus.Core.Domain.ValueObjects;

namespace Modbus.Core.Cloud;

/// <summary>
/// Manages connections to cloud MQTT brokers, shared across all devices that use the same broker.
/// Hides the underlying MQTT library from the rest of the app.
/// </summary>
public interface IMqttBrokerClient : IAsyncDisposable
{
    /// <summary>
    /// Subscribes to <paramref name="topic"/> on the broker described by <paramref name="config"/>,
    /// connecting if needed. <paramref name="onMessage"/> receives the UTF-8 payload of each matching
    /// message. Dispose the returned handle to remove the subscription.
    /// </summary>
    Task<IAsyncDisposable> SubscribeAsync(
        MqttConfig config, string topic, Func<string, Task> onMessage, CancellationToken cancellationToken = default);

    /// <summary>Publishes <paramref name="payload"/> to <paramref name="topic"/>, connecting if needed.</summary>
    Task PublishAsync(
        MqttConfig config, string topic, string payload, CancellationToken cancellationToken = default);
}
