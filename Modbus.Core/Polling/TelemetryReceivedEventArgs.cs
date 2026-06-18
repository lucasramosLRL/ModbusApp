using Modbus.Core.Cloud;
using Modbus.Core.Domain.Entities;

namespace Modbus.Core.Polling;

/// <summary>
/// Raised when a cloud device publishes a telemetry message. Carries one <see cref="TelemetryReading"/>
/// per numeric field in the payload — including fields with no register definition — so subscribers
/// can render exactly what the meter is publishing.
/// </summary>
public class TelemetryReceivedEventArgs : EventArgs
{
    public required ModbusDevice Device { get; init; }
    public required IReadOnlyList<TelemetryReading> Readings { get; init; }
    public DateTime Timestamp { get; init; }
}
