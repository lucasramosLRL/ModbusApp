using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Modbus.Core.Domain.Repositories;
using Modbus.Core.Polling;
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
    private readonly IDeviceScanService _scanService;
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
        IDeviceScanService scanService,
        SettingsViewModel settingsViewModel)
    {
        _deviceRepository    = deviceRepository;
        _deviceModelRepository = deviceModelRepository;
        _registerValueRepository = registerValueRepository;
        _pollingEngine       = pollingEngine;
        _scanService         = scanService;
        _settingsViewModel   = settingsViewModel;

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
        var detail = new DeviceDetailViewModel(device, _registerValueRepository, _pollingEngine, this);
        _ = detail.LoadValuesAsync();
        NavigationRequested?.Invoke(this, detail);
    }

    [RelayCommand]
    private void OpenAddDevice()
    {
        var vm = new AddDeviceViewModel(_scanService, _deviceRepository, _deviceModelRepository, this);
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
