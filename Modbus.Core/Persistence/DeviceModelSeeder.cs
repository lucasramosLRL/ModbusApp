using Modbus.Core.Domain.Entities;
using Modbus.Core.Domain.Repositories;
using Modbus.Core.Services;

namespace Modbus.Core.Persistence;

public class DeviceModelSeeder
{
    private readonly IDeviceModelRepository _repository;

    public DeviceModelSeeder(IDeviceModelRepository repository)
    {
        _repository = repository;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var (code, name) in DeviceCodeRegistry.KnownModels)
        {
            var existing = await _repository.GetByNameAsync(name);
            if (existing is null)
            {
                await _repository.AddAsync(new DeviceModel
                {
                    Name = name,
                    DeviceCode = code
                });
            }
        }
    }
}
