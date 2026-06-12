using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Modbus.Core.Services;
using Modbus.Desktop.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
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
    private readonly IMassMemoryService _massMemoryService;
    private readonly Func<Task> _pausePolling;
    private readonly Action _resumePolling;
    private readonly Action _onGoBack;

    private ushort _sqpf = 0x3210;
    private IReadOnlyList<GrandezaColumn> _columns = [];
    private CancellationTokenSource? _readCts;

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

    // Disparado pelo ExportTxtCommand; o code-behind abre o file picker e retorna o stream.
    public event Func<Task<Stream?>>? SaveFileRequested;

    public MassMemoryViewModel(
        DeviceItemViewModel device,
        IDeviceConfigService configService,
        IMassMemoryService massMemoryService,
        Func<Task> pausePolling,
        Action resumePolling,
        Action onGoBack)
    {
        _device             = device;
        _configService      = configService;
        _massMemoryService  = massMemoryService;
        _pausePolling       = pausePolling;
        _resumePolling      = resumePolling;
        _onGoBack           = onGoBack;

        LocalizationService.Instance.PropertyChanged += OnLocalizationChanged;
        UpdateToggleLabel();
    }

    public void Dispose()
    {
        LocalizationService.Instance.PropertyChanged -= OnLocalizationChanged;
        _readCts?.Cancel();
        _readCts?.Dispose();
    }

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
            if (profile.AddrSeqPf.HasValue)                fields.Add(profile.AddrSeqPf.Value);
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

            // ── SQPF ─────────────────────────────────────────────────────────
            if (profile.AddrSeqPf is RegisterField sqpfField
                && regs.TryGetValue(sqpfField.Addr, out var sqpfVal))
            {
                _sqpf = sqpfVal;
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

            _columns = columns;

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
        if (IsRunning)
        {
            _readCts?.Cancel();
        }
        else
        {
            IsRunning = true;
            _readCts?.Dispose();
            _readCts = new CancellationTokenSource();
            _ = ReadAllBlocksAsync(_readCts.Token);
        }
    }

    private async Task ReadAllBlocksAsync(CancellationToken ct)
    {
        var loc = LocalizationService.Instance;

        await _pausePolling();
        Records.Clear();

        try
        {
            var ctrl = await _massMemoryService.ReadControlBlockAsync(_device.Device, ct);

            if (ctrl is null)
            {
                StatusMessage = loc["MmControlBlockFailed"];
                return;
            }

            if (ctrl.BGS == 0)
            {
                StatusMessage = loc["MmEmpty"];
                return;
            }

            await foreach (var blk in _massMemoryService.ReadBlocksAsync(_device.Device, ctrl, ct))
            {
                StatusMessage = string.Format(loc["MmReading"], blk.BlockIndex, ctrl.BGS);
                Records.Add(new MassMemoryRecordViewModel
                {
                    Block      = blk.BlockIndex,
                    Date       = blk.Timestamp.ToString("dd/MM/yy"),
                    Time       = blk.Timestamp.ToString("HH:mm:ss"),
                    Values     = blk.Values.Select(v => v.ToString("G6")).ToArray(),
                    ChecksumOk = blk.ChecksumOk
                });
            }

            StatusMessage = ct.IsCancellationRequested
                ? string.Format(loc["MmReadCancelled"], Records.Count)
                : string.Format(loc["MmReadComplete"], Records.Count);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = string.Format(loc["MmReadCancelled"], Records.Count);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MassMemory] ReadAllBlocks failed: {ex.Message}");
            StatusMessage = string.Format(loc["MmReadError"], ex.Message);
        }
        finally
        {
            _resumePolling();
            IsRunning = false;
        }
    }

    [RelayCommand]
    private async Task ExportTxt()
    {
        if (Records.Count == 0 || SaveFileRequested is null) return;

        var stream = await SaveFileRequested.Invoke();
        if (stream is null) return;

        try
        {
            await using var writer = new StreamWriter(stream);

            // Header row
            var headers = new List<string> { "Bloco", "Data", "Hora" };
            headers.AddRange(_columns.Select(c => c.Code));
            headers.Add("CS");
            await writer.WriteLineAsync(string.Join("\t", headers));

            // Data rows
            foreach (var r in Records)
            {
                var row = new List<string> { r.Block.ToString(), r.Date, r.Time };
                row.AddRange(r.Values);
                row.Add(r.ChecksumOkText);
                await writer.WriteLineAsync(string.Join("\t", row));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MassMemory] ExportTxt failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private void GoBack() => _onGoBack();
}
