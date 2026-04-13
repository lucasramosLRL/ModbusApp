using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Modbus.Core.Domain.Entities;
using Modbus.Core.Domain.Repositories;
using Modbus.Core.Domain.ValueObjects;
using Modbus.Core.Services.Scanning;
using Modbus.Desktop.Infrastructure;
using Modbus.Desktop.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TransportType = Modbus.Core.Domain.Enums.TransportType;

namespace Modbus.Desktop.ViewModels;

public partial class AddDeviceViewModel : ObservableObject
{
    private readonly IDeviceScanService _scanService;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IDeviceModelRepository _deviceModelRepository;
    private readonly DeviceListViewModel _parent;
    private CancellationTokenSource? _scanCts;

    // ── Transport ─────────────────────────────────────────────────────────────

    [ObservableProperty]
    private TransportType _selectedTransport = TransportType.Rtu;

    [ObservableProperty]
    private bool _isRtu = true;

    [ObservableProperty]
    private bool _isTcp = false;

    partial void OnSelectedTransportChanged(TransportType value)
    {
        IsRtu = value == TransportType.Rtu;
        IsTcp = value == TransportType.Tcp;
    }

    partial void OnIsRtuChanged(bool value)
    {
        if (value && SelectedTransport != TransportType.Rtu)
            SelectedTransport = TransportType.Rtu;
        OnPropertyChanged(nameof(IsRtuPortUnavailable));
    }

    partial void OnIsTcpChanged(bool value)
    {
        if (value && SelectedTransport != TransportType.Tcp)
            SelectedTransport = TransportType.Tcp;
    }

    // ── RTU address range ─────────────────────────────────────────────────────

    [ObservableProperty]
    private byte _startAddress = 1;

    [ObservableProperty]
    private byte _endAddress = 247;

    public bool IsRtuPortUnavailable =>
        IsRtu &&
        !string.IsNullOrEmpty(RtuSettingsService.Instance.PortName) &&
        !SerialPortScanner.GetPortNames().Contains(RtuSettingsService.Instance.PortName,
            StringComparer.OrdinalIgnoreCase);

    // ── TCP parameters ────────────────────────────────────────────────────────

    [ObservableProperty]
    private int _tcpPort = 502;

    // ── Scan state ────────────────────────────────────────────────────────────

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _scanStatus = LocalizationService.Instance["Ready"];

    [ObservableProperty]
    private int _scanProgress;

    [ObservableProperty]
    private int _scanTotal = 1;

    [ObservableProperty]
    private int _foundCount;

    public ObservableCollection<ScanResultViewModel> ScanResults { get; } = new();

    // ── Selected result / form ────────────────────────────────────────────────

    [ObservableProperty]
    private ScanResultViewModel? _selectedResult;

    partial void OnSelectedResultChanged(ScanResultViewModel? value)
    {
        if (value is null) return;
        DeviceName = value.Result.SuggestedName;
        SlaveId = value.Result.SlaveId;
        if (value.Result.Tcp is not null)
            DeviceIp = value.Result.Tcp.IpAddress;
    }

    [ObservableProperty]
    private string _deviceName = "";

    [ObservableProperty]
    private byte _slaveId = 1;

    [ObservableProperty]
    private string _deviceIp = "";

    // ── Save state ────────────────────────────────────────────────────────────

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string? _saveError;

    // ── Commands ──────────────────────────────────────────────────────────────

