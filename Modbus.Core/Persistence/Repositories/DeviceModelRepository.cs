using Microsoft.EntityFrameworkCore;
using Modbus.Core.Domain.Entities;
using Modbus.Core.Domain.Repositories;

namespace Modbus.Core.Persistence.Repositories;

public class DeviceModelRepository : IDeviceModelRepository
{
    private readonly ModbusDbContext _db;

    public DeviceModelRepository(ModbusDbContext db) => _db = db;

    public async Task<IReadOnlyList<DeviceModel>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _db.DeviceModels
            .Include(m => m.Registers)
            .ToListAsync(cancellationToken);

    public async Task<DeviceModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        await _db.DeviceModels
            .Include(m => m.Registers)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public async Task<DeviceModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default) =>
        await _db.DeviceModels
            .Include(m => m.Registers)
            .FirstOrDefaultAsync(m => m.Name == name, cancellationToken);

    public async Task AddAsync(DeviceModel model, CancellationToken cancellationToken = default)
    {
        _db.DeviceModels.Add(model);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(DeviceModel model, CancellationToken cancellationToken = default)
    {
        _db.DeviceModels.Update(model);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
