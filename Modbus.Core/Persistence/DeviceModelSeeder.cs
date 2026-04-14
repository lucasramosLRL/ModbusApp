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

            if (name == "KS-3000" && existing.Registers.Count == 0)
                await SeedKs3000RegistersAsync(existing);
        }
    }

    private async Task SeedKs3000RegistersAsync(DeviceModel model)
    {
        // KS-3000 real-time input registers (FC 04)
        // All Float32 use LittleEndian word order (F2,F1,F0,EXP = CDAB)
        var registers = new List<RegisterDefinition>
        {
            Reg(model, 0,  "NS",   DataType.UInt32,  null,  "Serial Number"),
            Reg(model, 2,  "U0",   DataType.Float32, "V",   "Three-phase Voltage"),
            Reg(model, 4,  "U12",  DataType.Float32, "V",   "Phase Voltage A-B"),
            Reg(model, 6,  "U23",  DataType.Float32, "V",   "Phase Voltage B-C"),
            Reg(model, 8,  "U31",  DataType.Float32, "V",   "Phase Voltage C-A"),
            Reg(model, 10, "U1",   DataType.Float32, "V",   "Line Voltage 1"),
            Reg(model, 12, "U2",   DataType.Float32, "V",   "Line Voltage 2"),
            Reg(model, 14, "U3",   DataType.Float32, "V",   "Line Voltage 3"),
            Reg(model, 16, "I0",   DataType.Float32, "A",   "Three-phase Current"),
            Reg(model, 20, "I1",   DataType.Float32, "A",   "Line Current 1"),
            Reg(model, 22, "I2",   DataType.Float32, "A",   "Line Current 2"),
            Reg(model, 24, "I3",   DataType.Float32, "A",   "Line Current 3"),
            Reg(model, 26, "Freq", DataType.Float32, "Hz",  "Frequency"),
            Reg(model, 34, "P0",   DataType.Float32, "W",   "Three-phase Active Power"),
            Reg(model, 36, "P1",   DataType.Float32, "W",   "Active Power Line 1"),
            Reg(model, 38, "P2",   DataType.Float32, "W",   "Active Power Line 2"),
            Reg(model, 40, "P3",   DataType.Float32, "W",   "Active Power Line 3"),
            Reg(model, 42, "Q0",   DataType.Float32, "VAr", "Three-phase Reactive Power"),
            Reg(model, 44, "Q1",   DataType.Float32, "VAr", "Reactive Power Line 1"),
            Reg(model, 46, "Q2",   DataType.Float32, "VAr", "Reactive Power Line 2"),
            Reg(model, 48, "Q3",   DataType.Float32, "VAr", "Reactive Power Line 3"),
            Reg(model, 50, "S0",   DataType.Float32, "VA",  "Three-phase Apparent Power"),
            Reg(model, 52, "S1",   DataType.Float32, "VA",  "Apparent Power Line 1"),
            Reg(model, 54, "S2",   DataType.Float32, "VA",  "Apparent Power Line 2"),
            Reg(model, 56, "S3",   DataType.Float32, "VA",  "Apparent Power Line 3"),
            Reg(model, 58, "FP0",  DataType.Float32, null,  "Three-phase Power Factor"),
            Reg(model, 60, "FP1",  DataType.Float32, null,  "Power Factor Line 1"),
            Reg(model, 62, "FP2",  DataType.Float32, null,  "Power Factor Line 2"),
            Reg(model, 64, "FP3",  DataType.Float32, null,  "Power Factor Line 3"),
        };

        model.Registers = registers;
        await _repository.UpdateAsync(model);
    }

    private static RegisterDefinition Reg(
        DeviceModel model, ushort address, string name, DataType dataType, string? unit, string description) =>
        new()
        {
            DeviceModel   = model,
            DeviceModelId = model.Id,
            Address       = address,
            Name          = name,
            Description   = description,
            DataType      = dataType,
            RegisterType  = RegisterType.Input,
            WordOrder     = WordOrder.LittleEndian,
            ScaleFactor   = 1.0,
            Unit          = unit,
            IsWritable    = false,
        };
}
