using Modbus.Core.Domain.Enums;
using Modbus.Core.Domain.ValueObjects;

namespace Modbus.Core.Domain.Entities;

public class ModbusDevice
{
    public int Id { get; set; }
    public required string Name { get; set; }

    /// <summary>Modbus slave/unit identifier (1–247).</summary>
    public byte SlaveId { get; set; }
    public TransportType TransportType { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastSeenAt { get; set; }

    /// <summary>Null until model is detected via FC17 Report Slave ID.</summary>
    public int? DeviceModelId { get; set; }
    public DeviceModel? DeviceModel { get; set; }

    public TcpConfig? Tcp { get; set; }
    public RtuConfig? Rtu { get; set; }

    public ICollection<RegisterValue> RegisterValues { get; set; } = [];
}
