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
    private readonly ThemeService        _theme;

    // ── Localized labels ──────────────────────────────────────────────────────

    [ObservableProperty] private string _settingsTitle        = string.Empty;
    [ObservableProperty] private string _languageLabel        = string.Empty;
    [ObservableProperty] private string _themeLabel           = string.Empty;
    [ObservableProperty] private string _serialPortSettings   = string.Empty;
    [ObservableProperty] private string _comPortLabel         = string.Empty;
    [ObservableProperty] private string _baudRateLabel        = string.Empty;
    [ObservableProperty] private string _dataBitsLabel        = string.Empty;
    [ObservableProperty] private string _parityLabel          = string.Empty;
    [ObservableProperty] private string _stopBitsLabel        = string.Empty;
    [ObservableProperty] private string _refreshPortListLabel = string.Empty;
    [ObservableProperty] private string _portUnavailableLabel = string.Empty;

    private void UpdateLabels()
    {
        var loc = LocalizationService.Instance;
        SettingsTitle        = loc["SettingsTitle"];
        LanguageLabel        = loc["Language"];
        ThemeLabel           = loc["Theme"];
        SerialPortSettings   = loc["SerialPortSettings"];
        ComPortLabel         = loc["ComPort"];
        BaudRateLabel        = loc["BaudRate"];
        DataBitsLabel        = loc["DataBits"];
        ParityLabel          = loc["Parity"];
        StopBitsLabel        = loc["StopBits"];
        RefreshPortListLabel = loc["RefreshPortList"];
        PortUnavailableLabel = loc["PortUnavailable"];
    }

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

    // ── Theme ───────────────────────────────────────────────────────────────────

    public AppTheme[] AvailableThemes { get; } = Enum.GetValues<AppTheme>();

    public AppTheme SelectedTheme
    {
        get => _theme.CurrentTheme;
        set
        {
            if (_theme.CurrentTheme == value) return;
            _theme.CurrentTheme = value;
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

    public SettingsViewModel(LocalizationService loc, RtuSettingsService rtu, ThemeService theme)
    {
        _loc   = loc;
        _rtu   = rtu;
        _theme = theme;

        // Populate port list; always include the saved port even if not currently connected,
        // so the ComboBox binding doesn't reset it to null and overwrite the persisted value.
        var ports = SerialPortScanner.GetPortNames();
        _connectedPorts = new HashSet<string>(ports, StringComparer.OrdinalIgnoreCase);
        AvailablePorts = new ObservableCollection<string>(ports);
        if (!string.IsNullOrEmpty(_rtu.PortName) && !AvailablePorts.Contains(_rtu.PortName))
            AvailablePorts.Insert(0, _rtu.PortName);

        UpdateLabels();
        LocalizationService.Instance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == "Item[]") UpdateLabels();
        };

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
