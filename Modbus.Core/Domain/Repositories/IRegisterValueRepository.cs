using Modbus.Core.Domain.Entities;

namespace Modbus.Core.Domain.Repositories;

public interface IRegisterValueRepository
{
    Task<IReadOnlyList<RegisterValue>> GetByDeviceIdAsync(int deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates existing register values for the device or inserts them if not yet stored.
    /// Only the latest value per register address is kept.
    /// </summary>
    Task UpsertAsync(int deviceId, IEnumerable<RegisterValue> values, CancellationToken cancellationToken = default);
}
