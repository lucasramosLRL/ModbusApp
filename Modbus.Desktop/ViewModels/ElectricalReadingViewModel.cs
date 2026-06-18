using CommunityToolkit.Mvvm.ComponentModel;
using Modbus.Desktop.Services;

namespace Modbus.Desktop.ViewModels;

public partial class ElectricalReadingViewModel : ObservableObject
{
    public string Name { get; }
    public ushort Address { get; }
    public string? Unit { get; }

    private readonly string? _fallbackDescription;

    [ObservableProperty]
    private double _value;

    [ObservableProperty]
    private string _displayValue = "---";

    public string Description
    {
        get
        {
            var key = $"Reg{Name}";
            var localized = LocalizationService.Instance[key];
            // The indexer returns "[key]" when the key is missing — fall back to the supplied text.
            return localized == $"[{key}]" ? _fallbackDescription ?? string.Empty : localized;
        }
    }

    public ElectricalReadingViewModel(string name, ushort address, string? unit, string? fallbackDescription = null)
    {
        Name = name;
        Address = address;
        Unit = unit;
        _fallbackDescription = fallbackDescription;
    }

    public void Update(double newValue)
    {
        Value = newValue;
        DisplayValue = Unit is { Length: > 0 } u ? $"{newValue:F2} {u}" : $"{newValue:F3}";
    }
}
