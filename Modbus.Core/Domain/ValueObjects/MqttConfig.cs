namespace Modbus.Core.Domain.ValueObjects;

/// <summary>
/// Connection settings for reaching a KS field meter through a cloud MQTT broker.
/// <para>
/// Per the KS MQTT command spec, the meter subscribes to <see cref="CommandTopic"/>
/// (literally named ".../reply") to RECEIVE commands, and publishes telemetry and command
/// responses on <see cref="TelemetryTopic"/> / <see cref="ReplyTopic"/>.
/// </para>
/// Topic strings may contain the <c>{serial}</c> placeholder, resolved to the 7-digit serial.
/// </summary>
public class MqttConfig
{
    public required string BrokerHost { get; set; }
    public int Port { get; set; } = 8883;
    public bool UseTls { get; set; } = true;

    public string? ClientId { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }

    /// <summary>Topic the app PUBLISHES commands to — the meter's subscribe topic ("ks-01/{serial}/reply").</summary>
    public string CommandTopic { get; set; } = "ks-01/{serial}/reply";

    /// <summary>Topic the meter PUBLISHES telemetry on. NOTE: confirm exact name with firmware team.</summary>
    public string TelemetryTopic { get; set; } = "ks-01/{serial}/data";

    /// <summary>Topic the meter PUBLISHES command responses on. NOTE: confirm exact name with firmware team.</summary>
    public string ReplyTopic { get; set; } = "ks-01/{serial}/data";
}
