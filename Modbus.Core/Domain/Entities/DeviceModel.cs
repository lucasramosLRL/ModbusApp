namespace Modbus.Core.Domain.Entities;

public class DeviceModel
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Manufacturer { get; set; }
    public byte? DeviceCode { get; set; }
    public ushort? SqpfRegisterAddress { get; set; }

    public ICollection<RegisterDefinition> Registers { get; set; } = [];
    public ICollection<ModbusDevice> Devices { get; set; } = [];
}
