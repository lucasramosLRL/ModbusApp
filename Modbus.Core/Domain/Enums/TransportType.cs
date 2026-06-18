namespace Modbus.Core.Domain.Enums;

public enum TransportType
{
    Tcp,
    Rtu,

    /// <summary>Field device reached through a cloud MQTT broker (telemetry pub/sub + command/reply).</summary>
    MqttCloud
}
