using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Modbus.Core.Domain.Enums;
using Modbus.Core.Domain.Repositories;
using Modbus.Core.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Modbus.Desktop.ViewModels;

public partial class DeviceConfigureViewModel : ObservableObject
{
    private readonly Action _onGoBack;
    private readonly IDeviceConfigService _configService;
    private readonly IDeviceRepository _deviceRepository;
    private readonly Func<Task> _pausePolling;
    private readonly Action _resumePolling;

    /// <summary>
    /// Set by the View. Asked (blocking) right before a save that will reset the meter's mass
    /// memory — i.e. when the IoT grandezas selection and/or send interval changed. Returns
    /// true to proceed, false to cancel the save. When null, the save proceeds without asking.
    /// </summary>
    public Func<Task<bool>>? ConfirmMassMemoryReset { get; set; }

    public DeviceItemViewModel Device { get; }

    // ── Section navigation ───────────────────────────────────────────────────
    public sealed record SidebarSection(int Code, string Label)
    {
        public override string ToString() => Label;
    }

    public IReadOnlyList<SidebarSection> Sections { get; }

    [ObservableProperty] private SidebarSection? _selectedSection;

    public bool IsGeneral       => SelectedSection?.Code == 0;
    public bool IsEthernet      => SelectedSection?.Code == 1;
    public bool IsWireless      => SelectedSection?.Code == 2;
    public bool IsSntp          => SelectedSection?.Code == 3;
    public bool IsIot           => SelectedSection?.Code == 4;
    public bool IsClock         => SelectedSection?.Code == 5;
    public bool IsInputsOutputs => SelectedSection?.Code == 6;

    partial void OnSelectedSectionChanged(SidebarSection? value)
    {
        OnPropertyChanged(nameof(IsGeneral));
        OnPropertyChanged(nameof(IsEthernet));
        OnPropertyChanged(nameof(IsWireless));
        OnPropertyChanged(nameof(IsSntp));
        OnPropertyChanged(nameof(IsIot));
        OnPropertyChanged(nameof(IsClock));
        OnPropertyChanged(nameof(IsInputsOutputs));
    }

    // ── Capabilities ─────────────────────────────────────────────────────────
    private DeviceCapabilities Capabilities =>
        DeviceCapabilityRegistry.Get(Device.Device.DeviceModel?.DeviceCode);

    public bool HasEthernet      => Capabilities.HasFlag(DeviceCapabilities.Ethernet);
    public bool HasWireless      => Capabilities.HasFlag(DeviceCapabilities.Wireless);
    public bool HasSntp          => Capabilities.HasFlag(DeviceCapabilities.Sntp);
    public bool HasIot           => Capabilities.HasFlag(DeviceCapabilities.Iot);
    public bool HasClock         => Capabilities.HasFlag(DeviceCapabilities.Clock);
    public bool HasInputsOutputs => Capabilities.HasFlag(DeviceCapabilities.InputsOutputs);
    public bool HasFieldKe       => Capabilities.HasFlag(DeviceCapabilities.FieldKe);
    public bool HasCurrentInvert => Capabilities.HasFlag(DeviceCapabilities.FieldCurrentInvert);
    public bool HasIotGrandezaSelection => Capabilities.HasFlag(DeviceCapabilities.IotGrandezaSelection);

    public bool IsRtu => Device.TransportType == TransportType.Rtu;

    // ── Load / Edit / Save state ─────────────────────────────────────────────
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _loadError;

    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private string? _saveError;
    [ObservableProperty] private string? _saveSuccess;

    // Snapshot of the last fully-successful read. Acts as the baseline for the dirty diff
    // in SaveAsync (so we only write fields the user actually changed) and as the source
    // for Revert (which reverts the VM properties to these values).
    private IReadOnlyDictionary<ushort, ushort>? _baseline;
    private DeviceConfigProfile? _profile;

    // Editing is unlocked the moment the read completes successfully — no "Edit" button needed.
    public bool IsReady      => !IsLoading && !IsSaving
                              && string.IsNullOrEmpty(LoadError)
                              && _baseline is not null;
    public bool CanSave      => IsReady && !HasInvalidInput;
    public bool CanCancel    => IsReady;
    public bool CanRetryRead => !IsLoading && !IsSaving;
    public bool CanGoBack    => !IsSaving;

    partial void OnIsLoadingChanged(bool value)   { NotifyEditButtonsChanged(); }
    partial void OnIsSavingChanged(bool value)    { NotifyEditButtonsChanged(); GoBackCommand.NotifyCanExecuteChanged(); }
    partial void OnLoadErrorChanged(string? value){ NotifyEditButtonsChanged(); }