    partial void OnIsScanningChanged(bool value)
    {
        ScanCommand.NotifyCanExecuteChanged();
        CancelScanCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsSavingChanged(bool value)   => SaveCommand.NotifyCanExecuteChanged();
    partial void OnDeviceNameChanged(string value) => SaveCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        ScanResults.Clear();
        ScanProgress = 0;
        ScanTotal = 1;
        FoundCount = 0;
        SaveError = null;
        IsScanning = true;

        _scanCts = new CancellationTokenSource();
        var token = _scanCts.Token;

        var progress = new Progress<ScanProgress>(p =>
        {
            ScanProgress = p.Current;
            ScanTotal = p.Total;
            FoundCount = p.Found;
            ScanStatus = string.Format(LocalizationService.Instance["ScanningProgress"], p.CurrentLabel, p.Current, p.Total);
        });

        try
        {
            if (IsRtu)
            {
                var config = RtuSettingsService.Instance.ToRtuConfig();
                if (string.IsNullOrEmpty(config.PortName))
                {
                    ScanStatus = LocalizationService.Instance["SelectComPortFirst"];
                    return;
                }

                await _parent.SuspendRtuPollingAsync();
                try
                {
                    await foreach (var result in _scanService.ScanRtuAsync(
                        config, StartAddress, EndAddress, progress, token))
                    {
                        var vm = new ScanResultViewModel(result);
                        await Dispatcher.UIThread.InvokeAsync(() => ScanResults.Add(vm));
                    }
                }
                finally
                {
                    _parent.ResumeRtuPolling();
                }
            }
            else
            {
                await foreach (var result in _scanService.ScanTcpAsync(progress, token))
                {
                    var vm = new ScanResultViewModel(result);
                    await Dispatcher.UIThread.InvokeAsync(() => ScanResults.Add(vm));
                }
            }

            var loc = LocalizationService.Instance;
            ScanStatus = ScanResults.Count > 0
                ? string.Format(loc["ScanDone"], ScanResults.Count)
                : loc["ScanNoResponse"];
        }
        catch (OperationCanceledException)
        {
            ScanStatus = LocalizationService.Instance["ScanCancelled"];
        }
        catch (Exception ex)
        {
            ScanStatus = string.Format(LocalizationService.Instance["ErrorPrefix"], ex.Message);
        }
        finally
        {
            IsScanning = false;
            _scanCts?.Dispose();
            _scanCts = null;
        }
    }

    private bool CanScan() => !IsScanning;

    [RelayCommand(CanExecute = nameof(CanCancelScan))]
    private void CancelScan() => _scanCts?.Cancel();

    private bool CanCancelScan() => IsScanning;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        IsSaving = true;
        SaveError = null;

        try
        {
            var serialNumber = SelectedResult?.Result.SerialNumber;

            if (serialNumber.HasValue &&
                await _deviceRepository.ExistsBySerialNumberAsync(serialNumber.Value))
            {
                SaveError = string.Format(LocalizationService.Instance["DuplicateSerial"], serialNumber.Value);
                return;
            }

            int? deviceModelId = null;
            if (SelectedResult?.Result.ModelName is { } modelName)
            {
                var model = await _deviceModelRepository.GetByNameAsync(modelName);
                deviceModelId = model?.Id;
            }

            var device = new ModbusDevice
            {
                Name          = DeviceName,
                SlaveId       = SlaveId,
                TransportType = SelectedTransport,
                SerialNumber  = serialNumber,
                IsActive      = true,
                DeviceModelId = deviceModelId
            };

            if (SelectedTransport == TransportType.Rtu)
            {
                device.Rtu = RtuSettingsService.Instance.ToRtuConfig();
            }
            else
            {
                var src = SelectedResult?.Result.Tcp;
                device.Tcp = new TcpConfig
                {
                    IpAddress = src?.IpAddress ?? DeviceIp,
                    Port      = src?.Port ?? TcpPort
                };
            }

            await _deviceRepository.AddAsync(device);
            await _parent.LoadDevicesAsync();

            DeviceName    = "";
            SelectedResult = null;
            SaveError     = null;
            ScanStatus    = LocalizationService.Instance["DeviceSaved"];
        }
        catch (Exception ex)
        {
            SaveError = ex.Message;
        }
        finally
        {
            IsSaving = false;
        }
    }

    private bool CanSave() => !IsScanning && !IsSaving && !string.IsNullOrWhiteSpace(DeviceName);

    [RelayCommand]
    private void GoBack() => _parent.NavigateBack();

    [RelayCommand]
    private void GoToSettings() => _parent.NavigateToSettings();

    // ── Constructor ───────────────────────────────────────────────────────────

    public AddDeviceViewModel(
        IDeviceScanService scanService,
        IDeviceRepository deviceRepository,
        IDeviceModelRepository deviceModelRepository,
        DeviceListViewModel parent)
    {
        _scanService           = scanService;
        _deviceRepository      = deviceRepository;
        _deviceModelRepository = deviceModelRepository;
        _parent                = parent;

        RtuSettingsService.Instance.PropertyChanged += (_, _) =>
            OnPropertyChanged(nameof(IsRtuPortUnavailable));
    }
}
