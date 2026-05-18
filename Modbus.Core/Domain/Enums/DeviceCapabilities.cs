namespace Modbus.Core.Domain.Enums;

[Flags]
public enum DeviceCapabilities
{
    None               = 0,
    Ethernet           = 1 << 0,
    Wireless           = 1 << 1,
    Sntp               = 1 << 2,
    Iot                = 1 << 3,
    Clock              = 1 << 4,
    InputsOutputs      = 1 << 5,
    FieldKe            = 1 << 6,
    FieldCurrentInvert = 1 << 7,
}
