using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Modbus.Core.Services;
using Modbus.Desktop.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Modbus.Desktop.ViewModels;

public sealed record GrandezaColumn(ushort MqttId, string Code);

public sealed class MassMemoryRecordViewModel
{
    public int      Block          { get; init; }
    public string   Date           { get; init; } = "";
    public string   Time           { get; init; } = "";
    public string[] Values         { get; init; } = [];
    public bool     ChecksumOk     { get; init; }
    public string   ChecksumOkText => ChecksumOk ? "OK" : "Erro";
}

public partial class MassMemoryViewModel : ObservableObject, IDisposable
{
    private readonly DeviceItemViewModel _device;
    private readonly IDeviceConfigService _configService;
    private readonly Func<Task> _pausePolling;
    private readonly Action _resumePolling;
    private readonly Action _onGoBack;

    // ── Header ──────────────────────────────────────────────────────────────
    [ObservableProperty] private string _serialNumber    = "—";
    [ObservableProperty] private string _address         = "—";
    [ObservableProperty] private string _description     = "—";
    [ObservableProperty] private string _storageInterval = "—";
    [ObservableProperty] private string _storageMode     = "—";

    // ── Estado ───────────────────────────────────────────────────────────────
    [ObservableProperty] private bool    _isRunning;
    [ObservableProperty] private bool    _isLoading;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string  _toggleReadingLabel = "";

    // ── Dados ────────────────────────────────────────────────────────────────
    public ObservableCollection<MassMemoryRecordViewModel> Records { get; } = new();

    // Disparado após LoadAsync definir as colunas; o code-behind constrói o DataGrid.
    public event Action<IReadOnlyList<GrandezaColumn>>? ColumnsReady;

    public MassMemoryViewModel(
        DeviceItemViewModel device,
        IDeviceConfigService configService,
        Func<Task> pausePolling,
        Action resumePolling,
        Action onGoBack)
    {
        _device        = device;
        _configService = configService;
        _pausePolling  = pausePolling;
        _resumePolling = resumePolling;
        _onGoBack      = onGoBack;

        LocalizationService.Instance.PropertyChanged += OnLocalizationChanged;
        UpdateToggleLabel();
    }

    public void Dispose() =>
        LocalizationService.Instance.PropertyChanged -= OnLocalizationChanged;

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "Item[]") UpdateToggleLabel();
    }

    partial void OnIsRunningChanged(bool value) => UpdateToggleLabel();

    private void UpdateToggleLabel()
    {
        ToggleReadingLabel = IsRunning
            ? LocalizationService.Instance["MmPauseReading"]
            : LocalizationService.Instance["MmStartReading"];
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        StatusMessage = null;

        var profile = DeviceConfigProfileRegistry.Get(_device.Device.DeviceModel?.DeviceCode);
        var loc     = LocalizationService.Instance;

        Description  = _device.Name;
        Address      = _device.SlaveId.ToString();
        SerialNumber = _device.Device.SerialNumber.HasValue
            ? _device.Device.SerialNumber.Value.ToString("D7")
            : "—";

        if (profile is null)
        {
            IsLoading = false;
            StatusMessage = loc["MmNoGrandezas"];
            ColumnsReady?.Invoke([]);
            return;
        }

        await _pausePolling();
        try
        {
            var fields = new List<RegisterField>();
            if (profile.AddrSendInterval.HasValue)         fields.Add(profile.AddrSendInterval.Value);
            if (profile.AddrStorageMode.HasValue)          fields.Add(profile.AddrStorageMode.Value);
            if (profile.AddrGrandezasSlots1to20.HasValue)  fields.Add(profile.AddrGrandezasSlots1to20.Value);
            if (profile.AddrGrandezasSlots21to50.HasValue) fields.Add(profile.AddrGrandezasSlots21to50.Value);

            var result = await _configService.ReadAsync(_device.Device, fields);
            var regs   = result.Values;

            // ── Intervalo de armazenamento ───────────────────────────────────
            if (profile.AddrSendInterval is RegisterField siField
                && regs.TryGetValue(siField.Addr, out var interval))
            {
                StorageInterval = $"{interval} {loc["MmMinutes"]}";
            }

            // ── Modo de armazenamento ─────────────────────────────────────────
            if (profile.AddrStorageMode is RegisterField smField)
            {
                var smVal = smField.ExtractValue(regs);
                StorageMode = smVal == 1 ? loc["MmModeLinear"] : loc["MmModeCircular"];
            }
            else
            {
                StorageMode = loc["MmModeCircular"];
            }

            // ── Grandezas configuradas → colunas ────────────────────────────
            var grandezaById = GrandezaCatalog
                .ForDeviceCode(_device.Device.DeviceModel?.DeviceCode)
                .ToDictionary(g => g.MqttId);

            var columns = new List<GrandezaColumn>();
            foreach (var sf in new[] { profile.AddrGrandezasSlots1to20, profile.AddrGrandezasSlots21to50 })
            {
                if (sf is not RegisterField slotField) continue;
                for (int i = 0; i < slotField.WordCount; i++)
                {
                    var addr = (ushort)(slotField.Addr + i);
                    if (!regs.TryGetValue(addr, out var raw)) continue;
                    if (raw == 0xFFFF || raw == 0x0000) continue;
                    if (grandezaById.TryGetValue(raw, out var g))
                        columns.Add(new GrandezaColumn(g.MqttId, g.Code));
                    else
                        Debug.WriteLine($"[MassMemory] MqttId {raw} no slot {addr} não encontrado no catálogo.");
                }
            }

            if (columns.Count == 0)
                StatusMessage = loc["MmNoGrandezas"];

            ColumnsReady?.Invoke(columns);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MassMemory] LoadAsync failed: {ex.Message}");
            StatusMessage = ex.Message;
            ColumnsReady?.Invoke([]);
        }
        finally
        {
            _resumePolling();
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ToggleReading()
    {
        IsRunning = !IsRunning;
        if (!IsRunning)
            StatusMessage = null;
    }

    [RelayCommand]
    private void ExportTxt()
    {
        // Stub — exportação será implementada junto da leitura real da memória de massa.
    }

    [RelayCommand]
    private void GoBack() => _onGoBack();
}
