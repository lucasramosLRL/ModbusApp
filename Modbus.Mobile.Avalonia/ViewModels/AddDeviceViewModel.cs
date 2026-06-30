using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Modbus.Core.Domain.Entities;
using Modbus.Core.Domain.Repositories;
using Modbus.Core.Domain.ValueObjects;
using Modbus.Core.Services;
using Modbus.Core.Services.Scanning;
using Modbus.Mobile.Avalonia.Services;
using TransportType = Modbus.Core.Domain.Enums.TransportType;

namespace Modbus.Mobile.Avalonia.ViewModels;

/// <summary>
/// Add-device flow for mobile: TCP (broadcast scan + manual IP) and Cloud (MQTT).
/// Adapted from the desktop AddDeviceViewModel with RTU/scan-RTU/FC42 removed and
/// LocalizationService replaced by literal PT strings.
/// </summary>
public partial class AddDeviceViewModel : ViewModelBase
{
    private readonly IDeviceScanService _scanService;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IDeviceModelRepository _deviceModelRepository;
    private readonly IModbusServiceFactory _serviceFactory;
    private readonly INetworkScanLock _scanLock;
    private readonly DeviceListViewModel _parent;
    private CancellationTokenSource? _scanCts;

    // ── Transport ──────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCloud))]
    private bool _isTcp = true;

    public bool IsCloud => !IsTcp;

    partial void OnIsTcpChanged(bool value)
    {
        ScanResults.Clear();
        SelectedResult = null;
        FoundCount = 0;
        if (value) SlaveId = 255;
    }

    // ── TCP parameters ───────────────────────────────────────────────────────────

    [ObservableProperty]
    private string _deviceIp = "";

    [ObservableProperty]
    private int _tcpPort = 502;

    [ObservableProperty]
    private byte _slaveId = 255;

    partial void OnDeviceIpChanged(string value)
    {
        if (!_applyingResult && SelectedResult is not null &&
            value != (SelectedResult.Result.Tcp?.IpAddress ?? ""))
            SelectedResult = null;
    }

    // ── Cloud (MQTT broker) parameters ───────────────────────────────────────────

    [ObservableProperty] private string _brokerHost = "";
    [ObservableProperty] private int _brokerPort = 8883;
    [ObservableProperty] private bool _brokerUseTls = true;
    [ObservableProperty] private string _brokerUsername = "";
    [ObservableProperty] private string _brokerPassword = "";
    [ObservableProperty] private string _cloudSerial = "";
    [ObservableProperty] private string _telemetryTopic = "ks";

    public ObservableCollection<DeviceModel> AvailableModels { get; } = new();

    [ObservableProperty]
    private DeviceModel? _selectedModel;

    // ── Scan state ───────────────────────────────────────────────────────────────

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _scanStatus = "Pronto";

    [ObservableProperty]
    private int _scanProgress;

    [ObservableProperty]
    private int _scanTotal = 1;

    [ObservableProperty]
    private int _foundCount;

    public ObservableCollection<ScanResultViewModel> ScanResults { get; } = new();

    private bool _applyingResult;

    [ObservableProperty]
    private ScanResultViewModel? _selectedResult;

    partial void OnSelectedResultChanged(ScanResultViewModel? value)
    {
        if (value is null) return;
        _applyingResult = true;
        DeviceName = value.Result.SuggestedName;
        if (value.Result.Tcp is not null)
            DeviceIp = value.Result.Tcp.IpAddress;
        _applyingResult = false;
    }

    // ── Form / save state ────────────────────────────────────────────────────────

