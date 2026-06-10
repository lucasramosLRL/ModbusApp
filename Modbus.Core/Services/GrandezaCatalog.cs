namespace Modbus.Core.Services;

public sealed record Grandeza(
    ushort MqttId,
    string Code,
    string Description);

/// <summary>
/// Catalog of measurements ("grandezas") that a device can publish over MQTT/LoRa,
/// keyed by the MQTT/LoRa ID written into the selection slots (42102–42121 + 42201–42230).
/// The ID matches the "END. MQTT e LoRa" column of the KRON datasheet.
/// </summary>
public static class GrandezaCatalog
{
    // Full set — Konect 120 supports all five I/O channels (EDP1-3, SD1-2).
    private static readonly IReadOnlyList<Grandeza> _konect120 =
    [
        // Tensão (V)
        new(2,  "U0",  "Tensão Trifásica (V)"),
        new(4,  "U12", "Tensão Fase/Fase (A-B) (V)"),
        new(6,  "U23", "Tensão Fase/Fase (B-C) (V)"),
        new(8,  "U31", "Tensão Fase/Fase (C-A) (V)"),
        new(10, "U1",  "Tensão Linha 1 (V)"),
        new(12, "U2",  "Tensão Linha 2 (V)"),
        new(14, "U3",  "Tensão Linha 3 (V)"),

        // Corrente (A)
        new(16, "I0",  "Corrente Trifásica (A)"),
        new(20, "I1",  "Corrente Linha 1 (A)"),
        new(22, "I2",  "Corrente Linha 2 (A)"),
        new(24, "I3",  "Corrente Linha 3 (A)"),

        // Frequência (Hz)
        new(26, "FA",  "Frequência Linha 1 (Hz)"),

        // Potência Ativa (W)
        new(34, "P0",  "Potência Ativa Trifásica (W)"),
        new(36, "P1",  "Potência Ativa Linha 1 (W)"),
        new(38, "P2",  "Potência Ativa Linha 2 (W)"),
        new(40, "P3",  "Potência Ativa Linha 3 (W)"),

        // Potência Reativa (VAr)
        new(42, "Q0",  "Potência Reativa Trifásica (VAr)"),
        new(44, "Q1",  "Potência Reativa Linha 1 (VAr)"),
        new(46, "Q2",  "Potência Reativa Linha 2 (VAr)"),
        new(48, "Q3",  "Potência Reativa Linha 3 (VAr)"),

        // Potência Aparente (VA)
        new(50, "S0",  "Potência Aparente Trifásica (VA)"),
        new(52, "S1",  "Potência Aparente Linha 1 (VA)"),
        new(54, "S2",  "Potência Aparente Linha 2 (VA)"),
        new(56, "S3",  "Potência Aparente Linha 3 (VA)"),

        // Fator de Potência
        new(58, "FP0", "Fator de Potência Trifásico"),
        new(60, "FP1", "Fator de Potência Linha 1"),
        new(62, "FP2", "Fator de Potência Linha 2"),
        new(64, "FP3", "Fator de Potência Linha 3"),

        // Entradas digitais
        new(94, "EDP-1", "Contador da EDP-1"),
        new(96, "EDP-2", "Contador da EDP-2"),
        new(98, "EDP-3", "Contador da EDP-3"),

        // Status entradas/saídas digitais
        new(110, "EDP1S",  "Status da EDP1"),
        new(111, "EDP2S",  "Status da EDP2"),
        new(112, "EDP3S",  "Status da EDP3"),
        new(113, "OUT1S",  "Status da Saída 1"),
        new(114, "OUT2S",  "Status da Saída 2"),

        // Largura dos pulsos
        new(130, "EDP1P", "Largura do pulso EDP-1"),
        new(131, "EDP2P", "Largura do pulso EDP-2"),
        new(132, "EDP3P", "Largura do pulso EDP-3"),

        // Status carga
        new(150, "LSTS",  "Status da carga"),

        // Horímetro
        new(160, "HORIM", "Horímetro"),

        // Energias e Demandas
        new(200, "EA+",  "Energia Ativa Positiva (kWh)"),
        new(202, "ER+",  "Energia Reativa Positiva (kVArh)"),
        new(204, "EA-",  "Energia Ativa Negativa (kWh)"),
        new(206, "ER-",  "Energia Reativa Negativa (kVArh)"),
        new(208, "MDA",  "Máx. Demanda Ativa (kW)"),
        new(210, "DA",   "Demanda Ativa (kW)"),
        new(212, "MDS",  "Máx. Demanda Aparente (kVA)"),
        new(214, "DS",   "Demanda Aparente (kVA)"),
        new(216, "MDR",  "Máx. Demanda Reativa (kVAr)"),
        new(218, "DR",   "Demanda Reativa (kVAr)"),
        new(220, "MDI",  "Máx. Demanda Corrente (A)"),
        new(222, "DI",   "Demanda Corrente (A)"),
        new(224, "ES",   "Energia Aparente (kVAh)"),

        // Delta de Energias
        new(300, "EAD+", "Delta Energia Ativa Positiva (kWh)"),
        new(302, "ERD+", "Delta Energia Reativa Positiva (kVArh)"),
        new(304, "EAD-", "Delta Energia Ativa Negativa (kWh)"),
        new(306, "ERD-", "Delta Energia Reativa Negativa (kVArh)"),
        new(308, "ESD",  "Delta Energia Aparente (kVAh)"),
        new(310, "EA1D+","Delta Energia Ativa Positiva Fase 1 (kWh)"),
        new(312, "ER1D+","Delta Energia Reativa Positiva Fase 1 (kVArh)"),
        new(314, "EA1D-","Delta Energia Ativa Negativa Fase 1 (kWh)"),
        new(316, "ER1D-","Delta Energia Reativa Negativa Fase 1 (kVArh)"),
        new(318, "EA2D+","Delta Energia Ativa Positiva Fase 2 (kWh)"),
        new(320, "ER2D+","Delta Energia Reativa Positiva Fase 2 (kVArh)"),
        new(322, "EA2D-","Delta Energia Ativa Negativa Fase 2 (kWh)"),
        new(324, "ER2D-","Delta Energia Reativa Negativa Fase 2 (kVArh)"),
        new(326, "EA3D+","Delta Energia Ativa Positiva Fase 3 (kWh)"),
        new(328, "ER3D+","Delta Energia Reativa Positiva Fase 3 (kVArh)"),
        new(330, "EA3D-","Delta Energia Ativa Negativa Fase 3 (kWh)"),
        new(332, "ER3D-","Delta Energia Reativa Negativa Fase 3 (kVArh)"),
        new(334, "ES1D", "Delta Energia Aparente Fase 1 (kVAh)"),
        new(336, "ES2D", "Delta Energia Aparente Fase 2 (kVAh)"),
        new(338, "ES3D", "Delta Energia Aparente Fase 3 (kVAh)"),

        // Energias por fase
        new(1200, "EA1+", "Energia Ativa Positiva Fase 1 (kWh)"),
        new(1202, "ER1+", "Energia Reativa Positiva Fase 1 (kVArh)"),
        new(1204, "EA1-", "Energia Ativa Negativa Fase 1 (kWh)"),
        new(1206, "ER1-", "Energia Reativa Negativa Fase 1 (kVArh)"),
        new(1208, "EA2+", "Energia Ativa Positiva Fase 2 (kWh)"),
        new(1210, "ER2+", "Energia Reativa Positiva Fase 2 (kVArh)"),
        new(1212, "EA2-", "Energia Ativa Negativa Fase 2 (kWh)"),
        new(1214, "ER2-", "Energia Reativa Negativa Fase 2 (kVArh)"),
        new(1216, "EA3+", "Energia Ativa Positiva Fase 3 (kWh)"),
        new(1218, "ER3+", "Energia Reativa Positiva Fase 3 (kVArh)"),
        new(1220, "EA3-", "Energia Ativa Negativa Fase 3 (kWh)"),
        new(1222, "ER3-", "Energia Reativa Negativa Fase 3 (kVArh)"),
        new(1224, "ES1",  "Energia Aparente Fase 1 (kVAh)"),
        new(1226, "ES2",  "Energia Aparente Fase 2 (kVAh)"),
        new(1228, "ES3",  "Energia Aparente Fase 3 (kVAh)"),
    ];

    // KS-3000 has fixed I/O: EDP1, EDP2, SD1 only — no EDP3 (98,112,132) or SD2 (114).
    private static readonly IReadOnlyList<Grandeza> _ks3000 =
        [.. _konect120.Where(g => g.MqttId is not (98 or 112 or 114 or 132))];

    private static readonly Dictionary<byte, IReadOnlyList<Grandeza>> _byDeviceCode = new()
    {
        [0xF2] = _ks3000,
        [0xF3] = _konect120,
    };

    public static IReadOnlyList<Grandeza> ForDeviceCode(byte? deviceCode) =>
        deviceCode.HasValue && _byDeviceCode.TryGetValue(deviceCode.Value, out var list)
            ? list
            : Array.Empty<Grandeza>();

    /// <summary>
    /// Maximum number of measurements the user can select for MQTT/LoRa publishing.
    /// TODO: map FirmwareVersion → 10/20/50 once the firmware-version table is provided.
    /// For now all supported models default to 50.
    /// </summary>
    public static int Limit(byte? deviceCode, byte? firmwareVersion) => 50;
}
