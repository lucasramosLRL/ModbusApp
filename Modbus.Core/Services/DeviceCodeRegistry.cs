namespace Modbus.Core.Services;

public static class DeviceCodeRegistry
{
    private static readonly Dictionary<byte, string> _knownModels = new()
    {
        [0x44] = "MKM-C",
        [0x60] = "MKM-120",
        [0x71] = "TKE485-01",
        [0x72] = "MKM-01",
        [0x73] = "MKM-01 Demanda Ativa",
        [0x74] = "MKM-D Demanda Ativa/Aparente",
        [0x80] = "MKM-D Memoria de Massa",
        [0x90] = "MULTK",
        [0x91] = "MULTK2 Sem Memoria",
        [0x92] = "MULTK2 Com Memoria",
        [0x93] = "MULTK3 Sem Memoria",
        [0x94] = "MULTK3 Com Memoria",
        [0x95] = "XP2 5A",
        [0x96] = "XP2 120A",
        [0x97] = "XP2 5A 2 Seriais",
        [0x98] = "MULT-KC",
        [0xA0] = "MULT-K NG",
        [0xA1] = "MULT-K NG 70 MIPS",
        [0xB0] = "KONECT 63",
        [0xD0] = "MULT-K Serie 2",
        [0xE0] = "iKRON",
        [0xF0] = "iKRON 03 Trifasico",
        [0x10] = "MULT-K Easy Kron",
        [0x30] = "IVY Trifasico",
        [0xE2] = "iKRON 01D",
        [0xF2] = "KS-3000",
        [0xE5] = "DC96 PLUS",
        [0xF3] = "Konect 120",
        [0xF4] = "Konect DC",
        [0xF5] = "Konect Plus",
        [0xF6] = "Konect Grafic",
        [0xF7] = "Konect 05",
        [0xF8] = "Konect RW",
        [0xF9] = "Konect Plus RW",
        [0xFA] = "Konect Grafic RW",
        [0xFB] = "Konect K",
        [0xE3] = "iKRON 05D",
    };

    public static bool TryGetModelName(byte deviceCode, out string? name)
        => _knownModels.TryGetValue(deviceCode, out name);

    public static string? GetModelName(byte deviceCode)
        => _knownModels.TryGetValue(deviceCode, out var name) ? name : null;

    public static IReadOnlyDictionary<byte, string> KnownModels => _knownModels;
}
