using Modbus.Core.Domain.Entities;

namespace Modbus.Core.Cloud;

/// <summary>
/// Converts a decoded JSON telemetry payload (as published by field meters) into the
/// register-value shape used by the rest of the app, so cloud devices feed the same
/// reading pipeline (<see cref="Polling.RegisterValuesUpdatedEventArgs"/>) as local devices.
/// </summary>
public interface ITelemetryPayloadMapper
{
    /// <summary>
    /// Maps <paramref name="jsonPayload"/> to register values using the device's register
    /// definitions to resolve each JSON field to an address/type. Unknown or non-numeric
    /// fields are ignored. If the payload carries no timestamp, <paramref name="fallbackTimestamp"/> is used.
    /// </summary>
    IReadOnlyList<RegisterValue> Map(ModbusDevice device, string jsonPayload, DateTime fallbackTimestamp);
}
