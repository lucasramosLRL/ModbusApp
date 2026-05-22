using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Modbus.Desktop.Services;
using System;
using System.Threading.Tasks;

namespace Modbus.Desktop.ViewModels;

public partial class DigitalOutputViewModel : ObservableObject
{
    private readonly Func<bool, Task> _writeCoil;

    public string Name { get; }

    [ObservableProperty]
    private bool _isOn;

    public string StatusText => IsOn
        ? LocalizationService.Instance["IoStateOn"]
        : LocalizationService.Instance["IoStateOff"];

    public DigitalOutputViewModel(string name, Func<bool, Task> writeCoil)
    {
        Name = name;
        _writeCoil = writeCoil;
    }

    partial void OnIsOnChanged(bool value) => OnPropertyChanged(nameof(StatusText));

    public void UpdateStatus(double v) => IsOn = v > 0;

    [RelayCommand]
    private Task TurnOnAsync()  => _writeCoil(true);

    [RelayCommand]
    private Task TurnOffAsync() => _writeCoil(false);
}
