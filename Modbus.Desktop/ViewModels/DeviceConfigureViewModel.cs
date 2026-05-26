using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Modbus.Core.Domain.Enums;
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
    private readonly Func<Task> _pausePolling;
    private readonly Action _resumePolling;

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

    // ── Load state ───────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _loadError;

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
        public GrandezaCategory Category => Model.Category;

        [ObservableProperty] private bool _isSelected;
        [ObservableProperty] private int? _order;
        [ObservableProperty] private bool _isVisible = true;

        public GrandezaItemViewModel(Grandeza model) { Model = model; }

        public bool MatchesSearch(string? query)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;
            return Code.Contains(query, StringComparison.OrdinalIgnoreCase)
                || Description.Contains(query, StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed partial class GrandezaCategoryGroup : ObservableObject
    {
        public GrandezaCategory Category { get; }
        public string Label { get; }
        public IReadOnlyList<GrandezaItemViewModel> Items { get; }

        [ObservableProperty] private bool _isVisible = true;

        public GrandezaCategoryGroup(GrandezaCategory category, string label, IReadOnlyList<GrandezaItemViewModel> items)
        {
            Category = category;
            Label = label;
            Items = items;
        }

        public override string ToString() => Label;
    }

    public ObservableCollection<GrandezaCategoryGroup> AvailableGrouped { get; } = new();
    public ObservableCollection<GrandezaItemViewModel> SelectedOrdered { get; } = new();

    [ObservableProperty] private int _maxGrandezas = 50;
    [ObservableProperty] private string _searchText = string.Empty;

    public int SelectedCount => SelectedOrdered.Count;
    public string GrandezaCounterText =>
        string.Format(
            Modbus.Desktop.Services.LocalizationService.Instance["CfgGrandezaCounter"],
            SelectedCount, MaxGrandezas);

    partial void OnMaxGrandezasChanged(int value) => OnPropertyChanged(nameof(GrandezaCounterText));

    partial void OnSearchTextChanged(string value) => ApplyGrandezaFilter();

    private void ApplyGrandezaFilter()
    {
        var query = SearchText;
        foreach (var group in AvailableGrouped)
        {
            int visibleCount = 0;
            foreach (var item in group.Items)
            {
                var matches = item.MatchesSearch(query);
                item.IsVisible = matches;
                if (matches) visibleCount++;
            }
            group.IsVisible = visibleCount > 0;
        }
    }

    private readonly Dictionary<ushort, GrandezaItemViewModel> _grandezaById = new();

    private void BuildGrandezaCatalog()
    {
        AvailableGrouped.Clear();
        _grandezaById.Clear();

        var deviceCode = Device.Device.DeviceModel?.DeviceCode;
        var catalog = GrandezaCatalog.ForDeviceCode(deviceCode);
        if (catalog.Count == 0) return;

        MaxGrandezas = GrandezaCatalog.Limit(deviceCode, Device.Device.FirmwareVersion);

        var loc = Modbus.Desktop.Services.LocalizationService.Instance;
        foreach (var group in catalog.GroupBy(g => g.Category))
        {
            var items = group
                .Select(g => { var vm = new GrandezaItemViewModel(g); _grandezaById[g.MqttId] = vm; return vm; })
                .ToList();
            AvailableGrouped.Add(new GrandezaCategoryGroup(group.Key, loc[CategoryLocalizationKey(group.Key)], items));
        }
    }

    private static string CategoryLocalizationKey(GrandezaCategory c) => c switch
    {
        GrandezaCategory.Tensao            => "CfgCatTensao",
        GrandezaCategory.Corrente          => "CfgCatCorrente",
        GrandezaCategory.Frequencia        => "CfgCatFrequencia",
        GrandezaCategory.PotenciaAtiva     => "CfgCatPotenciaAtiva",
        GrandezaCategory.PotenciaReativa   => "CfgCatPotenciaReativa",
        GrandezaCategory.PotenciaAparente  => "CfgCatPotenciaAparente",
        GrandezaCategory.FatorPotencia     => "CfgCatFatorPotencia",
        GrandezaCategory.EntradasSaidas    => "CfgCatEntradasSaidas",
        GrandezaCategory.Horimetro         => "CfgCatHorimetro",
        GrandezaCategory.Energia           => "CfgCatEnergia",
        GrandezaCategory.DeltaEnergia      => "CfgCatDeltaEnergia",
        GrandezaCategory.EnergiaPorFase    => "CfgCatEnergiaPorFase",
        _                                  => "CfgCatOutros",
    };

    private void ApplyGrandezasFromRegs(IReadOnlyDictionary<ushort, ushort> regs, DeviceConfigProfile p)
    {
        SelectedOrdered.Clear();
        foreach (var item in _grandezaById.Values)
        {
            item.IsSelected = false;
            item.Order = null;
        }

        var slotAddrs = new List<ushort>();
        if (p.AddrGrandezasSlots1to20 is RegisterField f1)
            for (int i = 0; i < f1.WordCount; i++) slotAddrs.Add((ushort)(f1.Addr + i));
        if (p.AddrGrandezasSlots21to50 is RegisterField f2)
            for (int i = 0; i < f2.WordCount; i++) slotAddrs.Add((ushort)(f2.Addr + i));

        int order = 0;
        foreach (var addr in slotAddrs)
        {
            if (!regs.TryGetValue(addr, out var raw)) continue;
            if (raw == 0xFFFF || raw == 0x0000) continue;

            if (_grandezaById.TryGetValue(raw, out var item))
            {
                order++;
                item.IsSelected = true;
                item.Order = order;
                SelectedOrdered.Add(item);
            }
            else
            {
                Debug.WriteLine($"[Grandezas] ID {raw} no slot {addr} não encontrado no catálogo do modelo.");
            }
        }

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
        Func<Task> pausePolling,
        Action resumePolling,
        Action onGoBack)
    {
        Device              = device;
        _configService      = configService;
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

        IsLoading = true;
        LoadError = null;
        try
        {
            await _pausePolling();
            try
            {
                var read = await _configService.ReadAsync(
                    Device.Device, profile.AllFields, CancellationToken.None);
                ApplyRegisters(read.Values, profile);

                // Surface partial-read failures: data is incomplete and must not be
                // written back to the device until reloaded successfully.
                if (read.FailedBlocks.Count > 0)
                    LoadError =
                        $"Falha ao ler {read.FailedBlocks.Count} bloco(s) após múltiplas tentativas. " +
                        "Alguns campos podem estar vazios ou desatualizados — não grave configurações até a leitura completar:\n  • " +
                        string.Join("\n  • ", read.FailedBlocks);
            }
            finally
            {
                _resumePolling();
            }
        }
        catch (Exception ex)
        {
            LoadError = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyRegisters(IReadOnlyDictionary<ushort, ushort> regs, DeviceConfigProfile p)
    {
        // ── General ──────────────────────────────────────────────────────────
        if (DecodeFloat32(regs, p.AddrTp)           is decimal tp) Tp           = tp;
        if (DecodeFloat32(regs, p.AddrTc)           is decimal tc) Tc           = tc;
        if (DecodeFloat32(regs, p.AddrHourmeterThr) is decimal hm) HourmeterThr = hm;
        if (p.AddrKe?.ExtractValue(regs) is uint ke)        Ke = ke;
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

    private void ApplySeqPf(ushort raw)
    {
        string[] labels = ["F2", "F1", "F0", "EXP"];
        for (int i = 0; i < 4; i++)
        {
            int nibble = (raw >> (i * 4)) & 0xF;
            if (nibble < 0 || nibble > 3) return; // bail on invalid SQPF
            _pfPos[i] = labels[nibble];
            OnPropertyChanged($"PfPos{i}");
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        _clockTimer?.Stop();
        _onGoBack();
    }
}
