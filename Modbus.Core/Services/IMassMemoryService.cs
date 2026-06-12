using Modbus.Core.Domain.Entities;

namespace Modbus.Core.Services;

public interface IMassMemoryService
{
    /// <summary>
    /// Lê o Bloco de Controle via FC04 (Modicon 33931–33935).
    /// Retorna null se a leitura falhar.
    /// </summary>
    Task<MassMemoryControlBlock?> ReadControlBlockAsync(
        ModbusDevice device, CancellationToken ct = default);

    /// <summary>
    /// Lê todos os blocos da memória de massa via FC14 (ReadFileRecord), mantendo
    /// uma única conexão aberta durante toda a sessão. Faz yield de cada bloco assim
    /// que é decodificado para permitir exibição progressiva na UI.
    /// </summary>
    IAsyncEnumerable<MassMemoryBlock> ReadBlocksAsync(
        ModbusDevice device,
        MassMemoryControlBlock ctrl,
        int startFrom = 0,
        CancellationToken ct = default);
}
