using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Modbus.Core.Domain.Entities;
using Modbus.Core.Domain.Repositories;
using Modbus.Core.Domain.ValueObjects;
using Modbus.Core.Services;
using Modbus.Core.Services.Scanning;
using Modbus.Desktop.Infrastructure;
using Modbus.Desktop.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using TransportType = Modbus.Core.Domain.Enums.TransportType;

namespace Modbus.Desktop.ViewModels;

public partial class AddDeviceViewModel : ObservableObject
{
    private readonly IDeviceScanService _scanService;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IDeviceModelRepository _deviceModelRepository;
    private readonly IModbusServiceFactory _serviceFactory;
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

        if (IsScanning)
            _scanCts?.Cancel();

        ScanResults.Clear();
        SelectedResult = null;
        FoundCount = 0;
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
        if (value)
            SlaveId = 255;
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

    public ObservableCollection<ScanResultViewModel> ScanResults { get; } = [];

    // ── Selected result / form ────────────────────────────────────────────────

    [ObservableProperty]
    private ScanResultViewModel? _selectedResult;

    private bool _applyingResult;

    partial void OnSelectedResultChanged(ScanResultViewModel? value)
    {
        if (value is null) return;
        _applyingResult = true;
        DeviceName = value.Result.SuggestedName;
        if (!IsTcp)
            SlaveId = value.Result.SlaveId;
        if (value.Result.Tcp is not null)
            DeviceIp = value.Result.Tcp.IpAddress;
        _applyingResult = false;
    }

    [ObservableProperty]
    private string _deviceName = "";

    [ObservableProperty]
    private byte _slaveId = 1;

    partial void OnSlaveIdChanged(byte value)
    {
        if (!_applyingResult && SelectedResult is not null && value != SelectedResult.Result.SlaveId)
            SelectedResult = null;
    }

    [ObservableProperty]
    private string _deviceIp = "";

    partial void OnDeviceIpChanged(string value)
    {
        if (!_applyingResult && SelectedResult is not null &&
            value != (SelectedResult.Result.Tcp?.IpAddress ?? ""))
            SelectedResult = null;
    }

