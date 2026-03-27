using Modbus.Core.Domain.Enums;

namespace Modbus.Core.Domain.Entities;

public class RegisterValue
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public ModbusDevice Device { get; set; } = null!;

    public ushort Address { get; set; }
    public RegisterType RegisterType { get; set; }

    /// <summary>Scaled engineering value (raw * ScaleFactor).</summary>
    public double Value { get; set; }

    /// <summary>Raw 16-bit words as read from the device, before scaling.</summary>
    public ushort[] RawWords { get; set; } = [];

    public DateTime Timestamp { get; set; }
}
