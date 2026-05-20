using FluentAssertions;
using Modbus.Core.Domain.Entities;
using Modbus.Core.Domain.Enums;
using Modbus.Core.Persistence.Repositories;
using Modbus.Core.Tests.Infrastructure;

namespace Modbus.Core.Tests.Persistence;

public sealed class DeviceModelRepositoryTests : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _conn;
    private readonly Modbus.Core.Persistence.ModbusDbContext _db;
    private readonly DeviceModelRepository _repo;

    public DeviceModelRepositoryTests()
    {
        (_db, _conn) = TestDbContextFactory.Create();
        _repo = new DeviceModelRepository(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    // ── AddAsync / GetByIdAsync ───────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_PersistsModel_CanBeRetrievedById()
    {
        var model = new DeviceModel { Name = "KS-3000", DeviceCode = 0xF2 };
        await _repo.AddAsync(model);

        var found = await _repo.GetByIdAsync(model.Id);

        found.Should().NotBeNull();
        found!.Name.Should().Be("KS-3000");
        found.DeviceCode.Should().Be(0xF2);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        var result = await _repo.GetByIdAsync(9999);
        result.Should().BeNull();
    }

    // ── GetByNameAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByNameAsync_ExistingModel_ReturnsModel()
    {
        await _repo.AddAsync(new DeviceModel { Name = "Konect 120", DeviceCode = 0xF3 });

        var result = await _repo.GetByNameAsync("Konect 120");

        result.Should().NotBeNull();
        result!.DeviceCode.Should().Be(0xF3);
    }

    [Fact]
    public async Task GetByNameAsync_NonExistentModel_ReturnsNull()
    {
        var result = await _repo.GetByNameAsync("Unknown Model");
        result.Should().BeNull();
    }

    // ── GetAllAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsAllModels()
    {
        await _repo.AddAsync(new DeviceModel { Name = "KS-3000",   DeviceCode = 0xF2 });
        await _repo.AddAsync(new DeviceModel { Name = "Konect 120", DeviceCode = 0xF3 });

        var all = await _repo.GetAllAsync();

        all.Should().HaveCount(2);
        all.Select(m => m.Name).Should().Contain(["KS-3000", "Konect 120"]);
    }

    [Fact]
    public async Task GetAllAsync_IncludesRegistersNavigation()
    {
        var model = new DeviceModel
        {
            Name = "KS-3000",
            Registers = new List<RegisterDefinition>
            {
                new()
                {
                    Name         = "U0",
                    Address      = 2,
                    DataType     = DataType.Float32,
                    RegisterType = RegisterType.Input,
                    WordOrder    = WordOrder.UseSqpf,
                    ScaleFactor  = 1.0,
                }
            }
        };
        await _repo.AddAsync(model);

        var all = await _repo.GetAllAsync();

        all.Should().HaveCount(1);
        all[0].Registers.Should().HaveCount(1);
        all[0].Registers.First().Name.Should().Be("U0");
    }

    // ── UpdateAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_ChangesPersisted()
    {
        var model = new DeviceModel { Name = "KS-3000" };
        await _repo.AddAsync(model);

        model.SqpfRegisterAddress = 2900;
        await _repo.UpdateAsync(model);

        var found = await _repo.GetByIdAsync(model.Id);
        found!.SqpfRegisterAddress.Should().Be(2900);
    }
}
