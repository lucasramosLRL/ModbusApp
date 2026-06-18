using Modbus.Core.Domain.Entities;

namespace Modbus.Core.Cloud;

/// <summary>
/// One quantity decoded from a telemetry payload. Unlike <see cref="RegisterValue"/> this keeps the
/// original payload field <see cref="Code"/> (e.g. <c>U0</c>, <c>F1</c>, <c>CE</c>) so the cloud
/// reading screen can render exactly what the meter publishes — including fields that have no
/// register definition in our catalog (<see cref="Definition"/> is then <c>null</c>).
/// </summary>
/// <param name="Code">Raw field name as it appears in the payload metadata.</param>
/// <param name="Value">Engineering value (telemetry is already decoded).</param>
/// <param name="Definition">Matching register definition when the field is known; otherwise <c>null</c>.</param>
public sealed record TelemetryReading(string Code, double Value, RegisterDefinition? Definition);
