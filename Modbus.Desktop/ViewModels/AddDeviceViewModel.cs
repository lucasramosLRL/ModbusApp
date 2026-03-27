using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Modbus.Core.Domain.Entities;
using Modbus.Core.Domain.Repositories;
using Modbus.Core.Domain.ValueObjects;
using Modbus.Core.Services.Scanning;
using System;
using System.Collections.ObjectModel;
using Modbus.Desktop.Infrastructure;
using System.Threading;
using System.Threading.Tasks;
using Parity = Modbus.Core.Domain.Enums.Parity;
using StopBits = Modbus.Core.Domain.Enums.StopBits;
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
    }

    partial void OnIsTcpChanged(bool value)
    {
        if (value && SelectedTransport != TransportType.Tcp)
            SelectedTransport = TransportType.Tcp;
    }

    // ── RTU parameters ────────────────────────────────────────────────────────

    public ObservableCollection<string> AvailablePorts { get; } = new(SerialPortScanner.GetPortNames());

    [RelayCommand]
    private void RefreshPorts()
    {
        AvailablePorts.Clear();
        foreach (var p in SerialPortScanner.GetPortNames())
            AvailablePorts.Add(p);
        if (SelectedPort is null && AvailablePorts.Count > 0)
            SelectedPort = AvailablePorts[0];
    }
    public int[] AvailableBaudRates { get; } = { 300, 600, 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200 };
    public Parity[] AvailableParities { get; } = (Parity[])Enum.GetValues(typeof(Parity));
    public StopBits[] AvailableStopBits { get; } = (StopBits[])Enum.GetValues(typeof(StopBits));

    [ObservableProperty]
    private string? _selectedPort;

    [ObservableProperty]
    private int _selectedBaudRate = 9600;

    [ObservableProperty]
    private Parity _selectedParity = Parity.None;

    [ObservableProperty]
    private StopBits _selectedStopBits = StopBits.One;

    [ObservableProperty]
    private int _dataBits = 8;

    [ObservableProperty]
    private byte _startAddress = 1;

    [ObservableProperty]
    private byte _endAddress = 247;

    // ── TCP parameters ────────────────────────────────────────────────────────

    [ObservableProperty]
    private int _tcpPort = 502;

    // ── Scan state ────────────────────────────────────────────────────────────

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _scanStatus = "Ready";

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

    // For TCP manual/auto entry
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

    partial void OnIsSavingChanged(bool value)
    {
        SaveCommand.NotifyCanExecuteChanged();
    }

    partial void OnDeviceNameChanged(string value)
    {
        SaveCommand.NotifyCanExecuteChanged();
    }

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
            ScanStatus = $"Scanning {p.CurrentLabel} ({p.Current}/{p.Total})";
        });

        try
        {
            if (IsRtu)
            {
                if (string.IsNullOrEmpty(SelectedPort))
                {
                    ScanStatus = "Select a COM port first.";
                    return;
                }

                var config = new RtuConfig
                {
                    PortName = SelectedPort,
                    BaudRate = SelectedBaudRate,
                    DataBits = DataBits,
                    Parity = SelectedParity,
                    StopBits = SelectedStopBits
                };

                await foreach (var result in _scanService.ScanRtuAsync(
                    config, StartAddress, EndAddress, progress, token))
                {
                    var vm = new ScanResultViewModel(result);
                    await Dispatcher.UIThread.InvokeAsync(() => ScanResults.Add(vm));
                }
            }
            else
            {
                await foreach (var result in _scanService.ScanTcpAsync(
                    TcpPort, progress, token))
                {
                    var vm = new ScanResultViewModel(result);
                    await Dispatcher.UIThread.InvokeAsync(() => ScanResults.Add(vm));
                }
            }

            ScanStatus = ScanResults.Count > 0
                ? $"Done — {ScanResults.Count} device(s) found."
                : "Scan complete. No devices responded.";
        }
        catch (OperationCanceledException)
        {
            ScanStatus = "Scan cancelled.";
        }
        catch (Exception ex)
        {
            ScanStatus = $"Error: {ex.Message}";
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
            int? deviceModelId = null;
            if (SelectedResult?.Result.ModelName is { } modelName)
            {
                var model = await _deviceModelRepository.GetByNameAsync(modelName);
                deviceModelId = model?.Id;
            }

            var device = new ModbusDevice
            {
                Name = DeviceName,
                SlaveId = SlaveId,
                TransportType = SelectedTransport,
                IsActive = true,
                DeviceModelId = deviceModelId
            };

            if (SelectedTransport == TransportType.Rtu)
            {
                device.Rtu = SelectedResult?.Result.Rtu ?? new RtuConfig
                {
                    PortName = SelectedPort ?? "",
                    BaudRate = SelectedBaudRate,
                    DataBits = DataBits,
                    Parity = SelectedParity,
                    StopBits = SelectedStopBits
                };
            }
            else
            {
                device.Tcp = SelectedResult?.Result.Tcp ?? new TcpConfig
                {
                    IpAddress = DeviceIp,
                    Port = TcpPort
                };
            }

            await _deviceRepository.AddAsync(device);
            await _parent.LoadDevicesAsync();
            _parent.NavigateBack();
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

    // ── Constructor ───────────────────────────────────────────────────────────

    public AddDeviceViewModel(
        IDeviceScanService scanService,
        IDeviceRepository deviceRepository,
        IDeviceModelRepository deviceModelRepository,
        DeviceListViewModel parent)
    {
        _scanService = scanService;
        _deviceRepository = deviceRepository;
        _deviceModelRepository = deviceModelRepository;
        _parent = parent;

        // Pre-select first available port
        if (AvailablePorts.Count > 0)
            SelectedPort = AvailablePorts[0];
    }
}
