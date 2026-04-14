using Modbus.Core.Domain.Entities;

namespace Modbus.Core.Domain.Repositories;

public interface IDeviceModelRepository
{
    Task<IReadOnlyList<DeviceModel>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<DeviceModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<DeviceModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task AddAsync(DeviceModel model, CancellationToken cancellationToken = default);
    Task UpdateAsync(DeviceModel model, CancellationToken cancellationToken = default);
}
