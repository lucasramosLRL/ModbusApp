using Microsoft.EntityFrameworkCore;
using Modbus.Core.Domain.Entities;
using Modbus.Core.Domain.Enums;
using Modbus.Core.Domain.Repositories;

namespace Modbus.Core.Persistence.Repositories;

public class RegisterValueRepository : IRegisterValueRepository
{
    private readonly ModbusDbContext _db;

    public RegisterValueRepository(ModbusDbContext db) => _db = db;

    public async Task<IReadOnlyList<RegisterValue>> GetByDeviceIdAsync(int deviceId, CancellationToken cancellationToken = default) =>
        await _db.RegisterValues
            .Where(r => r.DeviceId == deviceId)
            .ToListAsync(cancellationToken);

    public async Task UpsertAsync(int deviceId, IEnumerable<RegisterValue> values, CancellationToken cancellationToken = default)
    {
        // Single query to load all existing values for this device
        var existing = await _db.RegisterValues
            .Where(r => r.DeviceId == deviceId)
            .ToDictionaryAsync(r => (r.Address, r.RegisterType), cancellationToken);

        foreach (var value in values)
        {
            var key = (value.Address, value.RegisterType);
            if (existing.TryGetValue(key, out var stored))
            {
                stored.Value     = value.Value;
                stored.RawWords  = value.RawWords;
                stored.Timestamp = value.Timestamp;
            }
            else
            {
                _db.RegisterValues.Add(value);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
