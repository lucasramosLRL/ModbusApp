using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Modbus.Core.Domain.Enums;
using Modbus.Core.Domain.Repositories;
using Modbus.Core.Polling;
using Modbus.Core.Services;
using System;
using System.Threading.Tasks;

namespace Modbus.Desktop.ViewModels;

public partial class DeviceHubViewModel : ObservableObject
{
    private readonly IRegisterValueRepository _registerValueRepository;
    private readonly IPollingEngine _pollingEngine;
    private readonly IDeviceConfigService _configService;
    private readonly IDeviceRepository _deviceRepository;
    private readonly DeviceListViewModel _parent;

    // Cached per-hub: null = not yet read; read once on first open that needs it.
    private ushort? _inOutCfg;
    private bool _inOutCfgLoaded;

    private ushort? _calibCfg;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenMassMemoryCommand))]
    [NotifyPropertyChangedFor(nameof(MassMemoryCardOpacity))]
    private bool _isMassMemoryEnabled;

    public double MassMemoryCardOpacity => IsMassMemoryEnabled ? 1.0 : 0.45;

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

        _ = LoadCapabilitiesAsync();
    }

    [RelayCommand]
    private void GoBack() => _parent.NavigateBack();

    [RelayCommand]
    private void OpenReadings() => _ = OpenReadingsAsync();

    private async Task OpenReadingsAsync()
    {
        bool isRtu = Device.Device.TransportType == TransportType.Rtu;
        ushort inOutCfg = await LoadInOutCfgIfNeededAsync(isRtu);

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
            onGoBack:  () => NavigationRequested?.Invoke(this, this),
            inOutCfg:  inOutCfg);

        _ = detail.LoadValuesAsync();
        NavigationRequested?.Invoke(this, detail);
    }

    [RelayCommand]
    private void OpenConfigure() => _ = OpenConfigureAsync();

    private async Task OpenConfigureAsync()
    {
        bool isRtu = Device.Device.TransportType == TransportType.Rtu;
        ushort inOutCfg = await LoadInOutCfgIfNeededAsync(isRtu);

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
            onGoBack:  () => NavigationRequested?.Invoke(this, this),
            inOutCfg:  inOutCfg);

        _ = configure.LoadAsync();
        NavigationRequested?.Invoke(this, configure);
    }

    [RelayCommand(CanExecute = nameof(IsMassMemoryEnabled))]
    private void OpenMassMemory()
    {
        bool isRtu = Device.Device.TransportType == TransportType.Rtu;

        var massMemory = new MassMemoryViewModel(
            Device,
            _configService,
            pausePolling:  isRtu
                ? () => _pollingEngine.SuspendRtuPollingAsync()
                : () => _pollingEngine.AcquireDeviceLockAsync(Device.Id),
            resumePolling: isRtu
                ? () => _pollingEngine.ResumeRtuPolling()
                : () => _pollingEngine.ReleaseDeviceLock(Device.Id),
            onGoBack: () => NavigationRequested?.Invoke(this, this));

        NavigationRequested?.Invoke(this, massMemory);
        _ = massMemory.LoadAsync();
    }

    /// <summary>
    /// Reads CalibCfg (byte positions 56-57) via FC 0x79 to determine device capabilities.
    /// D3=0 → mass memory enabled; null (TCP or failure) → assume enabled.
    /// Fires once at hub construction; IsMassMemoryEnabled is set when the read completes.
    /// </summary>
    private async Task LoadCapabilitiesAsync()
    {
        bool isRtu = Device.Device.TransportType == TransportType.Rtu;

        if (isRtu) await _pollingEngine.SuspendRtuPollingAsync();
        try
        {
            _calibCfg = await _configService.ReadCalibCfgAsync(Device.Device);
        }
        catch
        {
            _calibCfg = null;
        }
        finally
        {
            if (isRtu) _pollingEngine.ResumeRtuPolling();
        }

        // D3 = 0 → MM habilitada; null (TCP ou falha) → assume habilitada
        IsMassMemoryEnabled = _calibCfg == null || (_calibCfg.Value & (1 << 3)) == 0;
    }

    /// <summary>
    /// Reads InOutCfg via FC 0x79 the first time this hub needs it, then caches the result.
    /// For KS-3000 (fixed I/O), returns the hardcoded bitmask without a device read.
    /// Returns the fallback bitmask (all enabled) if the read fails or device is TCP-only.
    /// </summary>
    private async Task<ushort> LoadInOutCfgIfNeededAsync(bool isRtu)
    {
        if (_inOutCfgLoaded)
            return _inOutCfg ?? InOutCfgFallback();

        _inOutCfgLoaded = true;

        var caps = Modbus.Core.Services.DeviceCapabilityRegistry.Get(Device.Device.DeviceModel?.DeviceCode);
        if (!caps.HasFlag(DeviceCapabilities.ConfigurableIo))
        {
            // Fixed I/O model — derive the bitmask from its capabilities without device read.
            _inOutCfg = FixedInOutCfg(Device.Device.DeviceModel?.DeviceCode);
            return _inOutCfg.Value;
        }

        // Configurable I/O — read from device (RTU only; suspend polling while we use the port).
        if (isRtu) await _pollingEngine.SuspendRtuPollingAsync();
        try
        {
            _inOutCfg = await _configService.ReadInOutCfgAsync(Device.Device);
        }
        finally
        {
            if (isRtu) _pollingEngine.ResumeRtuPolling();
        }

        return _inOutCfg ?? InOutCfgFallback();
    }

    // KS-3000: EDP1 (bit0) + EDP2 (bit1) + SD1 (bit3) = 0x000B
    private static ushort FixedInOutCfg(byte? deviceCode) => deviceCode switch
    {
        0xF2 => 0x000B, // KS-3000: EDP1 + EDP2 + SD1
        _    => 0x001F, // unknown model → all enabled
    };

    // All five I/O channels enabled — shown when InOutCfg cannot be read.
    private static ushort InOutCfgFallback() => 0x001F;
}