    [ObservableProperty]
    private string _deviceName = "";

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SaveFeedbackColorHex))]
    private bool _saveFeedbackIsError;

    [ObservableProperty]
    private string? _saveFeedback;

    public string SaveFeedbackColorHex => SaveFeedbackIsError ? "#D32F2F" : "#388E3C";

    partial void OnIsScanningChanged(bool value)
    {
        ScanCommand.NotifyCanExecuteChanged();
        CancelScanCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsSavingChanged(bool value)     => SaveCommand.NotifyCanExecuteChanged();
    partial void OnDeviceNameChanged(string value) => SaveCommand.NotifyCanExecuteChanged();

    // ── Scan ─────────────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        ScanResults.Clear();
        ScanProgress = 0;
        ScanTotal = 1;
        FoundCount = 0;
        SaveFeedback = null;
        IsScanning = true;

        _scanCts = new CancellationTokenSource();
        var token = _scanCts.Token;

        var progress = new Progress<ScanProgress>(p =>
        {
            ScanProgress = p.Current;
            ScanTotal = p.Total;
            FoundCount = p.Found;
            ScanStatus = $"Buscando… {p.CurrentLabel} ({p.Current}/{p.Total})";
        });

        try
        {
            // Android drops broadcast replies without a held MulticastLock.
            using (_scanLock.Acquire())
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

            ScanStatus = ScanResults.Count > 0
                ? $"{ScanResults.Count} dispositivo(s) encontrado(s)"
                : "Nenhum dispositivo respondeu";
        }
        catch (OperationCanceledException)
        {
            ScanStatus = "Busca cancelada";
        }
        catch (Exception ex)
        {
            ScanStatus = "Erro: " + ex.Message;
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

    // ── Save ─────────────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        IsSaving = true;
        SaveFeedback = null;

        try
        {
            if (IsCloud)
            {
                await SaveCloudDeviceAsync();
                return;
            }

            string effectiveIp = SelectedResult?.Result.Tcp?.IpAddress ?? DeviceIp;
            if (string.IsNullOrWhiteSpace(effectiveIp))
            {
                SetFeedback("Informe o IP do dispositivo.", isError: true);
                return;
            }

            if (await _deviceRepository.ExistsByTcpIpAsync(effectiveIp))
            {
                SetFeedback($"Já existe um dispositivo com o IP {effectiveIp}.", isError: true);
                return;
            }

            uint? serialNumber = SelectedResult?.Result.SerialNumber
                                 ?? await ProbeSerialNumberAsync(effectiveIp);

            if (serialNumber is null)
            {
                SetFeedback("Falha ao conectar / ler o número de série.", isError: true);
                return;
            }

            if (await _deviceRepository.ExistsBySerialNumberAsync(serialNumber.Value))
            {
                SetFeedback($"Já existe um dispositivo com o série {serialNumber.Value}.", isError: true);
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
                Name            = DeviceName,
                SlaveId         = SlaveId,
                TransportType   = TransportType.Tcp,
                SerialNumber    = serialNumber,
                FirmwareVersion = SelectedResult?.Result.FirmwareVersion,
                IsActive        = true,
                DeviceModelId   = deviceModelId,
                Tcp             = new TcpConfig
                {
                    IpAddress = effectiveIp,
                    Port      = SelectedResult?.Result.Tcp?.Port ?? TcpPort
                }
            };

            await _deviceRepository.AddAsync(device);
            await _parent.LoadDevicesAsync();

            DeviceName = "";
            DeviceIp = "";
            SelectedResult = null;
            SetFeedback("Dispositivo salvo.", isError: false);
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

    private async Task SaveCloudDeviceAsync()
    {
        if (string.IsNullOrWhiteSpace(BrokerHost) || !uint.TryParse(CloudSerial, out var serial))
        {
            SetFeedback("Informe o broker e um número de série válido.", isError: true);
            return;
        }

        if (await _deviceRepository.ExistsBySerialNumberAsync(serial))
        {
            SetFeedback($"Já existe um dispositivo com o série {serial}.", isError: true);
            return;
        }

        var topic = string.IsNullOrWhiteSpace(TelemetryTopic) ? "ks" : TelemetryTopic.Trim();

        var device = new ModbusDevice
        {
            Name          = DeviceName,
            SlaveId       = SlaveId,
            TransportType = TransportType.MqttCloud,
            SerialNumber  = serial,
            IsActive      = true,
            DeviceModelId = SelectedModel?.Id,
            Mqtt          = new MqttConfig
            {
                BrokerHost     = BrokerHost.Trim(),
                Port           = BrokerPort,
                UseTls         = BrokerUseTls,
                Username       = string.IsNullOrWhiteSpace(BrokerUsername) ? null : BrokerUsername,
                Password       = string.IsNullOrWhiteSpace(BrokerPassword) ? null : BrokerPassword,
                TelemetryTopic = topic,
                ReplyTopic     = topic
            }
        };

        await _deviceRepository.AddAsync(device);
        await _parent.LoadDevicesAsync();

        DeviceName = "";
        CloudSerial = "";
        SetFeedback("Dispositivo salvo.", isError: false);
    }

    /// <summary>Connects over TCP and reads the NS register (FC04 addr 0); null on failure.</summary>
    private async Task<uint?> ProbeSerialNumberAsync(string effectiveIp)
    {
        var tempDevice = new ModbusDevice
        {
            Name = "",
            SlaveId = SlaveId,
            TransportType = TransportType.Tcp,
            Tcp = new TcpConfig { IpAddress = effectiveIp, Port = TcpPort }
        };

        IModbusService? svc = null;
        try
        {
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
            if (svc is not null)
            {
                try { await svc.DisconnectAsync(); } catch { }
                svc.Dispose();
            }
        }
    }

    private bool CanSave() => !IsScanning && !IsSaving && !string.IsNullOrWhiteSpace(DeviceName);

    [RelayCommand]
    private void GoBack()
    {
        _scanCts?.Cancel();
        _parent.NavigateBack();
    }

    private void SetFeedback(string message, bool isError)
    {
        SaveFeedbackIsError = isError;
        SaveFeedback = message;
    }

    // ── Constructor ──────────────────────────────────────────────────────────────

    public AddDeviceViewModel(
        IDeviceScanService scanService,
        IDeviceRepository deviceRepository,
        IDeviceModelRepository deviceModelRepository,
        IModbusServiceFactory serviceFactory,
        INetworkScanLock scanLock,
        DeviceListViewModel parent)
    {
        _scanService           = scanService;
        _deviceRepository      = deviceRepository;
        _deviceModelRepository = deviceModelRepository;
        _serviceFactory        = serviceFactory;
        _scanLock              = scanLock;
        _parent                = parent;

        _ = LoadAvailableModelsAsync();
    }

    private async Task LoadAvailableModelsAsync()
    {
        try
        {
            var models = await _deviceModelRepository.GetAllAsync();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                AvailableModels.Clear();
                foreach (var model in models)
                    AvailableModels.Add(model);
                SelectedModel ??= AvailableModels.FirstOrDefault();
            });
        }
        catch { /* model list is optional */ }
    }
}
