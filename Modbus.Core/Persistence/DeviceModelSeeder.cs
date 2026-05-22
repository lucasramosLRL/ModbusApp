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
        ApplySqpfToExistingRegisters(model);
        MergeRegisters(model, RealTimeRegs(model));
        MergeRegisters(model, EnergyDemandRegs(model));
        MergeRegisters(model, HourmeterRegs(model));
        MergeRegisters(model, IoRegs(model));
        MergeRegisters(model, StatusRegs(model));
        await _repository.UpdateAsync(model);
    }

    private async Task SeedKonect120RegistersAsync(DeviceModel model)
    {
        model.SqpfRegisterAddress = 2900; // holding register 42.901 (FC03, 0-based)
        ApplySqpfToExistingRegisters(model);
        MergeRegisters(model, RealTimeRegs(model));
        MergeRegisters(model, EnergyDemandRegs(model));
        MergeRegisters(model, HourmeterRegs(model));
        MergeRegisters(model, IoRegs(model));
        MergeRegisters(model, StatusRegs(model));
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

    private static void MergeRegisters(DeviceModel model, IEnumerable<RegisterDefinition> candidates)
    {
        var existing = model.Registers.Select(r => r.Address).ToHashSet();
        foreach (var reg in candidates)
            if (!existing.Contains(reg.Address))
                model.Registers.Add(reg);
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

    private static List<RegisterDefinition> EnergyDemandRegs(DeviceModel model) =>
    [
        Reg(model, 200, "EA+", DataType.Float32, "kWh",   "Energia Ativa Positiva",   WordOrder.UseSqpf),
        Reg(model, 202, "ER+", DataType.Float32, "kVArh", "Energia Reativa Positiva", WordOrder.UseSqpf),
        Reg(model, 204, "EA-", DataType.Float32, "kWh",   "Energia Ativa Negativa",   WordOrder.UseSqpf),
        Reg(model, 206, "ER-", DataType.Float32, "kVArh", "Energia Reativa Negativa", WordOrder.UseSqpf),
        Reg(model, 208, "MDA", DataType.Float32, "kW",    "Máx. Demanda Ativa",       WordOrder.UseSqpf),
        Reg(model, 210, "DA",  DataType.Float32, "kW",    "Demanda Ativa",            WordOrder.UseSqpf),
        Reg(model, 212, "MDS", DataType.Float32, "kVA",   "Máx. Demanda Aparente",    WordOrder.UseSqpf),
        Reg(model, 214, "DS",  DataType.Float32, "kVA",   "Demanda Aparente",         WordOrder.UseSqpf),
        Reg(model, 216, "MDR", DataType.Float32, "kVAr",  "Máx. Demanda Reativa",     WordOrder.UseSqpf),
        Reg(model, 218, "DR",  DataType.Float32, "kVAr",  "Demanda Reativa",          WordOrder.UseSqpf),
        Reg(model, 220, "MDI", DataType.Float32, "A",     "Máx. Demanda Corrente",    WordOrder.UseSqpf),
        Reg(model, 222, "DI",  DataType.Float32, "A",     "Demanda Corrente",         WordOrder.UseSqpf),
        Reg(model, 224, "ES",  DataType.Float32, "kVA",   "Energia Aparente",         WordOrder.UseSqpf),
    ];

    // Digital input/output registers (FC04, Input type).
    // Counters are Float32 + UseSqpf (same byte order as real-time regs).
    // Status registers are UInt16 BigEndian (standard 0=off, 1=on).
    // Pulse width registers are UInt16 ByteSwapped (device stores LSB first) with scale 0.1 → seconds.
    private static List<RegisterDefinition> HourmeterRegs(DeviceModel model) =>
    [
        Reg(model, 150, "LSTS",  DataType.UInt16,  null, "Status da Carga",        WordOrder.BigEndian,  1.0),
        Reg(model, 160, "HORIM", DataType.Float32, "h",  "Horímetro",              WordOrder.UseSqpf,    1.0),
    ];

    // Error-status registers (FC04, Input type).
    // Erro at Modicon 33.901 (0-based 3900): UInt16 — meter error bitmask (LSB + MSB).
    // ErroWF at Modicon 33.903 (0-based 3902): UInt16 — communication module error bitmask.
    private static List<RegisterDefinition> StatusRegs(DeviceModel model) =>
    [
        Reg(model, 3900, "Erro",   DataType.UInt16, null, "Código de Erro do Medidor",  WordOrder.BigEndian),
        Reg(model, 3902, "ErroWF", DataType.UInt16, null, "Código de Erro do Módulo",   WordOrder.BigEndian),
    ];

    private static List<RegisterDefinition> IoRegs(DeviceModel model) =>
    [
        Reg(model, 94,  "EDP1C", DataType.Float32, null, "Contador EDP-1",        WordOrder.UseSqpf,    1.0),
        Reg(model, 96,  "EDP2C", DataType.Float32, null, "Contador EDP-2",        WordOrder.UseSqpf,    1.0),
        Reg(model, 98,  "EDP3C", DataType.Float32, null, "Contador EDP-3",        WordOrder.UseSqpf,    1.0),
        Reg(model, 110, "EDP1S", DataType.UInt16,  null, "Status EDP-1",          WordOrder.BigEndian,  1.0),
        Reg(model, 111, "EDP2S", DataType.UInt16,  null, "Status EDP-2",          WordOrder.BigEndian,  1.0),
        Reg(model, 112, "EDP3S", DataType.UInt16,  null, "Status EDP-3",          WordOrder.BigEndian,  1.0),
        Reg(model, 113, "OUT1S", DataType.UInt16,  null, "Status Saída-1",        WordOrder.BigEndian,  1.0),
        Reg(model, 114, "OUT2S", DataType.UInt16,  null, "Status Saída-2",        WordOrder.BigEndian,  1.0),
        Reg(model, 130, "EDP1W", DataType.UInt16,  "s",  "Largura do Pulso EDP-1",WordOrder.ByteSwapped,0.1),
        Reg(model, 131, "EDP2W", DataType.UInt16,  "s",  "Largura do Pulso EDP-2",WordOrder.ByteSwapped,0.1),
        Reg(model, 132, "EDP3W", DataType.UInt16,  "s",  "Largura do Pulso EDP-3",WordOrder.ByteSwapped,0.1),
    ];

    private static RegisterDefinition Reg(
        DeviceModel model, ushort address, string name, DataType dataType, string? unit, string description,
        WordOrder wordOrder = WordOrder.ByteSwapped, double scaleFactor = 1.0) =>
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
            ScaleFactor   = scaleFactor,
            Unit          = unit,
            IsWritable    = false,
        };
}
