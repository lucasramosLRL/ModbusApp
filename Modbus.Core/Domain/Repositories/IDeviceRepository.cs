using Modbus.Core.Domain.Entities;

namespace Modbus.Core.Domain.Repositories;

public interface IDeviceRepository
{
    Task<IReadOnlyList<ModbusDevice>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ModbusDevice?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task AddAsync(ModbusDevice device, CancellationToken cancellationToken = default);
    Task UpdateAsync(ModbusDevice device, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
