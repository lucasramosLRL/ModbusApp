using Modbus.Core.Domain.Entities;

namespace Modbus.Core.Cloud;

/// <summary>Resolves topic templates (containing <c>{serial}</c>) against a concrete device.</summary>
public static class MqttTopics
{
    public const string SerialPlaceholder = "{serial}";

    public static string Resolve(string template, ModbusDevice device)
    {
        // KS serial is zero-padded to 7 digits in the topic (e.g. 1 → "0000001").
        var serial = device.SerialNumber?.ToString("D7") ?? device.Id.ToString("D7");
        return template.Replace(SerialPlaceholder, serial);
    }
}
