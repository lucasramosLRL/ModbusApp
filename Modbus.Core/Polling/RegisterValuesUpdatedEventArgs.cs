using Modbus.Core.Domain.Entities;

namespace Modbus.Core.Polling;

public class RegisterValuesUpdatedEventArgs : EventArgs
{
    public required ModbusDevice Device { get; init; }
    public required IReadOnlyList<RegisterValue> Values { get; init; }
    public DateTime Timestamp { get; init; }
}
