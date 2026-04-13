using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Modbus.Core.Domain.Enums;
using Modbus.Desktop.Infrastructure;
using Modbus.Desktop.Services;
using System;
using System.Collections.ObjectModel;

namespace Modbus.Desktop.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly LocalizationService _loc;
    private readonly RtuSettingsService  _rtu;

    // ── Language ──────────────────────────────────────────────────────────────

    public AppLanguage[] AvailableLanguages { get; } = Enum.GetValues<AppLanguage>();

    public AppLanguage SelectedLanguage
    {
        get => _loc.CurrentLanguage;
        set
        {
            if (_loc.CurrentLanguage == value) return;
            _loc.CurrentLanguage = value;
            OnPropertyChanged();
        }
    }

    // ── RTU / COM port ────────────────────────────────────────────────────────

    public ObservableCollection<string> AvailablePorts { get; }
    private HashSet<string> _connectedPorts = new(StringComparer.OrdinalIgnoreCase);

    public bool IsPortUnavailable =>
        !string.IsNullOrEmpty(_rtu.PortName) && !_connectedPorts.Contains(_rtu.PortName);

    public int[]      AvailableBaudRates { get; } = { 300, 600, 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200 };
    public Parity[]   AvailableParities  { get; } = (Parity[])Enum.GetValues(typeof(Parity));
    public StopBits[] AvailableStopBits  { get; } = (StopBits[])Enum.GetValues(typeof(StopBits));

    public string? SelectedPort
    {
        get => _rtu.PortName;
        set => _rtu.PortName = value ?? "";
    }

    public int BaudRate
    {
        get => _rtu.BaudRate;
        set => _rtu.BaudRate = value;
    }

    public int DataBits
    {
        get => _rtu.DataBits;
        set => _rtu.DataBits = value;
    }

    public Parity SelectedParity
    {
        get => _rtu.Parity;
        set => _rtu.Parity = value;
    }

    public StopBits SelectedStopBits
    {
        get => _rtu.StopBits;
        set => _rtu.StopBits = value;
    }

    [RelayCommand]
    private void RefreshPorts()
    {
        var current = SelectedPort;
        var ports = SerialPortScanner.GetPortNames();
        _connectedPorts = new HashSet<string>(ports, StringComparer.OrdinalIgnoreCase);
        AvailablePorts.Clear();
        foreach (var p in ports)
            AvailablePorts.Add(p);
        // Keep the saved port in the list even if not currently connected,
        // so the ComboBox binding doesn't reset it to null.
        if (!string.IsNullOrEmpty(current) && !AvailablePorts.Contains(current))
            AvailablePorts.Insert(0, current);
        OnPropertyChanged(nameof(IsPortUnavailable));
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    public SettingsViewModel(LocalizationService loc, RtuSettingsService rtu)
    {
        _loc = loc;
        _rtu = rtu;

        // Populate port list; always include the saved port even if not currently connected,
        // so the ComboBox binding doesn't reset it to null and overwrite the persisted value.
        var ports = SerialPortScanner.GetPortNames();
        _connectedPorts = new HashSet<string>(ports, StringComparer.OrdinalIgnoreCase);
        AvailablePorts = new ObservableCollection<string>(ports);
        if (!string.IsNullOrEmpty(_rtu.PortName) && !AvailablePorts.Contains(_rtu.PortName))
            AvailablePorts.Insert(0, _rtu.PortName);

        // Keep SettingsViewModel in sync when the service changes externally
        _rtu.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(SelectedPort));
            OnPropertyChanged(nameof(BaudRate));
            OnPropertyChanged(nameof(DataBits));
            OnPropertyChanged(nameof(SelectedParity));
            OnPropertyChanged(nameof(SelectedStopBits));
            OnPropertyChanged(nameof(IsPortUnavailable));
        };
    }
}
