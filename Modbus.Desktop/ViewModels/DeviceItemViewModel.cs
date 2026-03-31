using CommunityToolkit.Mvvm.ComponentModel;
using Modbus.Core.Domain.Entities;
using Modbus.Core.Domain.Enums;
using System;

namespace Modbus.Desktop.ViewModels;

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
    public string? ModelName => Device.DeviceModel?.Name;

    public string ConnectionAddress => Device.TransportType == TransportType.Tcp
        ? Device.Tcp?.IpAddress ?? "—"
        : Device.Rtu?.PortName ?? "—";

    public string StatusText => IsConnected ? "Connected" : "Disconnected";
}
