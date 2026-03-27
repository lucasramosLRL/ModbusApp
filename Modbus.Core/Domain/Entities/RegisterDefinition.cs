using Modbus.Core.Domain.Enums;

namespace Modbus.Core.Domain.Entities;

public class RegisterDefinition
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public ushort Address { get; set; }
    public RegisterType RegisterType { get; set; }
    public DataType DataType { get; set; }
    public WordOrder WordOrder { get; set; } = WordOrder.BigEndian;
    public double ScaleFactor { get; set; } = 1.0;
    public string? Unit { get; set; }
    public bool IsWritable { get; set; }

    /// <summary>
    /// Number of 16-bit Modbus registers this value occupies.
    /// </summary>
    public int RegisterCount => DataType switch
    {
        DataType.UInt32 or DataType.Int32 or DataType.Float32 => 2,
        _ => 1
    };

    public int DeviceModelId { get; set; }
    public DeviceModel DeviceModel { get; set; } = null!;
}
