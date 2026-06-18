using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Modbus.Core.Cloud;
using Modbus.Core.Domain.Repositories;
using Modbus.Core.Polling;
using Modbus.Core.Services;
using Modbus.Core.Services.Scanning;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Modbus.Desktop.ViewModels;

public partial class DeviceListViewModel : ObservableObject
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IDeviceModelRepository _deviceModelRepository;
    private readonly IRegisterValueRepository _registerValueRepository;
    private readonly IPollingEngine _pollingEngine;
    private readonly IDeviceConfigService _configService;
    private readonly IMassMemoryService _massMemoryService;
    private readonly IDeviceScanService _scanService;
    private readonly IModbusServiceFactory _serviceFactory;
    private readonly ICloudCommandService _cloudCommandService;
    private readonly SettingsViewModel _settingsViewModel;
    private bool _pollingStarted;

    public event EventHandler<object>? NavigationRequested;

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<DeviceItemViewModel> Devices { get; } = new();

    public DeviceListViewModel(
        IDeviceRepository deviceRepository,
        IDeviceModelRepository deviceModelRepository,
        IRegisterValueRepository registerValueRepository,
        IPollingEngine pollingEngine,
        IDeviceConfigService configService,
        IMassMemoryService massMemoryService,
        IDeviceScanService scanService,
        IModbusServiceFactory serviceFactory,
        ICloudCommandService cloudCommandService,
        SettingsViewModel settingsViewModel)
    {
        _deviceRepository        = deviceRepository;
        _deviceModelRepository   = deviceModelRepository;
        _registerValueRepository = registerValueRepository;
        _pollingEngine           = pollingEngine;
        _configService           = configService;
        _massMemoryService       = massMemoryService;
        _scanService             = scanService;
        _serviceFactory          = serviceFactory;
        _cloudCommandService     = cloudCommandService;
        _settingsViewModel       = settingsViewModel;

        _pollingEngine.RegisterValuesUpdated += OnRegisterValuesUpdated;
        _pollingEngine.DeviceConnectionFailed += OnDeviceConnectionFailed;

        _ = LoadDevicesAsync();
    }

    [RelayCommand]
    internal async Task LoadDevicesAsync()
    {
        IsLoading = true;
        foreach (var d in Devices) d.Dispose();
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
    private async Task DeleteDeviceAsync(DeviceItemViewModel device)
    {
        _pollingEngine.RemoveDevice(device.Id);
        await _deviceRepository.DeleteAsync(device.Id);
        device.Dispose();
        Devices.Remove(device);
    }

    [RelayCommand]
    private void OpenDeviceDetail(DeviceItemViewModel device)
    {
        var hub = new DeviceHubViewModel(device, _registerValueRepository, _pollingEngine, _configService, _massMemoryService, _deviceRepository, _cloudCommandService, this);
        hub.NavigationRequested += (_, vm) => NavigationRequested?.Invoke(this, vm);
        NavigationRequested?.Invoke(this, hub);
    }

    [RelayCommand]
    private void OpenAddDevice()
    {
        var vm = new AddDeviceViewModel(_scanService, _deviceRepository, _deviceModelRepository, _serviceFactory, _configService, this);
        NavigationRequested?.Invoke(this, vm);
    }

    internal void NavigateBack()       => NavigationRequested?.Invoke(this, this);
    internal void NavigateToSettings() => NavigationRequested?.Invoke(this, _settingsViewModel);

    internal Task SuspendRtuPollingAsync() => _pollingEngine.SuspendRtuPollingAsync();
    internal void ResumeRtuPolling()       => _pollingEngine.ResumeRtuPolling();

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