    private void NotifyEditButtonsChanged()
    {
        OnPropertyChanged(nameof(IsReady));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(CanRetryRead));
        OnPropertyChanged(nameof(CanGoBack));
    }

    // ── IP validation ────────────────────────────────────────────────────────
    // Each IP field exposes a "HasInvalidXxx" flag that is true only when the field
    // is non-empty AND can't be parsed. Empty strings are valid (the dirty diff
    // silently skips writing them). HasInvalidInput aggregates all of them and gates
    // the Save button so the user can't ship malformed IPs to the device.
    public bool HasInvalidEthIp       => IsMalformedIp(EthIp);
    public bool HasInvalidEthMask     => IsMalformedIp(EthMask);
    public bool HasInvalidEthGateway  => IsMalformedIp(EthGateway);
    public bool HasInvalidWifiIp      => IsMalformedIp(WifiIp);
    public bool HasInvalidWifiMask    => IsMalformedIp(WifiMask);
    public bool HasInvalidWifiGateway => IsMalformedIp(WifiGateway);
    public bool HasInvalidWifiDns     => IsMalformedIp(WifiDns);

    public bool HasInvalidInput =>
        HasInvalidEthIp || HasInvalidEthMask || HasInvalidEthGateway ||
        HasInvalidWifiIp || HasInvalidWifiMask || HasInvalidWifiGateway ||
        HasInvalidWifiDns;

    private static bool IsMalformedIp(string? s) =>
        !string.IsNullOrWhiteSpace(s) && ParseIp(s) is null;

    partial void OnEthIpChanged(string? value)       { OnPropertyChanged(nameof(HasInvalidEthIp));       NotifyInvalidInputChanged(); }
    partial void OnEthMaskChanged(string? value)     { OnPropertyChanged(nameof(HasInvalidEthMask));     NotifyInvalidInputChanged(); }
    partial void OnEthGatewayChanged(string? value)  { OnPropertyChanged(nameof(HasInvalidEthGateway));  NotifyInvalidInputChanged(); }
    partial void OnWifiIpChanged(string? value)      { OnPropertyChanged(nameof(HasInvalidWifiIp));      NotifyInvalidInputChanged(); }
    partial void OnWifiMaskChanged(string? value)    { OnPropertyChanged(nameof(HasInvalidWifiMask));    NotifyInvalidInputChanged(); }
    partial void OnWifiGatewayChanged(string? value) { OnPropertyChanged(nameof(HasInvalidWifiGateway)); NotifyInvalidInputChanged(); }
    partial void OnWifiDnsChanged(string? value)     { OnPropertyChanged(nameof(HasInvalidWifiDns));     NotifyInvalidInputChanged(); }

    private void NotifyInvalidInputChanged()
    {
        OnPropertyChanged(nameof(HasInvalidInput));
        OnPropertyChanged(nameof(CanSave));
    }

    // ── Device info strip (always visible) ──────────────────────────────────
    public string SerialNumberText =>
        Device.Device.SerialNumber is uint s ? $"{s:D7}" : "—";

    public string FirmwareText => Device.Device.FirmwareVersion is byte fw
        ? $"v{fw / 10}.{fw % 10}"
        : "—";

    public string HardwareText => "—";

    // ── General section fields ───────────────────────────────────────────────
    [ObservableProperty] private string _description;

    public string DeviceCodeDisplay =>
        Device.Device.DeviceModel?.DeviceCode is byte code ? $"{code:X2}" : "—";

    [ObservableProperty] private int _editableSlaveId;

    [ObservableProperty] private decimal? _tp;
    [ObservableProperty] private decimal? _tc;
    [ObservableProperty] private decimal? _ke;
    [ObservableProperty] private TlOption? _tl;
    [ObservableProperty] private decimal? _ti;

    public sealed record TlOption(int Code, string Label)
    {
        public override string ToString() => Label;
    }

    public IReadOnlyList<TlOption> TlOptions { get; } =
    [
        new(0,  "00 – Trifásico Estrela 3 Elementos 4 Fios"),
        new(1,  "01 – Bifásico"),
        new(2,  "02 – Monofásico"),
        new(3,  "03 – Trifásico Estrela Equilibrado 1 Elemento 2 Fios"),
        new(48, "48 – Trifásico Delta 3 Elementos"),
    ];
    [ObservableProperty] private bool _currentInvert;
    [ObservableProperty] private decimal? _hourmeterThr;

    // ── Seq. PF (swap logic: selecting a value exchanges it with the position
    //    that currently holds that value, so all 4 are always distinct) ───────
    public IReadOnlyList<string> AllPfBytes { get; } = ["F0", "F1", "F2", "EXP"];

    private readonly string[] _pfPos = ["F2", "F1", "F0", "EXP"];

    public string PfPos0 { get => _pfPos[0]; set => SetPfPos(0, value); }
    public string PfPos1 { get => _pfPos[1]; set => SetPfPos(1, value); }
    public string PfPos2 { get => _pfPos[2]; set => SetPfPos(2, value); }
    public string PfPos3 { get => _pfPos[3]; set => SetPfPos(3, value); }

    private void SetPfPos(int index, string newValue)
    {
        if (newValue == _pfPos[index]) return;

        int conflictIndex = Array.IndexOf(_pfPos, newValue);
        if (conflictIndex >= 0 && conflictIndex != index)
        {
            _pfPos[conflictIndex] = _pfPos[index];
            OnPropertyChanged($"PfPos{conflictIndex}");
        }

        _pfPos[index] = newValue;
        OnPropertyChanged($"PfPos{index}");
    }

    // ── Ethernet ─────────────────────────────────────────────────────────────
    // DNS (enabled + server) is shared with the Wireless tab on devices like the
    // Konect 120 where both interfaces read/write the same register. Both tabs
    // bind to WifiDnsEnabled / WifiDns so changes propagate automatically.
    [ObservableProperty] private bool _ethDhcp;
    [ObservableProperty] private string? _ethIp;
    [ObservableProperty] private string? _ethMask;
    [ObservableProperty] private string? _ethGateway;
    [ObservableProperty] private string? _ethMac;

    // ── Wireless ─────────────────────────────────────────────────────────────
    public sealed record WirelessModeOption(int Code, string Label)
    {
        public override string ToString() => Label;
    }

    // Bits D8 (WiFi disabled flag) and D9 (Bluetooth disabled flag) of register 40020,
    // read as a 2-bit field (BitOffset:8 BitWidth:2). Code = (D9 << 1) | D8.
    public IReadOnlyList<WirelessModeOption> WirelessModeOptions { get; } =
    [
        new(0, "Wi-Fi + Bluetooth"),
        new(1, "Bluetooth"),
        new(2, "Wi-Fi"),
        new(3, "Desabilitado"),
    ];

    [ObservableProperty] private WirelessModeOption? _wirelessMode;

    [ObservableProperty] private string? _ssid;
    [ObservableProperty] private string? _wifiPassword;
    [ObservableProperty] private bool _isWifiPasswordVisible;

    public char WifiPasswordChar => IsWifiPasswordVisible ? '\0' : '•';

    partial void OnIsWifiPasswordVisibleChanged(bool value)
        => OnPropertyChanged(nameof(WifiPasswordChar));
    [ObservableProperty] private string? _moduleVersion;
    [ObservableProperty] private bool _wifiDhcp;
    [ObservableProperty] private bool _wifiDnsEnabled;
    [ObservableProperty] private string? _wifiIp;
    [ObservableProperty] private string? _wifiMask;
    [ObservableProperty] private string? _wifiGateway;
    [ObservableProperty] private string? _wifiDns;
    [ObservableProperty] private string? _wifiMac;
    [ObservableProperty] private string? _btDescription;
    [ObservableProperty] private string? _btPassword;
    [ObservableProperty] private string? _btMac;

    // ── SNTP ─────────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _sntpEnabled;
    [ObservableProperty] private decimal? _timezone;
    [ObservableProperty] private decimal? _syncInterval;
    [ObservableProperty] private string? _ntpServer;

    // ── IoT ──────────────────────────────────────────────────────────────────
    public sealed record MqttBrokerOption(int Code, string Label)
    {
        public override string ToString() => Label;
    }

    // Bits D11-D12 of register 40020 (read as a 2-bit field).
    public IReadOnlyList<MqttBrokerOption> MqttBrokerOptions { get; } =
    [
        new(0, "Padrão / AWS"),
        new(1, "IBM"),
        new(2, "Azure"),
        new(3, "Losant / Wegnology"),
    ];

    [ObservableProperty] private MqttBrokerOption? _mqttBroker;

    [ObservableProperty] private bool _iotEnabled;
    [ObservableProperty] private bool _sendOnHour;
    [ObservableProperty] private bool _keepAlive;
    [ObservableProperty] private bool _tls;
    [ObservableProperty] private decimal? _sendInterval;
    [ObservableProperty] private string? _mqttPort;
    [ObservableProperty] private string? _mqttUrl;
    [ObservableProperty] private string? _mqttDescId;
    [ObservableProperty] private string? _mqttTopic;
    [ObservableProperty] private string? _mqttUser;
    [ObservableProperty] private string? _mqttToken;

    // ── Grandezas selecionadas (MQTT/LoRa) ──────────────────────────────────
    public sealed partial class GrandezaItemViewModel : ObservableObject
    {
        public Grandeza Model { get; }
        public ushort MqttId => Model.MqttId;
        public string Code => Model.Code;
        public string Description => Model.Description;

        [ObservableProperty] private bool _isSelected;
        [ObservableProperty] private int? _order;

        public GrandezaItemViewModel(Grandeza model) { Model = model; }
    }

    // Catalog order is the natural display order on the left list (already grouped by category
    // within the static catalog, so equivalent grandezas stay near each other without Expanders).
    public ObservableCollection<GrandezaItemViewModel> AvailableFlat { get; } = new();
    public ObservableCollection<GrandezaItemViewModel> SelectedOrdered { get; } = new();

    [ObservableProperty] private int _maxGrandezas = 50;

    public int SelectedCount => SelectedOrdered.Count;
    public string GrandezaCounterText =>
        string.Format(
            Modbus.Desktop.Services.LocalizationService.Instance["CfgGrandezaCounter"],
            SelectedCount, MaxGrandezas);

    partial void OnMaxGrandezasChanged(int value) => OnPropertyChanged(nameof(GrandezaCounterText));

    // Master list in the canonical catalog order — used to restore items to AvailableFlat
    // in the original position when removed from the selected panel.
    private readonly List<GrandezaItemViewModel> _catalogOrder = new();
    private readonly Dictionary<ushort, GrandezaItemViewModel> _grandezaById = new();

    private void BuildGrandezaCatalog()
    {
        AvailableFlat.Clear();
        SelectedOrdered.Clear();
        _catalogOrder.Clear();
        _grandezaById.Clear();

        var deviceCode = Device.Device.DeviceModel?.DeviceCode;
        var catalog = GrandezaCatalog.ForDeviceCode(deviceCode);
        if (catalog.Count == 0) return;

        MaxGrandezas = GrandezaCatalog.Limit(deviceCode, Device.Device.FirmwareVersion);

        foreach (var g in catalog)
        {
            var vm = new GrandezaItemViewModel(g);
            _grandezaById[g.MqttId] = vm;
            _catalogOrder.Add(vm);
            AvailableFlat.Add(vm);
        }
    }

    private void ApplyGrandezasFromRegs(IReadOnlyDictionary<ushort, ushort> regs, DeviceConfigProfile p)
    {
        // Rebuild both lists from scratch so the call is idempotent (Save → re-read uses this too).
        SelectedOrdered.Clear();
        AvailableFlat.Clear();
        foreach (var item in _catalogOrder)
        {
            item.IsSelected = false;
            item.Order = null;
            AvailableFlat.Add(item);
        }

        var slotAddrs = new List<ushort>();
        if (p.AddrGrandezasSlots1to20 is RegisterField f1)
            for (int i = 0; i < f1.WordCount; i++) slotAddrs.Add((ushort)(f1.Addr + i));
        if (p.AddrGrandezasSlots21to50 is RegisterField f2)
            for (int i = 0; i < f2.WordCount; i++) slotAddrs.Add((ushort)(f2.Addr + i));

        // Collect first, then sort by MqttId so SelectedOrdered is always kept in ascending
        // address order (matches what subsequent Add operations enforce).
        var loaded = new List<GrandezaItemViewModel>();
        foreach (var addr in slotAddrs)
        {
            if (!regs.TryGetValue(addr, out var raw)) continue;
            if (raw == 0xFFFF || raw == 0x0000) continue;

            if (_grandezaById.TryGetValue(raw, out var item))
                loaded.Add(item);
            else
                Debug.WriteLine($"[Grandezas] ID {raw} no slot {addr} não encontrado no catálogo do modelo.");
        }

        foreach (var item in loaded.OrderBy(g => g.MqttId))
        {
            item.IsSelected = true;
            SelectedOrdered.Add(item);
            AvailableFlat.Remove(item);
        }

        RecomputeOrders();
    }

    // ── Grandeza editing commands (used by the two-list panel buttons) ───────
    // Commands take the ListBox.SelectedItems as parameter (IList) — Avalonia exposes the
    // multi-selection observably but there's no two-way SelectedItems binding, so passing it
    // via CommandParameter is the cleanest MVVM approach.
    // Returns the index in AvailableFlat that the caller (the View) should select next so the
    // user can keep pressing "→" to add grandezas in sequence without re-clicking the list.
    // Returns -1 when no further selection is possible (empty list).
    public int AddGrandezasReturningNextIndex(System.Collections.IList? selectedFromAvailable)
    {
        if (selectedFromAvailable is null || selectedFromAvailable.Count == 0) return -1;

        // Snapshot to a typed list — modifying AvailableFlat would invalidate the live SelectedItems.
        var toAdd = selectedFromAvailable
            .OfType<GrandezaItemViewModel>()
            .ToList();
        if (toAdd.Count == 0) return -1;

        // Remember the smallest index of the items being removed; after the operation, that
        // slot is occupied by whatever item was immediately after the original selection —
        // selecting it gives the user a "press → repeatedly" affordance.
        int nextIndex = toAdd
            .Select(it => AvailableFlat.IndexOf(it))
            .Where(i => i >= 0)
            .DefaultIfEmpty(-1)
            .Min();

        bool limitHit = false;
        foreach (var item in toAdd)
        {
            if (SelectedOrdered.Count >= MaxGrandezas) { limitHit = true; break; }
            if (item.IsSelected) continue; // safety: skip if already selected somehow
            item.IsSelected = true;
            InsertSelectedByMqttId(item);
            AvailableFlat.Remove(item);
        }

        RecomputeOrders();
        if (limitHit)
        {
            var loc = Modbus.Desktop.Services.LocalizationService.Instance;
            SaveError = string.Format(loc["CfgGrandezaLimitReached"], MaxGrandezas);
        }

        if (AvailableFlat.Count == 0) return -1;
        return Math.Clamp(nextIndex, 0, AvailableFlat.Count - 1);
    }

    // Convenience for binding via CommandParameter — same logic, ignores the next-index hint.
    [RelayCommand]
    private void AddGrandezas(System.Collections.IList? selectedFromAvailable)
        => AddGrandezasReturningNextIndex(selectedFromAvailable);

    // Inserts an item into SelectedOrdered keeping the list sorted by MqttId ascending,
    // so slots on the device are always written in address order.
    private void InsertSelectedByMqttId(GrandezaItemViewModel item)
    {
        int i = 0;
        while (i < SelectedOrdered.Count && SelectedOrdered[i].MqttId < item.MqttId) i++;
        SelectedOrdered.Insert(i, item);
    }

    // Returns the index in SelectedOrdered that the caller (the View) should select next so
    // the user can keep pressing "←" to remove grandezas in sequence without re-clicking.
    // Returns -1 when no further selection is possible (empty list).
    public int RemoveGrandezasReturningNextIndex(System.Collections.IList? selectedFromSelected)
    {
        if (selectedFromSelected is null || selectedFromSelected.Count == 0) return -1;
        var toRemove = selectedFromSelected
            .OfType<GrandezaItemViewModel>()
            .ToList();
        if (toRemove.Count == 0) return -1;

        int nextIndex = toRemove
            .Select(it => SelectedOrdered.IndexOf(it))
            .Where(i => i >= 0)
            .DefaultIfEmpty(-1)
            .Min();

        foreach (var item in toRemove)
            RestoreToAvailable(item);
        RecomputeOrders();

        if (SelectedOrdered.Count == 0) return -1;
        return Math.Clamp(nextIndex, 0, SelectedOrdered.Count - 1);
    }

    // Convenience wrapper for binding (currently unused — the View calls the returning version
    // via code-behind to also drive focus/selection).
    [RelayCommand]
    private void RemoveGrandezas(System.Collections.IList? selectedFromSelected)
        => RemoveGrandezasReturningNextIndex(selectedFromSelected);

    [RelayCommand]
    private void ClearGrandezas()
    {
        if (SelectedOrdered.Count == 0) return;
        var snapshot = SelectedOrdered.ToList();
        foreach (var item in snapshot)
            RestoreToAvailable(item);
        RecomputeOrders();
    }

    private void RestoreToAvailable(GrandezaItemViewModel item)
    {
        SelectedOrdered.Remove(item);
        item.IsSelected = false;
        item.Order = null;

        // Insert at the position dictated by _catalogOrder so the left list keeps a stable
        // logical sort even after multiple add/remove cycles.
        int catalogIdx = _catalogOrder.IndexOf(item);
        int insertAt = 0;
        for (int i = 0; i < AvailableFlat.Count; i++)
        {
            if (_catalogOrder.IndexOf(AvailableFlat[i]) > catalogIdx) break;
            insertAt = i + 1;
        }
        AvailableFlat.Insert(insertAt, item);
    }

    private void RecomputeOrders()
    {
        for (int i = 0; i < SelectedOrdered.Count; i++)
            SelectedOrdered[i].Order = i + 1;
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(GrandezaCounterText));
    }

    // ── Clock ────────────────────────────────────────────────────────────────
    [ObservableProperty] private DateTimeOffset? _clockDate;
    [ObservableProperty] private TimeSpan? _clockTime;

    // Sync mode: when true, a 1-Hz timer pushes the current PC time into ClockDate/ClockTime
    // so the user always sees an up-to-date timestamp. Defaults to true to match the old
    // KRON software, which opens this tab with "PC" selected.
    [ObservableProperty] private bool _clockSyncFromPc = true;

    private DispatcherTimer? _clockTimer;

    public string ClockDateText => ClockDate is { } d ? d.ToString("dd/MM/yyyy") : "—";
    public string ClockTimeText => ClockTime is { } t ? t.ToString(@"hh\:mm\:ss") : "—";

    partial void OnClockDateChanged(DateTimeOffset? value) => OnPropertyChanged(nameof(ClockDateText));
    partial void OnClockTimeChanged(TimeSpan? value)       => OnPropertyChanged(nameof(ClockTimeText));

    partial void OnClockSyncFromPcChanged(bool value) => UpdateClockTimerState();

    private void UpdateClockTimerState()
    {
        if (ClockSyncFromPc)
        {
            if (_clockTimer is null)
            {
                _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _clockTimer.Tick += (_, _) => SetClockFromPc();
            }
            SetClockFromPc();   // immediate so the user doesn't wait a second for the first paint
            _clockTimer.Start();
        }
        else
        {
            _clockTimer?.Stop();
        }
    }

    private void SetClockFromPc()
    {
        var now = DateTime.Now;
        // DateTimeOffset rejects Local DateTime with a non-matching offset; strip Kind so
        // the Zero offset is accepted (matches what ApplyRegisters does for device reads).
        var dateOnly = DateTime.SpecifyKind(now.Date, DateTimeKind.Unspecified);
        if (ClockDate?.DateTime != dateOnly)
            ClockDate = new DateTimeOffset(dateOnly, TimeSpan.Zero);
        ClockTime = now.TimeOfDay;
    }

    // ── Inputs / Outputs ─────────────────────────────────────────────────────
    [ObservableProperty] private decimal? _debounceEdp;

    // ── Constructor / Commands ───────────────────────────────────────────────
    public DeviceConfigureViewModel(
        DeviceItemViewModel device,
        IDeviceConfigService configService,
        IDeviceRepository deviceRepository,
        Func<Task> pausePolling,
        Action resumePolling,
        Action onGoBack)
    {
        Device              = device;
        _configService      = configService;
        _deviceRepository   = deviceRepository;
        _pausePolling  = pausePolling;
        _resumePolling = resumePolling;
        _onGoBack           = onGoBack;
        _editableSlaveId    = device.SlaveId;
        _description        = device.Name;

        var loc = Modbus.Desktop.Services.LocalizationService.Instance;
        var sections = new List<SidebarSection> { new(0, loc["CfgGeneral"]) };
        if (HasEthernet)      sections.Add(new(1, loc["CfgEthernet"]));
        if (HasWireless)      sections.Add(new(2, loc["CfgWireless"]));
        if (HasSntp)          sections.Add(new(3, loc["CfgSntp"]));
        if (HasIot)           sections.Add(new(4, loc["CfgIot"]));
        if (HasClock)         sections.Add(new(5, loc["CfgClock"]));
        if (HasInputsOutputs) sections.Add(new(6, loc["CfgInputsOutputs"]));
        Sections = sections;
        _selectedSection = sections[0];

        // Kick off the PC-time updater so the Clock tab shows the current time
        // immediately when the user opens it (field init doesn't fire OnChanged).
        UpdateClockTimerState();

        BuildGrandezaCatalog();
    }

    public async Task LoadAsync()
    {
        var profile = DeviceConfigProfileRegistry.Get(Device.Device.DeviceModel?.DeviceCode);
        if (profile is null || profile.AllFields.Count == 0) return;

        _profile = profile;
        IsLoading = true;
        LoadError = null;
        SaveError = null;
        SaveSuccess = null;
        try
        {
            await _pausePolling();
            try
            {
                await DoReadFromDeviceAsync(profile);
            }
            finally
            {
                _resumePolling();
            }
        }
        catch (Exception ex)
        {
            _baseline = null;
            LoadError = ex.Message;
        }
        finally
        {
            IsLoading = false;
            NotifyEditButtonsChanged();
        }
    }

    // Performs the read + ApplyRegisters + baseline update, but does NOT pause/resume polling.
    // Both LoadAsync and SaveAsync call this — Save needs to hold a single pause across the
    // write batch AND the confirmation re-read, because releasing the lock briefly between
    // them lets the polling loop reconnect mid-cycle and the KS-3000 has been observed to
    // drop subsequent connections when that happens immediately after a write batch.
    private async Task DoReadFromDeviceAsync(DeviceConfigProfile profile)
    {
        var read = await _configService.ReadAsync(
            Device.Device, profile.AllFields, CancellationToken.None);
        ApplyRegisters(read.Values, profile);

        if (read.FailedBlocks.Count == 0)
        {
            _baseline = read.Values;
            LoadError = null;
        }
        else
        {
            _baseline = null;
            LoadError =
                $"Falha ao ler {read.FailedBlocks.Count} bloco(s) após múltiplas tentativas. " +
                "Alguns campos podem estar vazios ou desatualizados — não grave configurações até a leitura completar:\n  • " +
                string.Join("\n  • ", read.FailedBlocks);
        }
    }

    [RelayCommand]
    private Task RetryReadAsync() => LoadAsync();

    [RelayCommand]
    private void CancelEdit()
    {
        if (!CanCancel) return;
        if (_baseline is not null && _profile is not null)
            ApplyRegisters(_baseline, _profile); // revert in-memory edits to the last successful read
        SaveError = null;
    }

    // Reverts only the fields that would trigger a mass-memory reset (IoT grandezas +
    // send interval) back to the last read values, leaving unrelated edits untouched.
    // Called when the user declines the mass-memory-reset confirmation on Save.
    private void RevertIotMemoryFields()
    {
        if (_baseline is null || _profile is null) return;
        if (HasIotGrandezaSelection)
            ApplyGrandezasFromRegs(_baseline, _profile);
        if (_profile.AddrSendInterval?.ExtractValue(_baseline) is uint si)
            SendInterval = si;
    }

    [RelayCommand]
    private void UseNow()
    {
        if (!IsReady || ClockSyncFromPc) return;
        SetClockFromPc();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!CanSave || _baseline is null || _profile is null) return;

        IsSaving = true;
        SaveError = null;
        SaveSuccess = null;
        try
        {
            var ops = BuildWriteOperations(_baseline, _profile, out bool needsCoilReset, out bool needsIotBufferReset);

            if (ops.Count == 0 && !needsCoilReset)
            {
                // Nothing changed — exit silently without hitting the device.
                return;
            }

            // When IoT grandezas / send interval changed, the model-specific IoT buffer
            // reset coil must be pulsed (then a settle wait) before the commit/reset coil.
            ushort? iotBufferCoil = needsIotBufferReset ? _profile.IotBufferResetCoil : null;

            // That coil reinitializes the meter's mass memory (logged data is lost), so ask the
            // user to confirm before touching the device. Declining reverts the IoT fields that
            // would trigger the reset (grandezas + send interval) back to the last read values —
            // so the screen returns to a consistent state and a later save won't reset memory —
            // while keeping any unrelated edits. The save itself is aborted (no device writes).
            if (iotBufferCoil is not null && ConfirmMassMemoryReset is not null)
            {
                bool proceed = await ConfirmMassMemoryReset();
                if (!proceed)
                {
                    RevertIotMemoryFields();
                    return;
                }
            }

            // Build a single batch so the service can run every write (and the optional
            // commit coil) against ONE persistent connection. Doing them one-by-one
            // reopens TCP per call, which adds seconds of handshake latency per field
            // and can trip the 30s per-call timeout when editing several fields at once.
            var batch = ops
                .Select(o => new RegisterWrite(o.ModiconAddr, o.Words))
                .ToList();

            // Hold a single pause across the write batch, all reboot-retries and the
            // confirmation re-read. Releasing the lock in-between lets the polling loop
            // reconnect immediately, which has been observed to put the KS-3000 in a
            // transient state where the next read connection is reset with WSAECONNABORTED.
            await _pausePolling();
            var loc = Modbus.Desktop.Services.LocalizationService.Instance;
            try
            {
                // Resume-after-reboot loop. When the device reboots in the middle of a
                // batch (typical after Wi-Fi / Ethernet config writes), WriteBatchAsync
                // returns the unfinished ops in Remaining; we wait for the device to come
                // back and re-submit them. Bounded by MaxRebootRetries to avoid loops if
                // a specific op consistently triggers a reboot.
                const int MaxRebootRetries = 3;
                IReadOnlyList<RegisterWrite> pending = batch;
                bool stillNeedsCoilReset = needsCoilReset;
                int totalCompleted = 0;
                int totalOps = batch.Count;
                bool gaveUp = false;

                for (int attempt = 0; attempt <= MaxRebootRetries; attempt++)
                {
                    var result = await _configService.WriteBatchAsync(
                        Device.Device, pending, stillNeedsCoilReset, iotBufferCoil, CancellationToken.None);

                    totalCompleted += result.Completed;
                    // If the coil reset was deferred (reboot interrupted before it ran),
                    // try again on the next attempt.
                    stillNeedsCoilReset = stillNeedsCoilReset && !result.CoilResetSent;

                    if (!result.DeviceRebooted || result.Remaining.Count == 0)
                    {
                        pending = result.Remaining;
                        break;
                    }

                    if (attempt == MaxRebootRetries)
                    {
                        // Used all the retries — leave the loop with pending still populated;
                        // the post-loop block surfaces the partial outcome.
                        pending = result.Remaining;
                        gaveUp = true;
                        break;
                    }

                    // Reboot detected — show progress and wait for the device to come back.
                    SaveSuccess = string.Format(loc["CfgSavePartialReboot"], totalCompleted, totalOps);
                    var back = await _configService.WaitForDeviceReachableAsync(
                        Device.Device, maxWaitSeconds: 60, CancellationToken.None);
                    if (!back)
                    {
                        SaveSuccess = null;
                        SaveError = string.Format(loc["CfgSaveDeviceDownAfterPartial"], totalCompleted, totalOps);
                        return;
                    }
                    pending = result.Remaining;
                }

                if (gaveUp || pending.Count > 0)
                {
                    SaveSuccess = null;
                    SaveError = string.Format(loc["CfgSaveStillIncomplete"], totalCompleted, totalOps, MaxRebootRetries);
                    return;
                }

                // All ops landed — mark success and try the confirmation re-read.
                SaveSuccess = loc["CfgSaveSuccess"];

                // RTU slave address change: must be done last because it causes a device reboot.
                // After the write we update SlaveId in memory and in the DB, then fall through
                // to the normal WaitForDeviceReachableAsync + post-save re-read below —
                // which will use the updated SlaveId automatically. No need to navigate back.
                if (IsRtu
                    && EditableSlaveId >= 1 && EditableSlaveId <= 247
                    && EditableSlaveId != Device.Device.SlaveId)
                {
                    SaveSuccess = loc["WritingAddress"];
                    await _configService.WriteSlaveAddressAsync(
                        Device.Device, (byte)EditableSlaveId, CancellationToken.None);

                    // Update in memory before the reachability probe so that
                    // WaitForDeviceReachableAsync (and any subsequent reads) target the new address.
                    Device.Device.SlaveId = (byte)EditableSlaveId;
                    await _deviceRepository.UpdateAsync(Device.Device, CancellationToken.None);
                    Device.NotifySlaveIdChanged();

                    SaveSuccess = loc["WaitingForReboot"];
                }

                // Even after a clean batch, the device may have rebooted on the very last op
                // (coil reset, certain string writes). Probe before re-reading so the re-read
                // doesn't fire against a dead host.
                bool reachable = await _configService.WaitForDeviceReachableAsync(
                    Device.Device, maxWaitSeconds: 60, CancellationToken.None);

                if (reachable && _profile is not null)
                {
                    // The post-save re-read is informational — it refreshes the baseline so
                    // the user can keep editing without reopening the screen. If it fails
                    // (timeout, partial blocks, IO error), the WRITES themselves are still
                    // confirmed done; we just soften the success banner with a note and
                    // suppress the load-error banner. Never let a re-read failure flip the
                    // save outcome to "Erro ao gravar configurações" — that would be a lie.
                    try
                    {
                        await DoReadFromDeviceAsync(_profile);
                        if (!string.IsNullOrEmpty(LoadError))
                        {
                            SaveSuccess = loc["CfgSaveSuccessReadIncomplete"];
                            LoadError = null;
                        }
                    }
                    catch (Exception readEx)
                    {
                        Debug.WriteLine($"[DeviceConfigureViewModel] post-save re-read failed (soft): {readEx.GetType().Name} — {readEx.Message}");
                        SaveSuccess = loc["CfgSaveSuccessReadIncomplete"];
                        LoadError = null;
                    }
                }
                else
                {
                    SaveSuccess = loc["CfgSaveSuccessDeviceRebooting"];
                }
            }
            finally
            {
                _resumePolling();
            }
        }
        catch (Exception ex)
        {
            // Real save failure — clear any tentative success message so the red banner
            // is the single source of truth (avoids "Erro" + "Configurações gravadas"
            // showing at the same time).
            SaveSuccess = null;
            SaveError = ex.Message;
            Debug.WriteLine($"[DeviceConfigureViewModel] SaveAsync failed: {ex}");
        }
        finally
        {
            IsSaving = false;
        }
    }

    private void ApplyRegisters(IReadOnlyDictionary<ushort, ushort> regs, DeviceConfigProfile p)
    {
        // ── General ──────────────────────────────────────────────────────────
        if (DecodeFloat32(regs, p.AddrTp)           is decimal tp) Tp           = tp;
        if (DecodeFloat32(regs, p.AddrTc)           is decimal tc) Tc           = tc;
        if (DecodeFloat32(regs, p.AddrHourmeterThr) is decimal hm) HourmeterThr = hm;
        // KE is stored byte-swapped on KS-3000 like the other single 16-bit ints (Timezone/SyncInterval).
        if (p.AddrKe?.ExtractValue(regs) is uint ke)        Ke = SwapBytes((ushort)ke);
        if (p.AddrTl?.ExtractValue(regs) is uint tl)
            Tl = TlOptions.FirstOrDefault(o => o.Code == (int)tl);
        if (p.AddrTi?.ExtractValue(regs) is uint ti)        Ti = ti;
        if (p.AddrCurrentInvert?.ExtractValue(regs) is uint ci) CurrentInvert = ci != 0;
        if (p.AddrSeqPf?.ExtractValue(regs) is uint sq)
            ApplySeqPf((ushort)sq);

        // ── Ethernet ─────────────────────────────────────────────────────────
        // DNS settings are shared with Wireless — handled in the Wireless block below.
        if (p.AddrDhcp?.ExtractValue(regs)       is uint eDhcp)  EthDhcp     = eDhcp != 0;
        if (p.AddrIpAddress?.ExtractValue(regs)  is uint eIp)    EthIp       = FormatIp(eIp);
        if (p.AddrSubnetMask?.ExtractValue(regs) is uint eMsk)   EthMask     = FormatIp(eMsk);
        if (p.AddrGateway?.ExtractValue(regs)    is uint eGw)    EthGateway  = FormatIp(eGw);
        if (p.AddrMacAddress is RegisterField eMac) EthMac = FormatMac(regs, eMac);

        // ── Wireless ─────────────────────────────────────────────────────────
        Ssid           = p.AddrSsid?.ExtractString(regs);
        WifiPassword   = p.AddrWifiPassword?.ExtractString(regs);
        ModuleVersion  = FormatVersion(regs, p.AddrModuleVersion);
        BtDescription  = p.AddrBtDescription?.ExtractString(regs);
        BtPassword     = p.AddrBtPassword?.ExtractString(regs);

        if (p.AddrWirelessMode?.ExtractValue(regs) is uint wMode)
            WirelessMode = WirelessModeOptions.FirstOrDefault(o => o.Code == (int)wMode);
        if (p.AddrWifiDhcp?.ExtractValue(regs) is uint wDhcp)        WifiDhcp       = wDhcp != 0;
        if (p.AddrWifiDnsEnabled?.ExtractValue(regs) is uint wDnsEn) WifiDnsEnabled = wDnsEn != 0;
        if (p.AddrWifiIp?.ExtractValue(regs)      is uint wIp)  WifiIp      = FormatIp(wIp);
        if (p.AddrWifiMask?.ExtractValue(regs)    is uint wMsk) WifiMask    = FormatIp(wMsk);
        if (p.AddrWifiGateway?.ExtractValue(regs) is uint wGw)  WifiGateway = FormatIp(wGw);
        if (p.AddrWifiDns?.ExtractValue(regs)     is uint wDns) WifiDns     = FormatIp(wDns);
        if (p.AddrWifiMac is RegisterField wMac) WifiMac = FormatMac(regs, wMac);
        if (p.AddrBtMac   is RegisterField bMac) BtMac   = FormatMac(regs, bMac);

        // ── SNTP ─────────────────────────────────────────────────────────────
        if (p.AddrSntpEnabled?.ExtractValue(regs) is uint sntpEn) SntpEnabled = sntpEn != 0;
        // KS-3000 stores single 16-bit numeric registers byte-swapped (little-endian byte order).
        if (p.AddrTimezone?.ExtractValue(regs) is uint tz)
            Timezone = (short)SwapBytes((ushort)tz);
        if (p.AddrSyncInterval?.ExtractValue(regs) is uint si)
            SyncInterval = SwapBytes((ushort)si);
        NtpServer = p.AddrNtpServer?.ExtractString(regs);

        // ── IoT ──────────────────────────────────────────────────────────────
        if (p.AddrIotEnabled?.ExtractValue(regs) is uint iotEn)    IotEnabled = iotEn != 0;
        if (p.AddrSendOnHour?.ExtractValue(regs) is uint soh)      SendOnHour = soh != 0;
        // KeepAlive (bit 10 of 40007) and TLS (bit 10 of 40020) use "1 = disabled" semantics
        // per the KS-3000 spec — invert so the checkboxes mean "enabled".
        if (p.AddrKeepAlive?.ExtractValue(regs)  is uint ka)       KeepAlive  = ka == 0;
        if (p.AddrTls?.ExtractValue(regs)        is uint tls)      Tls        = tls == 0;
        if (p.AddrSendInterval?.ExtractValue(regs) is uint sInt)   SendInterval = sInt;
        if (p.AddrMqttBroker?.ExtractValue(regs) is uint mb)
            MqttBroker = MqttBrokerOptions.FirstOrDefault(o => o.Code == (int)mb);
        MqttPort    = p.AddrMqttPort?.ExtractString(regs);
        MqttUrl     = p.AddrMqttUrl?.ExtractString(regs);
        MqttDescId  = p.AddrMqttDescId?.ExtractString(regs);
        MqttTopic   = p.AddrMqttTopic?.ExtractString(regs);
        MqttUser    = p.AddrMqttUser?.ExtractString(regs);
        MqttToken   = p.AddrMqttToken?.ExtractString(regs);

        // ── Clock ────────────────────────────────────────────────────────────
        if (p.AddrClockTime is RegisterField ct
            && ct.ExtractTime(regs) is { } tParts)
            ClockTime = new TimeSpan(tParts.Hora, tParts.Minuto, tParts.Segundo);

        if (p.AddrClockDate is RegisterField cd
            && cd.ExtractDate(regs) is { } dParts)
        {
            try
            {
                ClockDate = new DateTimeOffset(
                    new DateTime(dParts.Ano, dParts.Mes, dParts.Dia), TimeSpan.Zero);
            }
            catch (ArgumentOutOfRangeException) { /* device sent invalid date — ignore */ }
        }

        // ── Inputs / Outputs ─────────────────────────────────────────────────
        if (p.AddrDebounceEdp?.ExtractValue(regs) is uint deb) DebounceEdp = deb;

        // ── Grandezas selecionadas ───────────────────────────────────────────
        if (HasIotGrandezaSelection)
            ApplyGrandezasFromRegs(regs, p);
    }

    private static decimal? DecodeFloat32(
        IReadOnlyDictionary<ushort, ushort> regs, RegisterField? field)
    {
        if (field is not RegisterField f) return null;
        if (!regs.TryGetValue(f.Addr, out var w0)) return null;
        if (!regs.TryGetValue((ushort)(f.Addr + 1), out var w1)) return null;
        return (decimal)RegisterDecoder.Decode(
            new[] { w0, w1 }, Modbus.Core.Domain.Enums.DataType.Float32, Modbus.Core.Domain.Enums.WordOrder.ByteSwapped);
    }

    // KS-3000 stores single 16-bit numeric registers byte-swapped.
    private static ushort SwapBytes(ushort v) => (ushort)((v << 8) | (v >> 8));

    // KS-3000 stores IP/Mask/Gateway/DNS in fully reversed byte order (low byte first).
    private static string FormatIp(uint v) =>
        $"{v & 0xFF}.{(v >> 8) & 0xFF}.{(v >> 16) & 0xFF}.{(v >> 24) & 0xFF}";

    // KS-3000 stores MAC in fully reversed byte order. We read 3 words → 6 bytes, then reverse.
    private static string? FormatMac(IReadOnlyDictionary<ushort, ushort> regs, RegisterField f)
    {
        var bytes = new byte[6];
        for (int i = 0; i < 3; i++)
        {
            if (!regs.TryGetValue((ushort)(f.Addr + i), out var w)) return null;
            bytes[i * 2]     = (byte)(w >> 8);
            bytes[i * 2 + 1] = (byte)(w & 0xFF);
        }
        Array.Reverse(bytes);
        return $"{bytes[0]:X2}:{bytes[1]:X2}:{bytes[2]:X2}:{bytes[3]:X2}:{bytes[4]:X2}:{bytes[5]:X2}";
    }

    // Module version: 4 bytes in major.minor.patch.build order (NOT a string).
    private static string? FormatVersion(
        IReadOnlyDictionary<ushort, ushort> regs, RegisterField? field)
    {
        if (field is not RegisterField f) return null;
        if (!regs.TryGetValue(f.Addr, out var w0)) return null;
        if (!regs.TryGetValue((ushort)(f.Addr + 1), out var w1)) return null;
        return $"{w0 >> 8}.{w0 & 0xFF}.{w1 >> 8}.{w1 & 0xFF}";
    }

    // ── Save: dirty-diff and write-op builder ────────────────────────────────

    private readonly record struct WriteOp(ushort ModiconAddr, ushort[] Words);

    private List<WriteOp> BuildWriteOperations(
        IReadOnlyDictionary<ushort, ushort> baseline,
        DeviceConfigProfile p,
        out bool needsCoilReset,
        out bool needsIotBufferReset)
    {
        var ops = new List<WriteOp>();
        needsCoilReset = false;
        needsIotBufferReset = false;

        // ── Bit-fields and single-register bytes that share a holding register ──
        // Collect every dirty bit-field by its parent register; then for each register,
        // merge them on top of the baseline value via ApplyBits and emit one FC06.
        // Whole single-register fields (Ke, SQPF, byte-swapped 16-bit, etc.) emit
        // their own FC06 below.
        var bitChanges = new List<(RegisterField Field, ushort NewValue)>();

        AddBitDirty(p.AddrCurrentInvert, () => CurrentInvert ? (ushort)1 : (ushort)0);
        AddBitDirty(p.AddrTl,            () => (ushort)(Tl?.Code ?? 0));
        AddBitDirty(p.AddrTi,            () => (ushort)(Ti ?? 0));

        AddBitDirty(p.AddrDhcp,            () => EthDhcp ? (ushort)1 : (ushort)0);
        AddBitDirty(p.AddrDnsEnabled,      () => WifiDnsEnabled ? (ushort)1 : (ushort)0);

        AddBitDirty(p.AddrWirelessMode,    () => (ushort)(WirelessMode?.Code ?? 0));
        AddBitDirty(p.AddrWifiDhcp,        () => WifiDhcp ? (ushort)1 : (ushort)0);
        AddBitDirty(p.AddrWifiDnsEnabled,  () => WifiDnsEnabled ? (ushort)1 : (ushort)0);

        AddBitDirty(p.AddrSntpEnabled,     () => SntpEnabled ? (ushort)1 : (ushort)0);

        AddBitDirty(p.AddrIotEnabled,      () => IotEnabled ? (ushort)1 : (ushort)0);
        AddBitDirty(p.AddrSendOnHour,      () => SendOnHour ? (ushort)1 : (ushort)0);
        // KeepAlive / TLS use "1 = disabled" semantics on the device.
        AddBitDirty(p.AddrKeepAlive,       () => KeepAlive ? (ushort)0 : (ushort)1);
        AddBitDirty(p.AddrTls,             () => Tls       ? (ushort)0 : (ushort)1);
        AddBitDirty(p.AddrMqttBroker,      () => (ushort)(MqttBroker?.Code ?? 0));

        foreach (var grp in bitChanges.GroupBy(c => c.Field.Addr))
        {
            if (!baseline.TryGetValue(grp.Key, out var baseValue)) continue;
            ushort merged = baseValue;
            foreach (var (field, newValue) in grp)
                merged = field.ApplyBits(merged, newValue);
            if (merged != baseValue)
                ops.Add(new WriteOp(grp.Key, new[] { merged }));
        }

        // ── Whole single-register integers ───────────────────────────────────
        AddSingleDirty(p.AddrKe,           () => SwapBytes((ushort)(Ke ?? 0)));
        AddSingleDirty(p.AddrSendInterval, () => (ushort)(SendInterval ?? 0));
        AddSingleDirty(p.AddrDebounceEdp,  () => (ushort)(DebounceEdp ?? 0));
        // Timezone / SyncInterval are stored byte-swapped on the device.
        AddSingleDirty(p.AddrTimezone,     () => SwapBytes((ushort)(short)(Timezone ?? 0)));
        AddSingleDirty(p.AddrSyncInterval, () => SwapBytes((ushort)(SyncInterval ?? 0)));
        AddSingleDirty(p.AddrSeqPf,        EncodeSeqPf);

        // ── Multi-word Float32 holding registers (byte-swapped) ──────────────
        AddFloat32Dirty(p.AddrTp,           () => Tp);
        AddFloat32Dirty(p.AddrTc,           () => Tc);
        AddFloat32Dirty(p.AddrHourmeterThr, () => HourmeterThr);

        // ── IPv4 addresses (2 words, fully little-endian per KS-3000 quirk) ──
        AddIpDirty(p.AddrIpAddress,  () => EthIp);
        AddIpDirty(p.AddrSubnetMask, () => EthMask);
        AddIpDirty(p.AddrGateway,    () => EthGateway);
        AddIpDirty(p.AddrWifiIp,     () => WifiIp);
        AddIpDirty(p.AddrWifiMask,   () => WifiMask);
        AddIpDirty(p.AddrWifiGateway,() => WifiGateway);
        // DNS server is shared — write whichever address the profile exposes.
        AddIpDirty(p.AddrWifiDns ?? p.AddrDnsServer, () => WifiDns);

        // ── Strings ──────────────────────────────────────────────────────────
        AddStringDirty(p.AddrSsid,         () => Ssid);
        AddStringDirty(p.AddrWifiPassword, () => WifiPassword);
        AddStringDirty(p.AddrBtDescription,() => BtDescription);
        AddStringDirty(p.AddrBtPassword,   () => BtPassword);
        AddStringDirty(p.AddrNtpServer,    () => NtpServer);
        AddStringDirty(p.AddrMqttUrl,      () => MqttUrl);
        AddStringDirty(p.AddrMqttDescId,   () => MqttDescId);
        AddStringDirty(p.AddrMqttPort,     () => MqttPort);
        AddStringDirty(p.AddrMqttTopic,    () => MqttTopic);
        AddStringDirty(p.AddrMqttUser,     () => MqttUser);
        AddStringDirty(p.AddrMqttToken,    () => MqttToken);

        // ── Clock ────────────────────────────────────────────────────────────
        // In PC sync mode, the timer keeps overwriting ClockDate/ClockTime so the baseline
        // comparison is unreliable. Always write when in PC mode (intent: "sync now").
        // In manual mode, write only if the user's chosen values differ from baseline.
        if (p.AddrClockTime is RegisterField ctField && p.AddrClockDate is RegisterField cdField)
        {
            DateTime? when = null;
            if (ClockSyncFromPc)
            {
                when = DateTime.Now;
            }
            else if (ClockDate is { } d && ClockTime is { } t)
            {
                var baselineTime = ctField.ExtractTime(baseline);
                var baselineDate = cdField.ExtractDate(baseline);

                bool timeChanged = baselineTime is null
                    || baselineTime.Value.Hora    != (byte)t.Hours
                    || baselineTime.Value.Minuto  != (byte)t.Minutes
                    || baselineTime.Value.Segundo != (byte)t.Seconds;
                bool dateChanged = baselineDate is null
                    || baselineDate.Value.Ano != d.Year
                    || baselineDate.Value.Mes != (byte)d.Month
                    || baselineDate.Value.Dia != (byte)d.Day;

                if (timeChanged || dateChanged)
                    when = new DateTime(d.Year, d.Month, d.Day, t.Hours, t.Minutes, t.Seconds);
            }

            if (when is { } now)
            {
                var (tw0, tw1) = RegisterField.EncodeTime(now.Hour, now.Minute, now.Second);
                var (dw0, dw1) = RegisterField.EncodeDate(now.Year, now.Month, now.Day, (int)now.DayOfWeek + 1);
                ops.Add(new WriteOp(ctField.Addr, new[] { tw0, tw1 }));
                ops.Add(new WriteOp(cdField.Addr, new[] { dw0, dw1 }));
            }
        }

        // ── IoT grandezas slot blocks ────────────────────────────────────────
        // Build the full 50-slot array from SelectedOrdered (pad empty with 0xFFFF),
        // then emit one WriteOp per profile field if any word in that field differs
        // from the baseline. WriteMultipleRegistersAsync handles the FC16 22-word chunking.
        AddGrandezaBlockDirty(p.AddrGrandezasSlots1to20,  startSlot: 0);
        AddGrandezaBlockDirty(p.AddrGrandezasSlots21to50, startSlot: 20);

        // O KS-3000 exige o coil 6 (FC05) ao final de QUALQUER gravação para commitar
        // as configurações — o medidor reinicia ao recebê-lo. Dispara sempre que houver
        // ao menos uma escrita (uma ou mais configurações).
        if (ops.Count > 0)
            needsCoilReset = true;

        // Alterar as grandezas IoT e/ou o intervalo de envio exige limpar o buffer IoT /
        // resetar a memória de massa (coil específico por modelo) ANTES do coil de reset.
        bool TargetsField(RegisterField? f) =>
            f is RegisterField rf && ops.Any(o => o.ModiconAddr == rf.Addr);
        if (TargetsField(p.AddrGrandezasSlots1to20)
            || TargetsField(p.AddrGrandezasSlots21to50)
            || TargetsField(p.AddrSendInterval))
            needsIotBufferReset = true;

        return ops;

        // ── Local helpers ────────────────────────────────────────────────────
        void AddBitDirty(RegisterField? field, Func<ushort> currentFactory)
        {
            if (field is not RegisterField f) return;
            var existing = f.ExtractValue(baseline);
            var current  = currentFactory();
            if (existing is null || existing.Value != current)
                bitChanges.Add((f, current));
        }

        void AddSingleDirty(RegisterField? field, Func<ushort> currentFactory)
        {
            if (field is not RegisterField f) return;
            if (f.IsBitField) return; // safety: bit-fields use AddBitDirty
            var existing = f.ExtractValue(baseline);
            var current  = currentFactory();
            if (existing is null || existing.Value != current)
                ops.Add(new WriteOp(f.Addr, new[] { current }));
        }

        void AddFloat32Dirty(RegisterField? field, Func<decimal?> currentFactory)
        {
            if (field is not RegisterField f) return;
            if (currentFactory() is not decimal current) return;
            var existing = DecodeFloat32(baseline, f);
            if (existing.HasValue && Math.Abs(existing.Value - current) < 0.0001m) return;
            var words = RegisterDecoder.EncodeFloat32((float)current, WordOrder.ByteSwapped);
            ops.Add(new WriteOp(f.Addr, words));
        }

        void AddIpDirty(RegisterField? field, Func<string?> currentFactory)
        {
            if (field is not RegisterField f) return;
            var current  = currentFactory();
            var existing = f.ExtractValue(baseline) is uint v ? FormatIp(v) : null;
            if (string.Equals(existing, current, StringComparison.Ordinal)) return;
            if (ParseIp(current) is not ushort[] words) return; // skip when malformed/null
            ops.Add(new WriteOp(f.Addr, words));
        }

        void AddStringDirty(RegisterField? field, Func<string?> currentFactory)
        {
            if (field is not RegisterField f) return;
            var current  = currentFactory() ?? string.Empty;
            var existing = f.ExtractString(baseline) ?? string.Empty;
            if (string.Equals(existing, current, StringComparison.Ordinal)) return;
            ops.Add(new WriteOp(f.Addr, f.EncodeString(current)));
        }

        // Build the desired words for the slice [startSlot .. startSlot + f.WordCount) from
        // SelectedOrdered (one ushort MqttId per slot, 0xFFFF for empty), then emit one
        // WriteOp covering the whole field if any word differs from the baseline.
        void AddGrandezaBlockDirty(RegisterField? field, int startSlot)
        {
            if (field is not RegisterField f) return;

            var desired = new ushort[f.WordCount];
            for (int i = 0; i < f.WordCount; i++)
            {
                int slotIdx = startSlot + i;
                desired[i] = slotIdx < SelectedOrdered.Count
                    ? SelectedOrdered[slotIdx].MqttId
                    : (ushort)0xFFFF;
            }

            bool dirty = false;
            for (int i = 0; i < f.WordCount; i++)
            {
                if (!baseline.TryGetValue((ushort)(f.Addr + i), out var baseWord) || baseWord != desired[i])
                {
                    dirty = true;
                    break;
                }
            }
            if (dirty)
                ops.Add(new WriteOp(f.Addr, desired));
        }

        ushort EncodeSeqPf()
        {
            // Inverse of ApplySeqPf: label → nibble value, placed HIGH-to-LOW.
            // Mapping: EXP→0, F0→1, F1→2, F2→3 (KRON convention).
            ushort raw = 0;
            for (int i = 0; i < 4; i++)
            {
                int nibble = _pfPos[i] switch
                {
                    "EXP" => 0,
                    "F0"  => 1,
                    "F1"  => 2,
                    "F2"  => 3,
                    _      => 0,
                };
                int destNibbleIdx = 3 - i;
                raw |= (ushort)(nibble << (destNibbleIdx * 4));
            }
            return raw;
        }
    }

    // Inverse of FormatIp. Returns null when the text isn't a valid IPv4 (skip the write).
    private static ushort[]? ParseIp(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var parts = text.Split('.');
        if (parts.Length != 4) return null;
        var octets = new byte[4];
        for (int i = 0; i < 4; i++)
        {
            if (!byte.TryParse(parts[i], out octets[i])) return null;
        }
        // word0 = (d << 8) | c ; word1 = (b << 8) | a  — mirrors the byte order assumed by FormatIp.
        return new[]
        {
            (ushort)((octets[3] << 8) | octets[2]),
            (ushort)((octets[1] << 8) | octets[0]),
        };
    }

    // KRON SQPF convention (verified against the old KRON software with multiple
    // ground-truth sequences):
    //   • Display order is HIGH-to-LOW: leftmost position (PfPos0) is nibble 3
    //     (bits 15-12 of the raw register), rightmost (PfPos3) is nibble 0 (bits 3-0).
    //   • Nibble value → label mapping: 0=EXP, 1=F0, 2=F1, 3=F2.
    // Default raw 0x3210 displays as "F2, F1, F0, EXP".
    private static readonly string[] SeqPfLabels = ["EXP", "F0", "F1", "F2"];

    private void ApplySeqPf(ushort raw)
    {
        for (int i = 0; i < 4; i++)
        {
            int srcNibbleIdx = 3 - i;
            int nibble = (raw >> (srcNibbleIdx * 4)) & 0xF;
            if (nibble < 0 || nibble > 3) return; // bail on invalid SQPF
            _pfPos[i] = SeqPfLabels[nibble];
            OnPropertyChanged($"PfPos{i}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void GoBack()
    {
        _clockTimer?.Stop();
        _onGoBack();
    }
}
