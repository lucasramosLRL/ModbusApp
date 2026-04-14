using CommunityToolkit.Mvvm.ComponentModel;
using Modbus.Desktop.Services;

namespace Modbus.Desktop.ViewModels;

public partial class ElectricalReadingViewModel : ObservableObject
{
    public string Name { get; }
    public ushort Address { get; }
    public string? Unit { get; }

    [ObservableProperty]
    private double _value;

    [ObservableProperty]
    private string _displayValue = "---";

    public string Description => LocalizationService.Instance[$"Reg{Name}"];

    public ElectricalReadingViewModel(string name, ushort address, string? unit)
    {
        Name = name;
        Address = address;
        Unit = unit;
    }

    public void Update(double newValue)
    {
        Value = newValue;
        DisplayValue = Unit is { Length: > 0 } u ? $"{newValue:F2} {u}" : $"{newValue:F3}";
    }
}
