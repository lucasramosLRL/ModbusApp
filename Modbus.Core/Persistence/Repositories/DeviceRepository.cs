using Microsoft.EntityFrameworkCore;
using Modbus.Core.Domain.Entities;
using Modbus.Core.Domain.Repositories;

namespace Modbus.Core.Persistence.Repositories;

public class DeviceRepository : IDeviceRepository
{
    private readonly ModbusDbContext _db;

    public DeviceRepository(ModbusDbContext db) => _db = db;

    public async Task<IReadOnlyList<ModbusDevice>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _db.Devices
            .Include(d => d.DeviceModel)
                .ThenInclude(m => m!.Registers)
            .ToListAsync(cancellationToken);

    public async Task<ModbusDevice?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        await _db.Devices
            .Include(d => d.DeviceModel)
                .ThenInclude(m => m!.Registers)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task AddAsync(ModbusDevice device, CancellationToken cancellationToken = default)
    {
        _db.Devices.Add(device);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ModbusDevice device, CancellationToken cancellationToken = default)
    {
        _db.Devices.Update(device);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var device = await _db.Devices.FindAsync([id], cancellationToken);
        if (device is not null)
        {
            _db.Devices.Remove(device);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
