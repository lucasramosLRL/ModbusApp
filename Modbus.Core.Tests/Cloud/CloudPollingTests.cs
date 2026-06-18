using FluentAssertions;
using Modbus.Core.Cloud;
using Modbus.Core.Domain.Entities;
using Modbus.Core.Domain.Enums;
using Modbus.Core.Domain.ValueObjects;
using Modbus.Core.Polling;
using Modbus.Core.Services;
using NSubstitute;

namespace Modbus.Core.Tests.Cloud;

public class CloudPollingTests
{
    [Fact]
    public async Task CloudDevice_TelemetryMessage_RaisesRegisterValuesUpdatedWithMappedValues()
    {
        var broker  = new ControllableBroker();
        var mapper  = new JsonTelemetryPayloadMapper();
        var factory = Substitute.For<IModbusServiceFactory>();
        var engine  = new PollingEngine(factory, TimeSpan.FromSeconds(5), broker, mapper);

        var device = new ModbusDevice
        {
            Id            = 11,
            Name          = "Field Meter",
            SlaveId       = 1,
            TransportType = TransportType.MqttCloud,
            SerialNumber  = 555,
            Mqtt          = new MqttConfig { BrokerHost = "broker.test" },
            DeviceModel   = new DeviceModel
            {
                Id = 1, Name = "KS-3000",
                Registers =
                [
                    new RegisterDefinition { Name = "U0", Address = 2, RegisterType = RegisterType.Input, DataType = DataType.Float32 }
                ]
            }
        };

        RegisterValuesUpdatedEventArgs? captured = null;
        engine.RegisterValuesUpdated += (_, e) => captured = e;

        engine.AddDevice(device);
        await broker.WaitForSubscriptionAsync();

        await broker.DeliverAsync("ks-01/0000555/data",
            """[{ "variable":"data", "time":"2026-04-06 14:45:00", "metadata":{ "U0":219.9 } }]""");

        captured.Should().NotBeNull();
        captured!.Device.Id.Should().Be(11);
        captured.Values.Should().ContainSingle(v => v.Address == 2 && v.Value == 219.9);

        // No Modbus service should be created for a cloud device (no polling path).
        factory.DidNotReceive().Create(Arg.Any<ModbusDevice>());

        await engine.StopAsync();
    }

    /// <summary>Broker fake that records topic handlers and lets the test push messages.</summary>
    private sealed class ControllableBroker : IMqttBrokerClient
    {
        private readonly Dictionary<string, Func<string, Task>> _handlers = new();
        private readonly TaskCompletionSource _subscribed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task WaitForSubscriptionAsync() => _subscribed.Task;

        public Task<IAsyncDisposable> SubscribeAsync(
            MqttConfig config, string topic, Func<string, Task> onMessage, CancellationToken cancellationToken = default)
        {
            lock (_handlers) _handlers[topic] = onMessage;
            _subscribed.TrySetResult();
            return Task.FromResult<IAsyncDisposable>(new NoopDisposable());
        }

        public Task PublishAsync(MqttConfig config, string topic, string payload, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public async Task DeliverAsync(string topic, string payload)
        {
            Func<string, Task>? handler;
            lock (_handlers) _handlers.TryGetValue(topic, out handler);
            if (handler is not null) await handler(payload);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private sealed class NoopDisposable : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
