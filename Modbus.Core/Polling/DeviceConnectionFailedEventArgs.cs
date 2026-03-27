using Modbus.Core.Domain.Entities;

namespace Modbus.Core.Polling;

public class DeviceConnectionFailedEventArgs : EventArgs
{
    public required ModbusDevice Device { get; init; }
    public required Exception Exception { get; init; }
}
