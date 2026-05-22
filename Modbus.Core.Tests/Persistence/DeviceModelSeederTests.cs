using FluentAssertions;
using Modbus.Core.Domain.Entities;
using Modbus.Core.Domain.Enums;
using Modbus.Core.Domain.Repositories;
using Modbus.Core.Persistence;
using NSubstitute;

namespace Modbus.Core.Tests.Persistence;

public sealed class DeviceModelSeederTests
{
    private readonly IDeviceModelRepository _repo = Substitute.For<IDeviceModelRepository>();
    private readonly DeviceModelSeeder _seeder;

    public DeviceModelSeederTests()
    {
        _seeder = new DeviceModelSeeder(_repo);
    }

    // ── Model creation ────────────────────────────────────────────────────────

    [Fact]
    public async Task SeedAsync_ModelNotFound_CallsAddAsync()
    {
        // GetByNameAsync returns null for all names (NSubstitute default for nullable)
        // AddAsync is a no-op (NSubstitute returns Task.CompletedTask)

        DeviceModel? addedKs3000 = null;
        _repo.When(r => r.AddAsync(Arg.Any<DeviceModel>(), Arg.Any<CancellationToken>()))
             .Do(ci =>
             {
                 var m = ci.Arg<DeviceModel>();
                 if (m.Name == "KS-3000") addedKs3000 = m;
             });

        await _seeder.SeedAsync();

        addedKs3000.Should().NotBeNull("KS-3000 model must be created when not found");
        addedKs3000!.DeviceCode.Should().Be(0xF2);
    }

    [Fact]
    public async Task SeedAsync_ModelAlreadyExists_DoesNotCallAddAsync()
    {
        _repo.GetByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(ci => new DeviceModel
             {
                 Name      = ci.Arg<string>(),
                 Registers = MakeOneRegister(),
             });

        await _seeder.SeedAsync();

        await _repo.DidNotReceive().AddAsync(Arg.Any<DeviceModel>(), Arg.Any<CancellationToken>());
    }

    // ── Register seeding ──────────────────────────────────────────────────────

    [Fact]
    public async Task SeedAsync_NewModel_SeedsCorrectRegisterCount()
    {
        DeviceModel? updated = null;
        _repo.When(r => r.UpdateAsync(Arg.Any<DeviceModel>(), Arg.Any<CancellationToken>()))
             .Do(ci =>
             {
                 var m = ci.Arg<DeviceModel>();
                 if (m.Name == "KS-3000") updated = m;
             });

        await _seeder.SeedAsync();

        updated.Should().NotBeNull();
        updated!.Registers.Should().HaveCount(53); // 29 real-time + 13 energy/demand + 11 IO
    }

    [Fact]
    public async Task SeedAsync_NewModel_Float32InputRegisters_HaveUseSqpfWordOrder()
    {
        DeviceModel? updated = null;
        _repo.When(r => r.UpdateAsync(Arg.Any<DeviceModel>(), Arg.Any<CancellationToken>()))
             .Do(ci =>
             {
                 var m = ci.Arg<DeviceModel>();
                 if (m.Name == "KS-3000") updated = m;
             });

        await _seeder.SeedAsync();

        var float32InputRegs = updated!.Registers
            .Where(r => r.DataType == DataType.Float32 && r.RegisterType == RegisterType.Input)
            .ToList();

        float32InputRegs.Should().NotBeEmpty();
        float32InputRegs.Should().AllSatisfy(r =>
            r.WordOrder.Should().Be(WordOrder.UseSqpf));
    }

    [Fact]
    public async Task SeedAsync_NewModel_UInt32Register_HasByteSwappedWordOrder()
    {
        DeviceModel? updated = null;
        _repo.When(r => r.UpdateAsync(Arg.Any<DeviceModel>(), Arg.Any<CancellationToken>()))
             .Do(ci =>
             {
                 var m = ci.Arg<DeviceModel>();
                 if (m.Name == "KS-3000") updated = m;
             });

        await _seeder.SeedAsync();

        var nsReg = updated!.Registers.FirstOrDefault(r => r.Name == "NS");
        nsReg.Should().NotBeNull();
        nsReg!.WordOrder.Should().Be(WordOrder.ByteSwapped);
        nsReg.DataType.Should().Be(DataType.UInt32);
    }

