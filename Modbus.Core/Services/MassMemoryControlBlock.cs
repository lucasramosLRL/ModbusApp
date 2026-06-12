namespace Modbus.Core.Services;

/// <summary>
/// Bloco de controle da memória de massa (FC04, endereços Modicon 33931–33935).
/// </summary>
public sealed record MassMemoryControlBlock(
    ushort QSF,   // Quantidade de setores da memória flash
    byte   GP,    // Grandezas programadas na MM
    int    BGS,   // Total de blocos gravados
    ushort INI,   // Setor onde está o primeiro bloco (modo circular)
    ushort CA);   // Capacidade de armazenamento por setor (blocos)
