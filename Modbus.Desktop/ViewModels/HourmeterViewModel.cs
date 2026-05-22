using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Modbus.Desktop.Services;
using System;
using System.Threading.Tasks;

namespace Modbus.Desktop.ViewModels;

public partial class HourmeterViewModel : ObservableObject
{
    private readonly Func<Task> _zeroCoil;

    [ObservableProperty]
    private double _hourValue;

    [ObservableProperty]
    private bool _isOn;

    public string HourText   => $"{HourValue:F2} h";
    public string StatusText => IsOn
        ? LocalizationService.Instance["IoStateOn"]
        : LocalizationService.Instance["IoStateOff"];

    public HourmeterViewModel(Func<Task> zeroCoil)
    {
        _zeroCoil = zeroCoil;
    }

    partial void OnHourValueChanged(double value) => OnPropertyChanged(nameof(HourText));
    partial void OnIsOnChanged(bool value)         => OnPropertyChanged(nameof(StatusText));

    public void UpdateHour(double v)   => HourValue = v;
    public void UpdateStatus(double v) => IsOn = v > 0;

    [RelayCommand]
    private Task ZeroAsync() => _zeroCoil();
}