    // ── Save state ────────────────────────────────────────────────────────────

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SaveFeedbackColorHex))]
    private bool _saveFeedbackIsError;

    [ObservableProperty]
    private string? _saveFeedback;

    public string SaveFeedbackColorHex => SaveFeedbackIsError ? "#D32F2F" : "#388E3C";

    private CancellationTokenSource? _feedbackCts;

    private void SetFeedback(string message, bool isError)
    {
        _feedbackCts?.Cancel();
        _feedbackCts?.Dispose();
        _feedbackCts = new CancellationTokenSource();
        SaveFeedbackIsError = isError;
        SaveFeedback = message;
        _ = ClearFeedbackAfterDelayAsync(_feedbackCts.Token);
    }

    private void ClearFeedback()
    {
        _feedbackCts?.Cancel();
        _feedbackCts?.Dispose();
        _feedbackCts = null;
        SaveFeedback = null;
    }

    private async Task ClearFeedbackAfterDelayAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(4000, token);
            SaveFeedback = null;
        }
        catch (OperationCanceledException) { }
    }

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
        ClearFeedback();
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
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            if (!token.IsCancellationRequested)
                                ScanResults.Add(vm);
                        });
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
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (!token.IsCancellationRequested)
                            ScanResults.Add(vm);
                    });
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
        ClearFeedback();
        var loc = LocalizationService.Instance;

        try
        {
            string effectiveIp = IsTcp ? (SelectedResult?.Result.Tcp?.IpAddress ?? DeviceIp) : "";
            uint? serialNumber;

            if (SelectedResult is not null)
            {
                // Scan path: serial already read, just check duplicates
                serialNumber = SelectedResult.Result.SerialNumber;

                if (serialNumber.HasValue &&
                    await _deviceRepository.ExistsBySerialNumberAsync(serialNumber.Value))
                {
                    SetFeedback(string.Format(loc["DuplicateSerial"], serialNumber.Value), isError: true);
                    return;
                }
                if (IsRtu && await _deviceRepository.ExistsByRtuSlaveIdAsync(SlaveId))
                {
                    SetFeedback(string.Format(loc["DuplicateRtuAddress"], SlaveId), isError: true);
                    return;
                }
                if (IsTcp && await _deviceRepository.ExistsByTcpIpAsync(effectiveIp))
                {
                    SetFeedback(string.Format(loc["DuplicateIp"], effectiveIp), isError: true);
                    return;
                }
            }
            else
            {
                // Manual path: check address duplicates first, then connect to read serial
                if (IsRtu && await _deviceRepository.ExistsByRtuSlaveIdAsync(SlaveId))
                {
                    SetFeedback(string.Format(loc["DuplicateRtuAddress"], SlaveId), isError: true);
                    return;
                }
                if (IsTcp && await _deviceRepository.ExistsByTcpIpAsync(effectiveIp))
                {
                    SetFeedback(string.Format(loc["DuplicateIp"], effectiveIp), isError: true);
                    return;
                }

                serialNumber = await ProbeSerialNumberAsync(effectiveIp);
                if (serialNumber is null)
                {
                    SetFeedback(loc["ConnectFailed"], isError: true);
                    return;
                }

                if (await _deviceRepository.ExistsBySerialNumberAsync(serialNumber.Value))
                {
                    SetFeedback(string.Format(loc["DuplicateSerial"], serialNumber.Value), isError: true);
                    return;
                }
            }

            int? deviceModelId = null;
            if (SelectedResult?.Result.ModelName is { } modelName)
            {
                var model = await _deviceModelRepository.GetByNameAsync(modelName);
                deviceModelId = model?.Id;
            }

            var device = new ModbusDevice
            {
                Name            = DeviceName,
                SlaveId         = SlaveId,
                TransportType   = SelectedTransport,
                SerialNumber    = serialNumber,
                FirmwareVersion = SelectedResult?.Result.FirmwareVersion,
                IsActive        = true,
                DeviceModelId   = deviceModelId
            };

            if (SelectedTransport == TransportType.Rtu)
                device.Rtu = RtuSettingsService.Instance.ToRtuConfig();
            else
                device.Tcp = new TcpConfig
                {
                    IpAddress = effectiveIp,
                    Port      = SelectedResult?.Result.Tcp?.Port ?? TcpPort
                };

            await _deviceRepository.AddAsync(device);
            await _parent.LoadDevicesAsync();

            DeviceName     = "";
            SelectedResult = null;
            ScanStatus     = loc["DeviceSaved"];
            SetFeedback(loc["DeviceSaved"], isError: false);
        }
        catch (Exception ex)
        {
            SetFeedback(ex.Message, isError: true);
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>Connects to the device, reads the NS register (FC04 addr 0) and returns the serial number, or null on failure.</summary>
    private async Task<uint?> ProbeSerialNumberAsync(string effectiveIp)
    {
        var tempDevice = SelectedTransport == TransportType.Rtu
            ? new ModbusDevice { Name = "", SlaveId = SlaveId, TransportType = TransportType.Rtu, Rtu = RtuSettingsService.Instance.ToRtuConfig() }
            : new ModbusDevice { Name = "", SlaveId = SlaveId, TransportType = TransportType.Tcp, Tcp = new TcpConfig { IpAddress = effectiveIp, Port = TcpPort } };

        bool rtuSuspended = false;
        IModbusService? svc = null;
        try
        {
            if (IsRtu)
            {
                await _parent.SuspendRtuPollingAsync();
                rtuSuspended = true;
            }

            svc = _serviceFactory.Create(tempDevice);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await svc.ConnectAsync(cts.Token);
            var words = await svc.ReadInputRegistersAsync(SlaveId, 0, 2, cts.Token);
            return (uint)((words[0] << 16) | words[1]);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (svc is not null) { try { await svc.DisconnectAsync(); } catch { } svc.Dispose(); }
            if (rtuSuspended) _parent.ResumeRtuPolling();
        }
    }

    private bool CanSave() => !IsScanning && !IsSaving && !string.IsNullOrWhiteSpace(DeviceName);

    [RelayCommand]
    private void GoBack()
    {
        _scanCts?.Cancel();
        _parent.NavigateBack();
    }

    [RelayCommand]
    private void GoToSettings() => _parent.NavigateToSettings();

    // ── Constructor ───────────────────────────────────────────────────────────

    public AddDeviceViewModel(
        IDeviceScanService scanService,
        IDeviceRepository deviceRepository,
        IDeviceModelRepository deviceModelRepository,
        IModbusServiceFactory serviceFactory,
        DeviceListViewModel parent)
    {
        _scanService           = scanService;
        _deviceRepository      = deviceRepository;
        _deviceModelRepository = deviceModelRepository;
        _serviceFactory        = serviceFactory;
        _parent                = parent;

        RtuSettingsService.Instance.PropertyChanged += (_, _) =>
            OnPropertyChanged(nameof(IsRtuPortUnavailable));
    }
}
