using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Modbus.Core.Domain.Entities;
using Modbus.Core.Domain.Enums;

namespace Modbus.Mobile.Avalonia.ViewModels;

public partial class DeviceItemViewModel : ObservableObject
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
    }

    public int Id => Device.Id;
    public string Name => Device.Name;
    public byte SlaveId => Device.SlaveId;
    public TransportType TransportType => Device.TransportType;

    public string TransportTypeDisplay => Device.TransportType switch
    {
        TransportType.Tcp       => "TCP",
        TransportType.Rtu       => "RTU",
        TransportType.MqttCloud => "MQTT",
        _                       => Device.TransportType.ToString().ToUpperInvariant()
    };

    public string ModelDisplayName => Device.DeviceModel?.Name ?? "Modelo desconhecido";

    public string SlaveIdText => $"Addr {Device.SlaveId}";

    public string? FirmwareText => Device.FirmwareVersion.HasValue
        ? $"v{Device.FirmwareVersion.Value / 10}.{Device.FirmwareVersion.Value % 10}"
        : null;

    public string LastSeenText => LastSeenAt.HasValue
        ? LastSeenAt.Value.ToString("HH:mm:ss")
        : "nunca";

    public string ConnectionAddress => Device.TransportType switch
    {
        TransportType.Tcp       => Device.Tcp?.IpAddress ?? "—",
        TransportType.MqttCloud => Device.Mqtt?.BrokerHost ?? "—",
        TransportType.Rtu       => Device.Rtu?.PortName ?? "—",
        _                       => "—"
    };

    public string StatusText => IsConnected ? "Conectado" : "Desconectado";
}
