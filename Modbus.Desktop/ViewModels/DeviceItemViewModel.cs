using CommunityToolkit.Mvvm.ComponentModel;
using Modbus.Core.Domain.Entities;
using Modbus.Core.Domain.Enums;
using Modbus.Desktop.Services;
using System;
using System.ComponentModel;

namespace Modbus.Desktop.ViewModels;

public partial class DeviceItemViewModel : ObservableObject, IDisposable
{
    public ModbusDevice Device { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool _isConnected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool _hasError;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastSeenText))]
    private DateTime? _lastSeenAt;

    public DeviceItemViewModel(ModbusDevice device)
    {
        Device = device;
        LastSeenAt = device.LastSeenAt;
        LocalizationService.Instance.PropertyChanged += OnLocalizationChanged;
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != "Item[]") return;
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ModelDisplayName));
        OnPropertyChanged(nameof(SlaveIdText));
        OnPropertyChanged(nameof(LastSeenText));
    }

    public void Dispose() =>
        LocalizationService.Instance.PropertyChanged -= OnLocalizationChanged;

    public void NotifySlaveIdChanged()
    {
        OnPropertyChanged(nameof(SlaveId));
        OnPropertyChanged(nameof(SlaveIdText));
    }

    public int Id => Device.Id;
    public string Name => Device.Name;
    public byte SlaveId => Device.SlaveId;
    public TransportType TransportType => Device.TransportType;

    // Uppercase label shown in the device list.
    public string TransportTypeDisplay => Device.TransportType switch
    {
        TransportType.Tcp       => "TCP",
        TransportType.Rtu       => "RTU",
        TransportType.MqttCloud => "MQTT",
        _                       => Device.TransportType.ToString().ToUpperInvariant()
    };

    public string ModelDisplayName =>
        Device.DeviceModel?.Name ?? LocalizationService.Instance["UnknownModel"];

    public string SlaveIdText =>
        string.Format(LocalizationService.Instance["AddrPrefix"], Device.SlaveId);

    public string? FirmwareText => Device.FirmwareVersion.HasValue
        ? $"v{Device.FirmwareVersion.Value / 10}.{Device.FirmwareVersion.Value % 10}"
        : null;

    public string LastSeenText => LastSeenAt.HasValue
        ? LastSeenAt.Value.ToString("HH:mm:ss")
        : LocalizationService.Instance["NeverSeen"];

    public string ConnectionAddress => Device.TransportType == TransportType.Tcp
        ? Device.Tcp?.IpAddress ?? "—"
        : Device.Rtu?.PortName ?? "—";

    public string StatusText => IsConnected
        ? LocalizationService.Instance["Connected"]
        : LocalizationService.Instance["Disconnected"];
}
