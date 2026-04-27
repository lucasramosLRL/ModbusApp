using Modbus.Core.Domain.Entities;
using Modbus.Core.Domain.Enums;
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
                var model = new DeviceModel { Name = name, DeviceCode = code };
                await _repository.AddAsync(model);
                existing = model;
            }

            if (name == "KS-3000")    await SeedKs3000RegistersAsync(existing);
            if (name == "Konect 120") await SeedKonect120RegistersAsync(existing);
        }
    }

    private async Task SeedKs3000RegistersAsync(DeviceModel model)
    {
        model.SqpfRegisterAddress = 2900; // holding register 42.901 (FC03, 0-based)
        if (model.Registers.Count == 0)
            model.Registers = RealTimeRegs(model);
        else
            ApplySqpfToExistingRegisters(model);
        await _repository.UpdateAsync(model);
    }

    private async Task SeedKonect120RegistersAsync(DeviceModel model)
    {
        model.SqpfRegisterAddress = 2900; // holding register 42.901 (FC03, 0-based)
        if (model.Registers.Count == 0)
            model.Registers = RealTimeRegs(model);
        else
            ApplySqpfToExistingRegisters(model);
        await _repository.UpdateAsync(model);
    }

    private static void ApplySqpfToExistingRegisters(DeviceModel model)
    {
        foreach (var reg in model.Registers)
        {
            if (reg.DataType == DataType.Float32 && reg.RegisterType == RegisterType.Input)
                reg.WordOrder = WordOrder.UseSqpf;
        }
    }

    // Real-time input registers (FC04) shared by KS-3000, Konect 120 and compatible models.
    // Float32 registers use UseSqpf so word order is resolved at poll time from register 42.901.
    // UInt32 registers (NS) are exempt from SQPF and use ByteSwapped.
    private static List<RegisterDefinition> RealTimeRegs(DeviceModel model) =>
    [
        Reg(model, 0,  "NS",   DataType.UInt32,  null,  "Serial Number",              WordOrder.ByteSwapped),
        Reg(model, 2,  "U0",   DataType.Float32, "V",   "Three-phase Voltage",         WordOrder.UseSqpf),
        Reg(model, 4,  "U12",  DataType.Float32, "V",   "Phase Voltage A-B",           WordOrder.UseSqpf),
        Reg(model, 6,  "U23",  DataType.Float32, "V",   "Phase Voltage B-C",           WordOrder.UseSqpf),
        Reg(model, 8,  "U31",  DataType.Float32, "V",   "Phase Voltage C-A",           WordOrder.UseSqpf),
        Reg(model, 10, "U1",   DataType.Float32, "V",   "Line Voltage 1",              WordOrder.UseSqpf),
        Reg(model, 12, "U2",   DataType.Float32, "V",   "Line Voltage 2",              WordOrder.UseSqpf),
        Reg(model, 14, "U3",   DataType.Float32, "V",   "Line Voltage 3",              WordOrder.UseSqpf),
        Reg(model, 16, "I0",   DataType.Float32, "A",   "Three-phase Current",         WordOrder.UseSqpf),
        Reg(model, 20, "I1",   DataType.Float32, "A",   "Line Current 1",              WordOrder.UseSqpf),
        Reg(model, 22, "I2",   DataType.Float32, "A",   "Line Current 2",              WordOrder.UseSqpf),
        Reg(model, 24, "I3",   DataType.Float32, "A",   "Line Current 3",              WordOrder.UseSqpf),
        Reg(model, 26, "Freq", DataType.Float32, "Hz",  "Frequency",                   WordOrder.UseSqpf),
        Reg(model, 34, "P0",   DataType.Float32, "W",   "Three-phase Active Power",    WordOrder.UseSqpf),
        Reg(model, 36, "P1",   DataType.Float32, "W",   "Active Power Line 1",         WordOrder.UseSqpf),
        Reg(model, 38, "P2",   DataType.Float32, "W",   "Active Power Line 2",         WordOrder.UseSqpf),
        Reg(model, 40, "P3",   DataType.Float32, "W",   "Active Power Line 3",         WordOrder.UseSqpf),
        Reg(model, 42, "Q0",   DataType.Float32, "VAr", "Three-phase Reactive Power",  WordOrder.UseSqpf),
        Reg(model, 44, "Q1",   DataType.Float32, "VAr", "Reactive Power Line 1",       WordOrder.UseSqpf),
        Reg(model, 46, "Q2",   DataType.Float32, "VAr", "Reactive Power Line 2",       WordOrder.UseSqpf),
        Reg(model, 48, "Q3",   DataType.Float32, "VAr", "Reactive Power Line 3",       WordOrder.UseSqpf),
        Reg(model, 50, "S0",   DataType.Float32, "VA",  "Three-phase Apparent Power",  WordOrder.UseSqpf),
        Reg(model, 52, "S1",   DataType.Float32, "VA",  "Apparent Power Line 1",       WordOrder.UseSqpf),
        Reg(model, 54, "S2",   DataType.Float32, "VA",  "Apparent Power Line 2",       WordOrder.UseSqpf),
        Reg(model, 56, "S3",   DataType.Float32, "VA",  "Apparent Power Line 3",       WordOrder.UseSqpf),
        Reg(model, 58, "FP0",  DataType.Float32, null,  "Three-phase Power Factor",    WordOrder.UseSqpf),
        Reg(model, 60, "FP1",  DataType.Float32, null,  "Power Factor Line 1",         WordOrder.UseSqpf),
        Reg(model, 62, "FP2",  DataType.Float32, null,  "Power Factor Line 2",         WordOrder.UseSqpf),
        Reg(model, 64, "FP3",  DataType.Float32, null,  "Power Factor Line 3",         WordOrder.UseSqpf),
    ];

    private static RegisterDefinition Reg(
        DeviceModel model, ushort address, string name, DataType dataType, string? unit, string description,
        WordOrder wordOrder = WordOrder.ByteSwapped) =>
        new()
        {
            DeviceModel   = model,
            DeviceModelId = model.Id,
            Address       = address,
            Name          = name,
            Description   = description,
            DataType      = dataType,
            RegisterType  = RegisterType.Input,
            WordOrder     = wordOrder,
            ScaleFactor   = 1.0,
            Unit          = unit,
            IsWritable    = false,
        };
}
