using Modbus.Core.Domain.Entities;
using Modbus.Core.Domain.Enums;
using System;

namespace Modbus.Desktop.ViewModels;

public class RegisterValueViewModel
{
    public string Name { get; }
    public ushort Address { get; }
    public RegisterType RegisterType { get; }
    public double Value { get; }
    public string? Unit { get; }
    public DateTime Timestamp { get; }
    public string DisplayValue { get; }

    public RegisterValueViewModel(RegisterValue value, RegisterDefinition? definition)
    {
        Name = definition?.Name ?? $"Register {value.Address}";
        Address = value.Address;
        RegisterType = value.RegisterType;
        Value = value.Value;
        Unit = definition?.Unit;
        Timestamp = value.Timestamp;
        DisplayValue = Unit is { Length: > 0 } u ? $"{Value:G6} {u}" : $"{Value:G6}";
    }
}
