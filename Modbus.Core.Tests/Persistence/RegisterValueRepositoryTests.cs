using FluentAssertions;
using Modbus.Core.Domain.Entities;
using Modbus.Core.Domain.Enums;
using Modbus.Core.Domain.ValueObjects;
using Modbus.Core.Persistence.Repositories;
using Modbus.Core.Tests.Infrastructure;

namespace Modbus.Core.Tests.Persistence;

public sealed class RegisterValueRepositoryTests : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _conn;
    private readonly Modbus.Core.Persistence.ModbusDbContext _db;
    private readonly RegisterValueRepository _repo;

    public RegisterValueRepositoryTests()
    {
        (_db, _conn) = TestDbContextFactory.Create();
        _repo = new RegisterValueRepository(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<ModbusDevice> CreateDeviceAsync(int id = 1)
    {
        var device = new ModbusDevice
        {
            Name          = $"Device {id}",
            SlaveId       = (byte)id,
            TransportType = TransportType.Tcp,
            Tcp           = new TcpConfig { IpAddress = $"10.0.0.{id}", Port = 502 },
            IsActive      = true,
        };
        _db.Devices.Add(device);
        await _db.SaveChangesAsync();
        return device;
    }

    private static RegisterValue MakeValue(int deviceId, ushort address, double value,
        RegisterType regType = RegisterType.Input) => new()
    {
        DeviceId     = deviceId,
        Address      = address,
        RegisterType = regType,
        Value        = value,
        RawWords     = [0x1234, 0x5678],
        Timestamp    = DateTime.UtcNow,
    };

    // ── UpsertAsync — insert ──────────────────────────────────────────────────

    [Fact]
    public async Task UpsertAsync_NewValues_AreInserted()
    {
        var device = await CreateDeviceAsync();

        var values = new List<RegisterValue>
        {
            MakeValue(device.Id, address: 2,  value: 220.0),
            MakeValue(device.Id, address: 4,  value: 230.0),
        };

        await _repo.UpsertAsync(device.Id, values);

        var stored = await _repo.GetByDeviceIdAsync(device.Id);
        stored.Should().HaveCount(2);
        stored.Select(v => v.Value).Should().Contain([220.0, 230.0]);
    }

    // ── UpsertAsync — update ──────────────────────────────────────────────────

    [Fact]
    public async Task UpsertAsync_ExistingValues_AreUpdated_NotDuplicated()
    {
        var device = await CreateDeviceAsync();

        await _repo.UpsertAsync(device.Id, [MakeValue(device.Id, 2, 100.0)]);
        await _repo.UpsertAsync(device.Id, [MakeValue(device.Id, 2, 999.0)]);

        var stored = await _repo.GetByDeviceIdAsync(device.Id);
        stored.Should().HaveCount(1, "upsert must not duplicate the register");
        stored[0].Value.Should().Be(999.0);
    }

    // ── UpsertAsync — mixed ───────────────────────────────────────────────────

    [Fact]
    public async Task UpsertAsync_MixOfNewAndExisting_HandledCorrectly()
    {
        var device = await CreateDeviceAsync();

        await _repo.UpsertAsync(device.Id, [MakeValue(device.Id, 2, 1.0)]);

        await _repo.UpsertAsync(device.Id, [
            MakeValue(device.Id, 2, 2.0),   // existing → update
            MakeValue(device.Id, 4, 3.0),   // new → insert
        ]);

        var stored = await _repo.GetByDeviceIdAsync(device.Id);
        stored.Should().HaveCount(2);
        stored.First(v => v.Address == 2).Value.Should().Be(2.0);
        stored.First(v => v.Address == 4).Value.Should().Be(3.0);
    }

    // ── GetByDeviceIdAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetByDeviceIdAsync_ReturnsOnlyValuesForThatDevice()
    {
        var device1 = await CreateDeviceAsync(id: 1);
        var device2 = await CreateDeviceAsync(id: 2);

        await _repo.UpsertAsync(device1.Id, [MakeValue(device1.Id, 2, 100.0)]);
        await _repo.UpsertAsync(device2.Id, [MakeValue(device2.Id, 2, 200.0)]);

        var d1Values = await _repo.GetByDeviceIdAsync(device1.Id);
        d1Values.Should().HaveCount(1);
        d1Values[0].Value.Should().Be(100.0);

        var d2Values = await _repo.GetByDeviceIdAsync(device2.Id);
        d2Values.Should().HaveCount(1);
        d2Values[0].Value.Should().Be(200.0);
    }
}
