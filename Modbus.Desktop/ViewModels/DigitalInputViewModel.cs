using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Modbus.Desktop.Services;
using System;
using System.Threading.Tasks;

namespace Modbus.Desktop.ViewModels;

public partial class DigitalInputViewModel : ObservableObject
{
    private readonly Func<Task> _resetCoil;

    public string Name { get; }

    [ObservableProperty]
    private bool _isOn;

    [ObservableProperty]
    private double _counter;

    [ObservableProperty]
    private double _pulseWidthSeconds;

    public string StatusText  => IsOn
        ? LocalizationService.Instance["IoStateOn"]
        : LocalizationService.Instance["IoStateOff"];

    public string CounterText     => $"{Counter:F0}";
    public string PulseText       => $"{PulseWidthSeconds:F1} s";

    public DigitalInputViewModel(string name, Func<Task> resetCoil)
    {
        Name = name;
        _resetCoil = resetCoil;
    }

    partial void OnIsOnChanged(bool value)             => OnPropertyChanged(nameof(StatusText));
    partial void OnCounterChanged(double value)        => OnPropertyChanged(nameof(CounterText));
    partial void OnPulseWidthSecondsChanged(double value) => OnPropertyChanged(nameof(PulseText));

    public void UpdateStatus(double v)  => IsOn = v > 0;
    public void UpdateCounter(double v) => Counter = v;
    public void UpdatePulse(double v)   => PulseWidthSeconds = v;

    [RelayCommand]
    private Task ResetCounterAsync() => _resetCoil();
}
