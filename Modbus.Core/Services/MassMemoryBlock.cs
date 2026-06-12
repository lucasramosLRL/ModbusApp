namespace Modbus.Core.Services;

/// <summary>
/// Um bloco de dados lido da memória de massa via FC 0x14 (ReadFileRecord).
/// </summary>
public sealed record MassMemoryBlock(
    DateTime Timestamp,
    double[] Values,
    bool     ChecksumOk,
    int      BlockIndex,
    int      IterationIndex);
