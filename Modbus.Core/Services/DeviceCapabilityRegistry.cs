using Modbus.Core.Domain.Enums;

namespace Modbus.Core.Services;

/// <summary>Maps device codes to the set of features available on that model.</summary>
public static class DeviceCapabilityRegistry
{
    private static readonly Dictionary<byte, DeviceCapabilities> _map = new()
    {
        // KS-3000 — somente Wi-Fi, sem Ethernet por cabo
        [0xF2] = DeviceCapabilities.Wireless        |
                 DeviceCapabilities.Sntp            |
                 DeviceCapabilities.Iot             |
                 DeviceCapabilities.Clock           |
                 DeviceCapabilities.InputsOutputs   |
                 DeviceCapabilities.FieldKe         |
                 DeviceCapabilities.FieldCurrentInvert |
                 DeviceCapabilities.IotGrandezaSelection,

        // Konect 120 — Ethernet por cabo + Wi-Fi; I/O configurável na fábrica
        [0xF3] = DeviceCapabilities.Ethernet       |
                 DeviceCapabilities.Wireless        |
                 DeviceCapabilities.Sntp            |
                 DeviceCapabilities.Iot             |
                 DeviceCapabilities.Clock           |
                 DeviceCapabilities.InputsOutputs   |
                 DeviceCapabilities.FieldKe         |
                 DeviceCapabilities.FieldCurrentInvert |
                 DeviceCapabilities.IotGrandezaSelection |
                 DeviceCapabilities.ConfigurableIo,
    };

    public static DeviceCapabilities Get(byte? deviceCode) =>
        deviceCode.HasValue && _map.TryGetValue(deviceCode.Value, out var caps)
            ? caps
            : DeviceCapabilities.None;
}
