namespace Modbus.Desktop.ViewModels;

/// <summary>
/// Decodes Modbus error-status registers (Erro / ErroWF) into human-readable messages.
/// Each register is a UInt16 where LSB carries meter/module fault bits and MSB carries
/// system-level or network fault bits. All bit values are powers of two (1, 2, 4 … 128).
/// </summary>
internal static class MeterErrorCodes
{
    private record ErrorTable(string?[] LsbBits, string?[] MsbBits);

    // ── Konect 120 — Erro (meter, Modicon 33.901) ─────────────────────────────
    private static readonly ErrorTable Konect120Erro = new(
        LsbBits:
        [
            "Inversão de Fase ou Falta de Fase",       // bit 0 → 001
            "Erro Matemático",                          // bit 1 → 002
            "Overflow na geração do Pulso de Energia", // bit 2 → 004
            null,                                      // bit 3 → 008 (reservado)
            "Sistema reinicializado incorretamente",   // bit 4 → 016
            "Bateria Fraca",                           // bit 5 → 032
            "RTC – Erro de sincronia",                 // bit 6 → 064
            "Erro na Memória de Massa",                // bit 7 → 128
        ],
        MsbBits:
        [
            "Reservado para uso futuro",                         // bit 0 → 001
            "Configuração incorreta do módulo de comunicação",   // bit 1 → 002
            "Configuração incorreta do Hardware utilizado",      // bit 2 → 004
            "Proteção de Firmware ativa",                        // bit 3 → 008
            "Alarme de curva de carga ativado",                  // bit 4 → 016
            null, null, null,
        ]
    );

    // ── Konect 120 — ErroWF (WiFi/Ethernet module, Modicon 33.903) ────────────
    private static readonly ErrorTable Konect120ErroWF = new(
        LsbBits:
        [
            "Tempo máximo de conexão com o AP atingido",  // bit 0 → 001
            "Senha de conexão com AP incorreta",           // bit 1 → 002
            "Não conseguiu encontrar o AP",                // bit 2 → 004
            "Conexão com AP falhou",                       // bit 3 → 008
            "O broker recusou o login do instrumento",    // bit 4 → 016
            "Erro na publicação das grandezas",            // bit 5 → 032
            "Sem internet",                                // bit 6 → 064
            "Erro desconhecido",                           // bit 7 → 128
        ],
        MsbBits:
        [
            "Ethernet não recebeu IP da rede",            // bit 0 → 001
            "IP da rede Ethernet configurado é inválido", // bit 1 → 002
            "WiFi não recebeu IP da rede",                // bit 2 → 004
            "IP da rede WiFi configurado é inválido",     // bit 3 → 008
            null, null, null, null,                       // bits 4-7 → reservado
        ]
    );

    // ── KS-3000 ───────────────────────────────────────────────────────────────
    // Reuse Konect 120 tables until KS-3000 documentation specifies different codes.
    private static readonly ErrorTable Ks3000Erro   = Konect120Erro;
    private static readonly ErrorTable Ks3000ErroWF = Konect120ErroWF;

    // ── Lookup ────────────────────────────────────────────────────────────────
    private static readonly Dictionary<(string Model, string Reg), ErrorTable> Tables = new()
    {
        [("Konect 120", "Erro")]   = Konect120Erro,
        [("Konect 120", "ErroWF")] = Konect120ErroWF,
        [("KS-3000",    "Erro")]   = Ks3000Erro,
        [("KS-3000",    "ErroWF")] = Ks3000ErroWF,
    };

    /// <summary>Returns the list of active error descriptions for the given model, register, and raw value.</summary>
    public static IReadOnlyList<string> Decode(string modelName, string regName, ushort rawValue)
    {
        if (!Tables.TryGetValue((modelName, regName), out var table))
            return [];

        var errors = new List<string>();
        byte lsb = (byte)(rawValue & 0xFF);
        byte msb = (byte)(rawValue >> 8);

        CollectBits(errors, lsb, table.LsbBits);
        CollectBits(errors, msb, table.MsbBits);

        return errors;
    }

    private static void CollectBits(List<string> errors, byte value, string?[] descs)
    {
        for (int bit = 0; bit < 8; bit++)
        {
            if ((value & (1 << bit)) == 0) continue;
            string? desc = descs[bit];
            if (desc is not null)
                errors.Add($"({(1 << bit):D3}) - {desc}");
        }
    }
}
