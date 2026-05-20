using FluentAssertions;
using Modbus.Core.Domain.Entities;
using Modbus.Core.Domain.Enums;
using Modbus.Core.Domain.ValueObjects;
using Modbus.Core.Persistence.Repositories;
using Modbus.Core.Tests.Infrastructure;

namespace Modbus.Core.Tests.Persistence;

public sealed class DeviceRepositoryTests : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _conn;
    private readonly Modbus.Core.Persistence.ModbusDbContext _db;
    private readonly DeviceRepository _repo;

    public DeviceRepositoryTests()
    {
        (_db, _conn) = TestDbContextFactory.Create();
        _repo = new DeviceRepository(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ModbusDevice MakeTcpDevice(string ip = "192.168.1.1", byte slaveId = 255) => new()
    {
        Name          = "Test TCP Device",
        SlaveId       = slaveId,
        TransportType = TransportType.Tcp,
        Tcp           = new TcpConfig { IpAddress = ip, Port = 502 },
        IsActive      = true,
    };

    private static ModbusDevice MakeRtuDevice(byte slaveId = 1) => new()
    {
        Name          = "Test RTU Device",
        SlaveId       = slaveId,
        TransportType = TransportType.Rtu,
        Rtu           = new RtuConfig { PortName = "COM1", BaudRate = 9600 },
        IsActive      = true,
    };

    // ── AddAsync / GetByIdAsync ───────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_TcpDevice_PersistsWithTcpConfig()
    {
        var device = MakeTcpDevice("10.0.0.1");
        await _repo.AddAsync(device);

        var found = await _repo.GetByIdAsync(device.Id);

        found.Should().NotBeNull();
        found!.TransportType.Should().Be(TransportType.Tcp);
        found.Tcp.Should().NotBeNull();
        found.Tcp!.IpAddress.Should().Be("10.0.0.1");
    }

    [Fact]
    public async Task GetByIdAsync_IncludesDeviceModelNavigation()
    {
        var model = new DeviceModel { Name = "KS-3000", DeviceCode = 0xF2 };
        _db.DeviceModels.Add(model);
        await _db.SaveChangesAsync();

        var device = MakeTcpDevice();
        device.DeviceModelId = model.Id;
        await _repo.AddAsync(device);

        var found = await _repo.GetByIdAsync(device.Id);

        found!.DeviceModel.Should().NotBeNull();
        found.DeviceModel!.Name.Should().Be("KS-3000");
    }

    // ── Exists checks ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ExistsByTcpIpAsync_ReturnsTrue_WhenIpExists()
    {
        await _repo.AddAsync(MakeTcpDevice("192.168.1.50"));

        var exists = await _repo.ExistsByTcpIpAsync("192.168.1.50");

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByTcpIpAsync_ReturnsFalse_WhenIpAbsent()
    {
        var exists = await _repo.ExistsByTcpIpAsync("192.168.99.99");
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsByRtuSlaveIdAsync_ReturnsTrue_WhenSlaveIdExists()
    {
        await _repo.AddAsync(MakeRtuDevice(slaveId: 5));

        var exists = await _repo.ExistsByRtuSlaveIdAsync(5);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByRtuSlaveIdAsync_ReturnsFalse_WhenSlaveIdAbsent()
    {
        var exists = await _repo.ExistsByRtuSlaveIdAsync(99);
        exists.Should().BeFalse();
    }

    // ── DeleteAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_RemovesDevice()
    {
        var device = MakeTcpDevice();
        await _repo.AddAsync(device);

        await _repo.DeleteAsync(device.Id);

        var found = await _repo.GetByIdAsync(device.Id);
        found.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_NonExistentId_DoesNotThrow()
    {
        var act = async () => await _repo.DeleteAsync(9999);
        await act.Should().NotThrowAsync();
    }

    // ── GetAllAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsAllDevices()
    {
        await _repo.AddAsync(MakeTcpDevice("1.1.1.1", slaveId: 1));
        await _repo.AddAsync(MakeRtuDevice(slaveId: 2));

        var all = await _repo.GetAllAsync();

        all.Should().HaveCount(2);
    }
}
