using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Modbus.Core.Domain.Entities;
using Modbus.Core.Domain.Enums;

namespace Modbus.Core.Cloud;

/// <summary>
/// Implements the KS MQTT command protocol over <see cref="IMqttBrokerClient"/>.
/// </summary>
/// <remarks>
/// Per the KS spec the app PUBLISHES commands to the meter's subscribe topic
/// (<see cref="Domain.ValueObjects.MqttConfig.CommandTopic"/>, named ".../reply"):
/// <list type="bullet">
/// <item>Holding read:  <c>{ "999-123": { "id":"…", "HRR": ["40001","7"] } }</c></item>
/// <item>Holding write: <c>{ "999-123": { "id":"…", "HRW": ["42101","0005…"] } }</c></item>
/// <item>Coil action:   <c>{ "999-999": { "id":"…", "COIL":"006" } }</c></item>
/// </list>
/// Register numbers are Modicon (holding = raw + 40001). Register values are 4 hex digits each.
/// Responses arrive UNwrapped on the data topic: <c>{ "HRR":"00007A44…" }</c> for reads and
/// <c>{ "Message":"HRW Success", … }</c> for writes. The meter does NOT echo the request <c>id</c>,
/// so responses are correlated by single-flight: one command per response topic at a time.
/// </remarks>
public sealed class CloudCommandService : ICloudCommandService, IAsyncDisposable
{
    private const int HoldingModiconBase = 40001;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    private readonly IMqttBrokerClient _broker;
    private readonly TimeSpan _timeout;
    private readonly ConcurrentDictionary<string, Task<ResponseChannel>> _channels = new();
    private int _idCounter;

    public CloudCommandService(IMqttBrokerClient broker, TimeSpan? timeout = null)
    {
        _broker  = broker;
        _timeout = timeout ?? DefaultTimeout;
    }

    public async Task<ushort[]> ReadRegistersAsync(
        ModbusDevice device, RegisterType registerType, ushort startAddress, ushort quantity,
        CancellationToken cancellationToken = default)
    {
        if (registerType != RegisterType.Holding)
            throw new NotSupportedException(
                "Only Holding Register reads (HRR) are available over the KS cloud protocol; input registers arrive via telemetry.");

        var modicon = (startAddress + HoldingModiconBase).ToString(CultureInfo.InvariantCulture);
        var command = Envelope("999-123", ("HRR", new[] { modicon, quantity.ToString(CultureInfo.InvariantCulture) }));

        var reply = await SendAsync(device, command, expectResponse: true, cancellationToken);

        if (!reply.TryGetProperty("HRR", out var hrr) || hrr.GetString() is not { } hex)
            throw new NotSupportedException("Cloud read reply did not contain an 'HRR' payload.");

        return DecodeRegistersHex(hex);
    }

