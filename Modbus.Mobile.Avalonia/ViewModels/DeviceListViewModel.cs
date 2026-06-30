using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Modbus.Core.Domain.Repositories;
using Modbus.Core.Polling;
using Modbus.Core.Services;
using Modbus.Core.Services.Scanning;
using Modbus.Mobile.Avalonia.Services;

namespace Modbus.Mobile.Avalonia.ViewModels;

public partial class DeviceListViewModel : ViewModelBase
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IDeviceModelRepository _deviceModelRepository;
    private readonly IModbusServiceFactory _serviceFactory;
    private readonly IPollingEngine _pollingEngine;
    private readonly IDeviceScanService _scanService;
    private readonly INetworkScanLock _scanLock;
    private bool _pollingStarted;

    public event EventHandler<object>? NavigationRequested;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingDelete))]
    private DeviceItemViewModel? _pendingDelete;

    public bool HasPendingDelete => PendingDelete is not null;

    public ObservableCollection<DeviceItemViewModel> Devices { get; } = new();

    public DeviceListViewModel(
        IDeviceRepository deviceRepository,
        IDeviceModelRepository deviceModelRepository,
        IModbusServiceFactory serviceFactory,
        IPollingEngine pollingEngine,
        IDeviceScanService scanService,
        INetworkScanLock scanLock)
    {
        _deviceRepository      = deviceRepository;
        _deviceModelRepository = deviceModelRepository;
        _serviceFactory        = serviceFactory;
        _pollingEngine         = pollingEngine;
        _scanService           = scanService;
        _scanLock              = scanLock;

        _pollingEngine.RegisterValuesUpdated += OnRegisterValuesUpdated;
        _pollingEngine.DeviceConnectionFailed += OnDeviceConnectionFailed;

        _ = LoadDevicesAsync();
    }

    [RelayCommand]
    internal async Task LoadDevicesAsync()
    {
        IsLoading = true;
        Devices.Clear();

        try
        {
            var devices = await _deviceRepository.GetAllAsync();
            foreach (var device in devices)
            {
                Devices.Add(new DeviceItemViewModel(device));
                _pollingEngine.AddDevice(device);
            }

            if (!_pollingStarted)
            {
                await _pollingEngine.StartAsync();
                _pollingStarted = true;
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void OpenAddDevice()
    {
        var vm = new AddDeviceViewModel(
            _scanService, _deviceRepository, _deviceModelRepository,
            _serviceFactory, _scanLock, this);
        NavigationRequested?.Invoke(this, vm);
    }

    // ── Delete (in-page confirmation overlay) ──────────────────────────────────

    [RelayCommand]
    private void RequestDelete(DeviceItemViewModel device) => PendingDelete = device;

    [RelayCommand]
    private void CancelDelete() => PendingDelete = null;

    [RelayCommand]
    private async Task ConfirmDeleteAsync()
    {
        var device = PendingDelete;
        if (device is null) return;
        PendingDelete = null;

        _pollingEngine.RemoveDevice(device.Id);
        await _deviceRepository.DeleteAsync(device.Id);
        Devices.Remove(device);
    }

    internal void NavigateBack() => NavigationRequested?.Invoke(this, this);

    private void OnRegisterValuesUpdated(object? sender, RegisterValuesUpdatedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            var vm = FindDevice(e.Device.Id);
            if (vm is null) return;
            vm.IsConnected = true;
            vm.HasError = false;
            vm.LastSeenAt = e.Timestamp;
        });
    }

    private void OnDeviceConnectionFailed(object? sender, DeviceConnectionFailedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            var vm = FindDevice(e.Device.Id);
            if (vm is null) return;
            vm.IsConnected = false;
            vm.HasError = true;
            vm.ErrorMessage = e.Exception.Message;
        });
    }

    private DeviceItemViewModel? FindDevice(int id)
    {
        foreach (var d in Devices)
            if (d.Id == id) return d;
        return null;
    }
}
