using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Modbus.Core.Domain.Enums;
using Modbus.Core.Domain.Repositories;
using Modbus.Core.Polling;
using Modbus.Core.Services;
using System;

namespace Modbus.Desktop.ViewModels;

public partial class DeviceHubViewModel : ObservableObject
{
    private readonly IRegisterValueRepository _registerValueRepository;
    private readonly IPollingEngine _pollingEngine;
    private readonly IDeviceConfigService _configService;
    private readonly IDeviceRepository _deviceRepository;
    private readonly DeviceListViewModel _parent;

    public event EventHandler<object>? NavigationRequested;

    public DeviceItemViewModel Device { get; }

    public DeviceHubViewModel(
        DeviceItemViewModel device,
        IRegisterValueRepository registerValueRepository,
        IPollingEngine pollingEngine,
        IDeviceConfigService configService,
        IDeviceRepository deviceRepository,
        DeviceListViewModel parent)
    {
        Device = device;
        _registerValueRepository = registerValueRepository;
        _pollingEngine = pollingEngine;
        _configService = configService;
        _deviceRepository = deviceRepository;
        _parent = parent;
    }

    [RelayCommand]
    private void GoBack() => _parent.NavigateBack();

    [RelayCommand]
    private void OpenReadings()
    {
        bool isRtu = Device.Device.TransportType == TransportType.Rtu;
        var detail = new DeviceDetailViewModel(
            Device,
            _registerValueRepository,
            _pollingEngine,
            configService: _configService,
            pausePolling:  isRtu
                ? () => _pollingEngine.SuspendRtuPollingAsync()
                : () => _pollingEngine.AcquireDeviceLockAsync(Device.Id),
            resumePolling: isRtu
                ? () => _pollingEngine.ResumeRtuPolling()
                : () => _pollingEngine.ReleaseDeviceLock(Device.Id),
            onGoBack: () => NavigationRequested?.Invoke(this, this));

        _ = detail.LoadValuesAsync();
        NavigationRequested?.Invoke(this, detail);
    }

    [RelayCommand]
    private void OpenConfigure()
    {
        bool isRtu = Device.Device.TransportType == Modbus.Core.Domain.Enums.TransportType.Rtu;
        var configure = new DeviceConfigureViewModel(
            Device,
            _configService,
            _deviceRepository,
            pausePolling:  isRtu
                ? () => _pollingEngine.SuspendRtuPollingAsync()
                : () => _pollingEngine.AcquireDeviceLockAsync(Device.Id),
            resumePolling: isRtu
                ? () => _pollingEngine.ResumeRtuPolling()
                : () => _pollingEngine.ReleaseDeviceLock(Device.Id),
            onGoBack:      () => NavigationRequested?.Invoke(this, this));

        _ = configure.LoadAsync();
        NavigationRequested?.Invoke(this, configure);
    }
}
