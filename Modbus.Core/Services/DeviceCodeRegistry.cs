namespace Modbus.Core.Services;

public static class DeviceCodeRegistry
{
    private static readonly Dictionary<byte, string> _knownModels = new()
    {
        [0xF2] = "KS-3000",
    };

    public static bool TryGetModelName(byte deviceCode, out string? name)
        => _knownModels.TryGetValue(deviceCode, out name);

    public static string? GetModelName(byte deviceCode)
        => _knownModels.TryGetValue(deviceCode, out var name) ? name : null;

    public static IReadOnlyDictionary<byte, string> KnownModels => _knownModels;
}
