namespace Modbus.Core.Services;

public enum GrandezaCategory
{
    Tensao,
    Corrente,
    Frequencia,
    PotenciaAtiva,
    PotenciaReativa,
    PotenciaAparente,
    FatorPotencia,
    EntradasSaidas,
    Horimetro,
    Energia,
    DeltaEnergia,
    EnergiaPorFase,
    Outros,
}

public sealed record Grandeza(
    ushort MqttId,
    string Code,
    string Description,
    GrandezaCategory Category);

/// <summary>
/// Catalog of measurements ("grandezas") that a device can publish over MQTT/LoRa,
/// keyed by the MQTT/LoRa ID written into the selection slots (42102–42121 + 42201–42230).
/// The ID matches the "END. MQTT e LoRa" column of the KRON datasheet.
/// </summary>
public static class GrandezaCatalog
{
    private static readonly IReadOnlyList<Grandeza> _ks3000 =
    [
        // Tensão (V)
        new(2,  "U0",  "Tensão Trifásica (V)",            GrandezaCategory.Tensao),
        new(4,  "U12", "Tensão Fase/Fase (A-B) (V)",      GrandezaCategory.Tensao),
        new(6,  "U23", "Tensão Fase/Fase (B-C) (V)",      GrandezaCategory.Tensao),
        new(8,  "U31", "Tensão Fase/Fase (C-A) (V)",      GrandezaCategory.Tensao),
        new(10, "U1",  "Tensão Linha 1 (V)",              GrandezaCategory.Tensao),
        new(12, "U2",  "Tensão Linha 2 (V)",              GrandezaCategory.Tensao),
        new(14, "U3",  "Tensão Linha 3 (V)",              GrandezaCategory.Tensao),

        // Corrente (A)
        new(16, "I0",  "Corrente Trifásica (A)",          GrandezaCategory.Corrente),
        new(20, "I1",  "Corrente Linha 1 (A)",            GrandezaCategory.Corrente),
        new(22, "I2",  "Corrente Linha 2 (A)",            GrandezaCategory.Corrente),
        new(24, "I3",  "Corrente Linha 3 (A)",            GrandezaCategory.Corrente),

        // Frequência (Hz)
        new(26, "FA",  "Frequência Linha 1 (Hz)",         GrandezaCategory.Frequencia),

        // Potência Ativa (W)
        new(34, "P0",  "Potência Ativa Trifásica (W)",    GrandezaCategory.PotenciaAtiva),
        new(36, "P1",  "Potência Ativa Linha 1 (W)",      GrandezaCategory.PotenciaAtiva),
        new(38, "P2",  "Potência Ativa Linha 2 (W)",      GrandezaCategory.PotenciaAtiva),
        new(40, "P3",  "Potência Ativa Linha 3 (W)",      GrandezaCategory.PotenciaAtiva),

        // Potência Reativa (VAr)
        new(42, "Q0",  "Potência Reativa Trifásica (VAr)",GrandezaCategory.PotenciaReativa),
        new(44, "Q1",  "Potência Reativa Linha 1 (VAr)",  GrandezaCategory.PotenciaReativa),
        new(46, "Q2",  "Potência Reativa Linha 2 (VAr)",  GrandezaCategory.PotenciaReativa),
        new(48, "Q3",  "Potência Reativa Linha 3 (VAr)",  GrandezaCategory.PotenciaReativa),

        // Potência Aparente (VA)
        new(50, "S0",  "Potência Aparente Trifásica (VA)",GrandezaCategory.PotenciaAparente),
        new(52, "S1",  "Potência Aparente Linha 1 (VA)",  GrandezaCategory.PotenciaAparente),
        new(54, "S2",  "Potência Aparente Linha 2 (VA)",  GrandezaCategory.PotenciaAparente),
        new(56, "S3",  "Potência Aparente Linha 3 (VA)",  GrandezaCategory.PotenciaAparente),

        // Fator de Potência
        new(58, "FP0", "Fator de Potência Trifásico",     GrandezaCategory.FatorPotencia),
        new(60, "FP1", "Fator de Potência Linha 1",       GrandezaCategory.FatorPotencia),
        new(62, "FP2", "Fator de Potência Linha 2",       GrandezaCategory.FatorPotencia),
        new(64, "FP3", "Fator de Potência Linha 3",       GrandezaCategory.FatorPotencia),

        // Entradas digitais
        new(94, "EDP-1", "Contador da EDP-1",             GrandezaCategory.EntradasSaidas),
        new(96, "EDP-2", "Contador da EDP-2",             GrandezaCategory.EntradasSaidas),
        new(98, "EDP-3", "Contador da EDP-3",             GrandezaCategory.EntradasSaidas),

        // Status entradas/saídas digitais
        new(110, "EDP1S",  "Status da EDP1",              GrandezaCategory.EntradasSaidas),
        new(111, "EDP2S",  "Status da EDP2",              GrandezaCategory.EntradasSaidas),
        new(112, "EDP3S",  "Status da EDP3",              GrandezaCategory.EntradasSaidas),
        new(113, "OUT1S",  "Status da Saída 1",           GrandezaCategory.EntradasSaidas),
        new(114, "OUT2S",  "Status da Saída 2",           GrandezaCategory.EntradasSaidas),

        // Largura dos pulsos
        new(130, "EDP-1 Pulso", "Largura do pulso EDP-1", GrandezaCategory.EntradasSaidas),
        new(131, "EDP-2 Pulso", "Largura do pulso EDP-2", GrandezaCategory.EntradasSaidas),
        new(132, "EDP-3 Pulso", "Largura do pulso EDP-3", GrandezaCategory.EntradasSaidas),

        // Status carga
        new(150, "LSTS",  "Status da carga",              GrandezaCategory.EntradasSaidas),

        // Horímetro
        new(160, "HORIM", "Horímetro",                    GrandezaCategory.Horimetro),

        // Energias e Demandas
        new(200, "EA+",  "Energia Ativa Positiva (kWh)",          GrandezaCategory.Energia),
        new(202, "ER+",  "Energia Reativa Positiva (kVArh)",      GrandezaCategory.Energia),
        new(204, "EA-",  "Energia Ativa Negativa (kWh)",          GrandezaCategory.Energia),
        new(206, "ER-",  "Energia Reativa Negativa (kVArh)",      GrandezaCategory.Energia),
        new(208, "MDA",  "Máx. Demanda Ativa (kW)",               GrandezaCategory.Energia),
        new(210, "DA",   "Demanda Ativa (kW)",                    GrandezaCategory.Energia),
        new(212, "MDS",  "Máx. Demanda Aparente (kVA)",           GrandezaCategory.Energia),
        new(214, "DS",   "Demanda Aparente (kVA)",                GrandezaCategory.Energia),
        new(216, "MDR",  "Máx. Demanda Reativa (kVAr)",           GrandezaCategory.Energia),
        new(218, "DR",   "Demanda Reativa (kVAr)",                GrandezaCategory.Energia),
        new(220, "MDI",  "Máx. Demanda Corrente (A)",             GrandezaCategory.Energia),
        new(222, "DI",   "Demanda Corrente (A)",                  GrandezaCategory.Energia),
        new(224, "ES",   "Energia Aparente (kVAh)",               GrandezaCategory.Energia),

        // Delta de Energias
        new(300, "EAD+", "Delta Energia Ativa Positiva (kWh)",        GrandezaCategory.DeltaEnergia),
        new(302, "ERD+", "Delta Energia Reativa Positiva (kVArh)",    GrandezaCategory.DeltaEnergia),
        new(304, "EAD-", "Delta Energia Ativa Negativa (kWh)",        GrandezaCategory.DeltaEnergia),
        new(306, "ERD-", "Delta Energia Reativa Negativa (kVArh)",    GrandezaCategory.DeltaEnergia),
        new(308, "ESD",  "Delta Energia Aparente (kVAh)",             GrandezaCategory.DeltaEnergia),
        new(310, "EA1D+","Delta Energia Ativa Positiva Fase 1 (kWh)", GrandezaCategory.DeltaEnergia),
        new(312, "ER1D+","Delta Energia Reativa Positiva Fase 1 (kVArh)", GrandezaCategory.DeltaEnergia),
        new(314, "EA1D-","Delta Energia Ativa Negativa Fase 1 (kWh)", GrandezaCategory.DeltaEnergia),
        new(316, "ER1D-","Delta Energia Reativa Negativa Fase 1 (kVArh)", GrandezaCategory.DeltaEnergia),
        new(318, "EA2D+","Delta Energia Ativa Positiva Fase 2 (kWh)", GrandezaCategory.DeltaEnergia),
        new(320, "ER2D+","Delta Energia Reativa Positiva Fase 2 (kVArh)", GrandezaCategory.DeltaEnergia),
        new(322, "EA2D-","Delta Energia Ativa Negativa Fase 2 (kWh)", GrandezaCategory.DeltaEnergia),
        new(324, "ER2D-","Delta Energia Reativa Negativa Fase 2 (kVArh)", GrandezaCategory.DeltaEnergia),
        new(326, "EA3D+","Delta Energia Ativa Positiva Fase 3 (kWh)", GrandezaCategory.DeltaEnergia),
        new(328, "ER3D+","Delta Energia Reativa Positiva Fase 3 (kVArh)", GrandezaCategory.DeltaEnergia),
        new(330, "EA3D-","Delta Energia Ativa Negativa Fase 3 (kWh)", GrandezaCategory.DeltaEnergia),
        new(332, "ER3D-","Delta Energia Reativa Negativa Fase 3 (kVArh)", GrandezaCategory.DeltaEnergia),
        new(334, "ES1D", "Delta Energia Aparente Fase 1 (kVAh)",      GrandezaCategory.DeltaEnergia),
        new(336, "ES2D", "Delta Energia Aparente Fase 2 (kVAh)",      GrandezaCategory.DeltaEnergia),
        new(338, "ES3D", "Delta Energia Aparente Fase 3 (kVAh)",      GrandezaCategory.DeltaEnergia),

        // Energias por fase
        new(1200, "EA1+", "Energia Ativa Positiva Fase 1 (kWh)",       GrandezaCategory.EnergiaPorFase),
        new(1202, "ER1+", "Energia Reativa Positiva Fase 1 (kVArh)",   GrandezaCategory.EnergiaPorFase),
        new(1204, "EA1-", "Energia Ativa Negativa Fase 1 (kWh)",       GrandezaCategory.EnergiaPorFase),
        new(1206, "ER1-", "Energia Reativa Negativa Fase 1 (kVArh)",   GrandezaCategory.EnergiaPorFase),
        new(1208, "EA2+", "Energia Ativa Positiva Fase 2 (kWh)",       GrandezaCategory.EnergiaPorFase),
        new(1210, "ER2+", "Energia Reativa Positiva Fase 2 (kVArh)",   GrandezaCategory.EnergiaPorFase),
        new(1212, "EA2-", "Energia Ativa Negativa Fase 2 (kWh)",       GrandezaCategory.EnergiaPorFase),
        new(1214, "ER2-", "Energia Reativa Negativa Fase 2 (kVArh)",   GrandezaCategory.EnergiaPorFase),
        new(1216, "EA3+", "Energia Ativa Positiva Fase 3 (kWh)",       GrandezaCategory.EnergiaPorFase),
        new(1218, "ER3+", "Energia Reativa Positiva Fase 3 (kVArh)",   GrandezaCategory.EnergiaPorFase),
        new(1220, "EA3-", "Energia Ativa Negativa Fase 3 (kWh)",       GrandezaCategory.EnergiaPorFase),
        new(1222, "ER3-", "Energia Reativa Negativa Fase 3 (kVArh)",   GrandezaCategory.EnergiaPorFase),
        new(1224, "ES1",  "Energia Aparente Fase 1 (kVAh)",            GrandezaCategory.EnergiaPorFase),
        new(1226, "ES2",  "Energia Aparente Fase 2 (kVAh)",            GrandezaCategory.EnergiaPorFase),
        new(1228, "ES3",  "Energia Aparente Fase 3 (kVAh)",            GrandezaCategory.EnergiaPorFase),
    ];

    // KS-3000 and Konect 120 share the same measurement set.
    private static readonly Dictionary<byte, IReadOnlyList<Grandeza>> _byDeviceCode = new()
    {
        [0xF2] = _ks3000,
        [0xF3] = _ks3000,
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
