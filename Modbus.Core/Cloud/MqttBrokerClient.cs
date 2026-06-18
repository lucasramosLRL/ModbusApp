using System.Collections.Concurrent;
using MQTTnet;
using MQTTnet.Protocol;
using Modbus.Core.Domain.ValueObjects;

namespace Modbus.Core.Cloud;

/// <summary>
/// MQTTnet-backed <see cref="IMqttBrokerClient"/>. Keeps one connection per distinct broker
/// (host/port/user) and multiplexes topic subscriptions and publishes over it.
/// </summary>
public sealed class MqttBrokerClient : IMqttBrokerClient
{
    private readonly MqttClientFactory _factory = new();
    private readonly ConcurrentDictionary<string, BrokerConnection> _connections = new();

    public async Task<IAsyncDisposable> SubscribeAsync(
        MqttConfig config, string topic, Func<string, Task> onMessage, CancellationToken cancellationToken = default)
    {
        var connection = await GetOrConnectAsync(config, cancellationToken);
        return await connection.SubscribeAsync(topic, onMessage, cancellationToken);
    }

    public async Task PublishAsync(
        MqttConfig config, string topic, string payload, CancellationToken cancellationToken = default)
    {
        var connection = await GetOrConnectAsync(config, cancellationToken);
        await connection.PublishAsync(topic, payload, cancellationToken);
    }

    private async Task<BrokerConnection> GetOrConnectAsync(MqttConfig config, CancellationToken cancellationToken)
    {
        var key = $"{config.BrokerHost}:{config.Port}:{config.Username}";
        var connection = _connections.GetOrAdd(key, _ => new BrokerConnection(_factory.CreateMqttClient(), config));
        await connection.EnsureConnectedAsync(cancellationToken);
        return connection;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var connection in _connections.Values)
            await connection.DisposeAsync();
        _connections.Clear();
    }

    /// <summary>A single broker connection with topic-keyed message routing.</summary>
    private sealed class BrokerConnection : IAsyncDisposable
    {
        private readonly IMqttClient _client;
        private readonly MqttConfig _config;
        private readonly SemaphoreSlim _connectLock = new(1, 1);
        private readonly ConcurrentDictionary<string, List<Func<string, Task>>> _handlers = new();

        public BrokerConnection(IMqttClient client, MqttConfig config)
        {
            _client = client;
            _config = config;
            _client.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
        }

        public async Task EnsureConnectedAsync(CancellationToken cancellationToken)
        {
            if (_client.IsConnected) return;

            await _connectLock.WaitAsync(cancellationToken);
            try
            {
                if (_client.IsConnected) return;

                var builder = new MqttClientOptionsBuilder()
                    .WithTcpServer(_config.BrokerHost, _config.Port)
                    .WithClientId(_config.ClientId ?? $"modbusapp-{Guid.NewGuid():N}")
                    .WithCleanSession();

                if (!string.IsNullOrEmpty(_config.Username))
                    builder = builder.WithCredentials(_config.Username, _config.Password);

                if (_config.UseTls)
                    builder = builder.WithTlsOptions(o => o.UseTls());

                await _client.ConnectAsync(builder.Build(), cancellationToken);

                // Re-subscribe any topics that were registered before a (re)connect.
                foreach (var topic in _handlers.Keys)
                    await _client.SubscribeAsync(topic, MqttQualityOfServiceLevel.AtLeastOnce, cancellationToken);
            }
            finally
            {
                _connectLock.Release();
            }
        }

        public async Task<IAsyncDisposable> SubscribeAsync(
            string topic, Func<string, Task> onMessage, CancellationToken cancellationToken)
        {
            var list = _handlers.GetOrAdd(topic, _ => new List<Func<string, Task>>());
            lock (list) list.Add(onMessage);

            if (_client.IsConnected)
                await _client.SubscribeAsync(topic, MqttQualityOfServiceLevel.AtLeastOnce, cancellationToken);

            return new Subscription(this, topic, onMessage);
        }

        public Task PublishAsync(string topic, string payload, CancellationToken cancellationToken)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            return _client.PublishAsync(message, cancellationToken);
        }

        private async Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
        {
            var topic = e.ApplicationMessage.Topic;
            if (!_handlers.TryGetValue(topic, out var list))
                return;

            var payload = e.ApplicationMessage.ConvertPayloadToString() ?? string.Empty;

            Func<string, Task>[] snapshot;
            lock (list) snapshot = list.ToArray();

            foreach (var handler in snapshot)
            {
                try { await handler(payload); }
                catch { /* one handler failing must not break the others */ }
            }
        }

        private async Task RemoveHandlerAsync(string topic, Func<string, Task> handler)
        {
            if (!_handlers.TryGetValue(topic, out var list))
                return;

            bool empty;
            lock (list)
            {
                list.Remove(handler);
                empty = list.Count == 0;
            }

            if (empty)
            {
                _handlers.TryRemove(topic, out _);
                if (_client.IsConnected)
                    try { await _client.UnsubscribeAsync(topic); } catch { }
            }
        }

        public async ValueTask DisposeAsync()
        {
            try { if (_client.IsConnected) await _client.DisconnectAsync(); } catch { }
            _client.Dispose();
            _connectLock.Dispose();
        }

        private sealed class Subscription(BrokerConnection owner, string topic, Func<string, Task> handler) : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => new(owner.RemoveHandlerAsync(topic, handler));
        }
    }
}