    [Fact]
    public async Task SeedAsync_NewModel_SetsSqpfRegisterAddressTo2900()
    {
        DeviceModel? updated = null;
        _repo.When(r => r.UpdateAsync(Arg.Any<DeviceModel>(), Arg.Any<CancellationToken>()))
             .Do(ci =>
             {
                 var m = ci.Arg<DeviceModel>();
                 if (m.Name == "KS-3000") updated = m;
             });

        await _seeder.SeedAsync();

        updated!.SqpfRegisterAddress.Should().Be(2900);
    }

    // ── Idempotency ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SeedAsync_ExistingModelWithRegisters_MergesNewRegisters()
    {
        var existingReg = MakeOneRegister();
        _repo.GetByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(ci => new DeviceModel
             {
                 Name      = ci.Arg<string>(),
                 Registers = existingReg,
             });

        DeviceModel? updated = null;
        _repo.When(r => r.UpdateAsync(Arg.Any<DeviceModel>(), Arg.Any<CancellationToken>()))
             .Do(ci =>
             {
                 var m = ci.Arg<DeviceModel>();
                 if (m.Name == "KS-3000") updated = m;
             });

        await _seeder.SeedAsync();

        // Existing register at address 0 is preserved; remaining 28 real-time + 13 energy + 11 IO registers
        // are merged in (the NS register at address 0 is skipped because it already exists).
        updated!.Registers.Should().HaveCount(53);
        updated.Registers.Should().Contain(r => r.Address == 0 && r.Name == "Dummy");
    }

    [Fact]
    public async Task SeedAsync_ExistingModelWithFloat32Registers_AppliesUseSqpfWordOrder()
    {
        var existingRegs = new List<RegisterDefinition>
        {
            new()
            {
                Name         = "U0",
                Address      = 2,
                DataType     = DataType.Float32,
                RegisterType = RegisterType.Input,
                WordOrder    = WordOrder.ByteSwapped, // old value, should be updated
                ScaleFactor  = 1.0,
            }
        };

        _repo.GetByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(ci => new DeviceModel
             {
                 Name      = ci.Arg<string>(),
                 Registers = existingRegs,
             });

        DeviceModel? updated = null;
        _repo.When(r => r.UpdateAsync(Arg.Any<DeviceModel>(), Arg.Any<CancellationToken>()))
             .Do(ci =>
             {
                 var m = ci.Arg<DeviceModel>();
                 if (m.Name == "KS-3000") updated = m;
             });

        await _seeder.SeedAsync();

        updated!.Registers.First().WordOrder.Should().Be(WordOrder.UseSqpf);
    }

    // ── Coverage of both models ───────────────────────────────────────────────

    [Fact]
    public async Task SeedAsync_BothKnownModels_CallUpdateAsync()
    {
        var updatedNames = new List<string>();
        _repo.When(r => r.UpdateAsync(Arg.Any<DeviceModel>(), Arg.Any<CancellationToken>()))
             .Do(ci => updatedNames.Add(ci.Arg<DeviceModel>().Name));

        await _seeder.SeedAsync();

        updatedNames.Should().Contain("KS-3000");
        updatedNames.Should().Contain("Konect 120");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static List<RegisterDefinition> MakeOneRegister() =>
    [
        new RegisterDefinition
        {
            Name         = "Dummy",
            Address      = 0,
            DataType     = DataType.UInt16,
            RegisterType = RegisterType.Input,
            WordOrder    = WordOrder.BigEndian,
            ScaleFactor  = 1.0,
        }
    ];
}