    public async Task WriteRegistersAsync(
        ModbusDevice device, ushort startAddress, ushort[] values, CancellationToken cancellationToken = default)
    {
        var modicon = (startAddress + HoldingModiconBase).ToString(CultureInfo.InvariantCulture);
        var command = Envelope("999-123", ("HRW", new[] { modicon, EncodeRegistersHex(values) }));

        var reply = await SendAsync(device, command, expectResponse: true, cancellationToken);

        var message = reply.TryGetProperty("Message", out var m) ? m.GetString() : null;
        if (message is null || !message.Contains("Success", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Cloud write was not confirmed (Message: '{message ?? "none"}').");
    }

    public Task WriteCoilAsync(
        ModbusDevice device, ushort address, bool value, CancellationToken cancellationToken = default)
    {
        // KS COIL numbers are 1-based (wire 5 → "006"). Coils are momentary actions; the reset coil
        // reboots the meter, which often prevents a reply — so we publish without awaiting a response.
        if (!value)
            return Task.CompletedTask;

        var coil    = (address + 1).ToString("D3", CultureInfo.InvariantCulture);
        var command = Envelope("999-999", ("COIL", coil));
        return SendAsync(device, command, expectResponse: false, cancellationToken);
    }

    public Task SendConfigAsync(
        ModbusDevice device, IReadOnlyDictionary<string, string> fields, CancellationToken cancellationToken = default)
    {
        if (fields.Count == 0)
            return Task.CompletedTask;

        var command = Envelope("999-999", fields.Select(f => (f.Key, (object)f.Value)).ToArray());
        return SendAsync(device, command, expectResponse: false, cancellationToken);
    }

    // ── Transport ─────────────────────────────────────────────────────────────

    private async Task<JsonElement> SendAsync(
        ModbusDevice device, Dictionary<string, object> command, bool expectResponse, CancellationToken cancellationToken)
    {
        if (device.Mqtt is null)
            throw new InvalidOperationException($"Device '{device.Name}' has no MqttConfig.");

        var commandTopic = MqttTopics.Resolve(device.Mqtt.CommandTopic, device);
        var json         = JsonSerializer.Serialize(command);

        if (!expectResponse)
        {
            await _broker.PublishAsync(device.Mqtt, commandTopic, json, cancellationToken);
            return default;
        }

        var channel = await EnsureChannelAsync(device, cancellationToken);

        // Single-flight: the meter does not echo the request id, so only one command may be
        // outstanding on a response topic at a time.
        await channel.Flight.WaitAsync(cancellationToken);
        try
        {
            var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
            channel.Pending = tcs;

            await _broker.PublishAsync(device.Mqtt, commandTopic, json, cancellationToken);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_timeout);
            await using (timeoutCts.Token.Register(() => tcs.TrySetCanceled()))
            {
                try
                {
                    return await tcs.Task;
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException($"No cloud reply within {_timeout.TotalSeconds:0}s.");
                }
            }
        }
        finally
        {
            channel.Pending = null;
            channel.Flight.Release();
        }
    }

    private Task<ResponseChannel> EnsureChannelAsync(ModbusDevice device, CancellationToken cancellationToken)
    {
        var responseTopic = MqttTopics.Resolve(device.Mqtt!.ReplyTopic, device);
        return _channels.GetOrAdd(responseTopic, topic => CreateChannelAsync(device, topic, cancellationToken));
    }

    private async Task<ResponseChannel> CreateChannelAsync(ModbusDevice device, string topic, CancellationToken cancellationToken)
    {
        var channel = new ResponseChannel();
        channel.Subscription = await _broker.SubscribeAsync(
            device.Mqtt!, topic, payload => OnResponseAsync(channel, payload), cancellationToken);
        return channel;
    }

    private static Task OnResponseAsync(ResponseChannel channel, string payload)
    {
        var pending = channel.Pending;
        if (pending is null)
            return Task.CompletedTask; // no command outstanding — this is telemetry, ignore.

        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            // Only command responses complete a pending request; telemetry on the same topic is ignored.
            if (root.ValueKind == JsonValueKind.Object &&
                (root.TryGetProperty("HRR", out _) || root.TryGetProperty("Message", out _)))
            {
                pending.TrySetResult(root.Clone());
            }
        }
        catch (JsonException) { /* ignore malformed payloads */ }

        return Task.CompletedTask;
    }

    // ── Encoding helpers ──────────────────────────────────────────────────────

    private Dictionary<string, object> Envelope(string commandId, params (string Key, object Value)[] fields)
    {
        var id = (Interlocked.Increment(ref _idCounter) % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        var inner = new Dictionary<string, object> { ["id"] = id };
        foreach (var (key, value) in fields)
            inner[key] = value;
        return new Dictionary<string, object> { [commandId] = inner };
    }

    /// <summary>Each register → 4 hex digits, big-endian, concatenated (e.g. [5,2] → "00050002").</summary>
    internal static string EncodeRegistersHex(ushort[] values)
    {
        var sb = new StringBuilder(values.Length * 4);
        foreach (var v in values)
            sb.Append(v.ToString("X4", CultureInfo.InvariantCulture));
        return sb.ToString();
    }

    /// <summary>Splits a hex string into 4-digit big-endian register words.</summary>
    internal static ushort[] DecodeRegistersHex(string hex)
    {
        hex = hex.Trim();
        if (hex.Length % 4 != 0)
            throw new FormatException($"HRR hex length {hex.Length} is not a multiple of 4.");

        var result = new ushort[hex.Length / 4];
        for (int i = 0; i < result.Length; i++)
            result[i] = ushort.Parse(hex.AsSpan(i * 4, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return result;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var channelTask in _channels.Values)
        {
            try
            {
                var channel = await channelTask;
                if (channel.Subscription is not null)
                    await channel.Subscription.DisposeAsync();
                channel.Flight.Dispose();
            }
            catch { /* best-effort teardown */ }
        }
        _channels.Clear();
    }

    private sealed class ResponseChannel
    {
        public SemaphoreSlim Flight { get; } = new(1, 1);
        public volatile TaskCompletionSource<JsonElement>? Pending;
        public IAsyncDisposable? Subscription;
    }
}
