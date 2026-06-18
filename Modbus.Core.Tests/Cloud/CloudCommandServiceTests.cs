using System.Text.Json;
using FluentAssertions;
using Modbus.Core.Cloud;
using Modbus.Core.Domain.Entities;
using Modbus.Core.Domain.Enums;
using Modbus.Core.Domain.ValueObjects;

namespace Modbus.Core.Tests.Cloud;

public class CloudCommandServiceTests
{
    private static ModbusDevice MakeDevice() => new()
    {
        Id            = 9,
        Name          = "KS Meter",
        SlaveId       = 1,
        TransportType = TransportType.MqttCloud,
        SerialNumber  = 42,
        Mqtt          = new MqttConfig { BrokerHost = "broker.test" } // default KS topics
    };

    [Fact]
    public async Task ReadHolding_PublishesHrrEnvelope_AndDecodesHexResponse()
    {
        var broker = new FakeBroker(_ => """{ "HRR": "0001000200030004000500060007" }""");
        var sut = new CloudCommandService(broker);

        var result = await sut.ReadRegistersAsync(MakeDevice(), RegisterType.Holding, startAddress: 0, quantity: 7);

        result.Should().Equal(1, 2, 3, 4, 5, 6, 7);
        broker.LastPublishedTopic.Should().Be("ks-01/0000042/reply");

        // Envelope: { "999-123": { "id":"…", "HRR": ["40001","7"] } }
        var inner = JsonDocument.Parse(broker.LastPublishedPayload!).RootElement.GetProperty("999-123");
        inner.GetProperty("HRR")[0].GetString().Should().Be("40001");
        inner.GetProperty("HRR")[1].GetString().Should().Be("7");
        inner.GetProperty("id").GetString().Should().HaveLength(6);
    }

    [Fact]
    public async Task ReadInput_IsNotSupported()
    {
        var sut = new CloudCommandService(new FakeBroker(_ => null));
        var act = () => sut.ReadRegistersAsync(MakeDevice(), RegisterType.Input, 0, 4);
        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public async Task WriteHolding_PublishesHrwEnvelope_WithHexValues_AndAcceptsSuccess()
    {
        var broker = new FakeBroker(_ => """{ "Message": "HRW Success", "HR": "42101", "Size": "2" }""");
        var sut = new CloudCommandService(broker);

        await sut.WriteRegistersAsync(MakeDevice(), startAddress: 2100, values: [5, 2]);

        var inner = JsonDocument.Parse(broker.LastPublishedPayload!).RootElement.GetProperty("999-123");
        inner.GetProperty("HRW")[0].GetString().Should().Be("42101");   // 2100 + 40001
        inner.GetProperty("HRW")[1].GetString().Should().Be("00050002");
    }

    [Fact]
    public async Task WriteHolding_ThrowsWhenNotConfirmed()
    {
        var broker = new FakeBroker(_ => """{ "Message": "HRW Fail" }""");
        var sut = new CloudCommandService(broker);

        var act = () => sut.WriteRegistersAsync(MakeDevice(), 2100, [1]);
        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public async Task Read_TimesOutWhenNoResponse()
    {
        var broker = new FakeBroker(_ => null);
        var sut = new CloudCommandService(broker, timeout: TimeSpan.FromMilliseconds(150));

        var act = () => sut.ReadRegistersAsync(MakeDevice(), RegisterType.Holding, 0, 1);
        await act.Should().ThrowAsync<TimeoutException>();
    }

    [Fact]
    public async Task WriteCoil_PublishesCoilCommand_AndIsFireAndForget()
    {
        var broker = new FakeBroker(_ => null); // never replies — must not hang
        var sut = new CloudCommandService(broker);

        // Wire address 5 → KS COIL "006" (1-based).
        await sut.WriteCoilAsync(MakeDevice(), address: 5, value: true);

        var inner = JsonDocument.Parse(broker.LastPublishedPayload!).RootElement.GetProperty("999-999");
        inner.GetProperty("COIL").GetString().Should().Be("006");
    }

    [Fact]
    public async Task SendConfig_PublishesNamedFieldsUnder999_999_FireAndForget()
    {
        var broker = new FakeBroker(_ => null); // no reply — must not hang
        var sut = new CloudCommandService(broker);

        await sut.SendConfigAsync(MakeDevice(), new Dictionary<string, string>
        {
            ["TC"] = "100.00",
            ["IA"] = "1"
        });

        broker.LastPublishedTopic.Should().Be("ks-01/0000042/reply");
        var inner = JsonDocument.Parse(broker.LastPublishedPayload!).RootElement.GetProperty("999-999");
        inner.GetProperty("TC").GetString().Should().Be("100.00");
        inner.GetProperty("IA").GetString().Should().Be("1");
        inner.GetProperty("id").GetString().Should().HaveLength(6);
    }

    [Fact]
    public void HexCodec_RoundTrips()
    {
        var words = new ushort[] { 0x0005, 0x7A44, 0x0000, 0x3028 };
        var hex   = CloudCommandService.EncodeRegistersHex(words);
        hex.Should().Be("00057A4400003028");
        CloudCommandService.DecodeRegistersHex(hex).Should().Equal(words);
    }

    /// <summary>In-memory broker that records publishes and delivers a reply on the subscribed topic.</summary>
    private sealed class FakeBroker(Func<string, string?> replyFactory) : IMqttBrokerClient
    {
        private Func<string, Task>? _handler;
        public string? LastPublishedTopic { get; private set; }
        public string? LastPublishedPayload { get; private set; }

        public Task<IAsyncDisposable> SubscribeAsync(
            MqttConfig config, string topic, Func<string, Task> onMessage, CancellationToken cancellationToken = default)
        {
            _handler = onMessage;
            return Task.FromResult<IAsyncDisposable>(new NoopDisposable());
        }

        public async Task PublishAsync(
            MqttConfig config, string topic, string payload, CancellationToken cancellationToken = default)
        {
            LastPublishedTopic   = topic;
            LastPublishedPayload = payload;
            var reply = replyFactory(payload);
            if (reply is not null && _handler is not null)
                await _handler(reply);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private sealed class NoopDisposable : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
