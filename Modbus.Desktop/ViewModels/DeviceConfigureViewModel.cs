using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Modbus.Core.Domain.Enums;
using Modbus.Core.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Modbus.Desktop.ViewModels;

public partial class DeviceConfigureViewModel : ObservableObject
{
    private readonly Action _onGoBack;
    private readonly IDeviceConfigService _configService;
    private readonly Func<Task> _suspendRtuPolling;
    private readonly Action _resumeRtuPolling;

    public DeviceItemViewModel Device { get; }

    // ── Section navigation ───────────────────────────────────────────────────
    [ObservableProperty] private int _selectedSectionIndex;

    public bool IsGeneral       => SelectedSectionIndex == 0;
    public bool IsEthernet      => SelectedSectionIndex == 1;
    public bool IsWireless      => SelectedSectionIndex == 2;
    public bool IsSntp          => SelectedSectionIndex == 3;
    public bool IsIot           => SelectedSectionIndex == 4;
    public bool IsClock         => SelectedSectionIndex == 5;
    public bool IsInputsOutputs => SelectedSectionIndex == 6;

    partial void OnSelectedSectionIndexChanged(int value)
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
    [ObservableProperty] private decimal? _tl;
    [ObservableProperty] private decimal? _ti;
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

    // ── Wireless ─────────────────────────────────────────────────────────────
    [ObservableProperty] private string? _ssid;
    [ObservableProperty] private string? _wifiPassword;
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
    [ObservableProperty] private bool _iotEnabled;
    [ObservableProperty] private bool _sendOnHour;
    [ObservableProperty] private bool _keepAlive;
    [ObservableProperty] private bool _tls;
    [ObservableProperty] private decimal? _sendInterval;
    [ObservableProperty] private decimal? _mqttPort;
    [ObservableProperty] private string? _mqttUrl;
    [ObservableProperty] private string? _mqttDescId;
    [ObservableProperty] private string? _mqttTopic;
    [ObservableProperty] private string? _mqttUser;
    [ObservableProperty] private string? _mqttToken;

    // ── Clock ────────────────────────────────────────────────────────────────
    [ObservableProperty] private DateTimeOffset? _clockDate;
    [ObservableProperty] private TimeSpan? _clockTime;

    // ── Inputs / Outputs ─────────────────────────────────────────────────────
    [ObservableProperty] private decimal? _debounceEdp;

    // ── Constructor / Commands ───────────────────────────────────────────────
    public DeviceConfigureViewModel(
        DeviceItemViewModel device,
        IDeviceConfigService configService,
        Func<Task> suspendRtuPolling,
        Action resumeRtuPolling,
        Action onGoBack)
    {
        Device              = device;
        _configService      = configService;
        _suspendRtuPolling  = suspendRtuPolling;
        _resumeRtuPolling   = resumeRtuPolling;
        _onGoBack           = onGoBack;
        _editableSlaveId    = device.SlaveId;
        _description        = device.Name;
    }

    public async Task LoadAsync()
    {
        var profile = DeviceConfigProfileRegistry.Get(Device.Device.DeviceModel?.DeviceCode);
        if (profile is null || profile.AllAddresses.Count == 0) return;

        IsLoading = true;
        LoadError = null;
        try
        {
            bool isRtu = Device.Device.TransportType == TransportType.Rtu;
            if (isRtu) await _suspendRtuPolling();
            try
            {
                var regs = await _configService.ReadAsync(
                    Device.Device, profile.AllAddresses, CancellationToken.None);
                ApplyRegisters(regs, profile);
            }
            finally
            {
                if (isRtu) _resumeRtuPolling();
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
        if (p.AddrTp?.ExtractValue(regs) is uint tpRaw)
            Tp = (decimal)BitConverter.Int32BitsToSingle((int)tpRaw);
        if (p.AddrTc?.ExtractValue(regs) is uint tcRaw)
            Tc = (decimal)BitConverter.Int32BitsToSingle((int)tcRaw);
        if (p.AddrKe?.ExtractValue(regs) is uint ke)        Ke = ke;
        if (p.AddrTl?.ExtractValue(regs) is uint tl)        Tl = tl;
        if (p.AddrTi?.ExtractValue(regs) is uint ti)        Ti = ti;
        if (p.AddrCurrentInvert?.ExtractValue(regs) is uint ci) CurrentInvert = ci != 0;
        if (p.AddrHourmeterThr?.ExtractValue(regs) is uint hmRaw)
            HourmeterThr = (decimal)BitConverter.Int32BitsToSingle((int)hmRaw);
        if (p.AddrSeqPf?.ExtractValue(regs) is uint sq)
            ApplySeqPf((ushort)sq);

        // ── Wireless ─────────────────────────────────────────────────────────
        Ssid           = p.AddrSsid?.ExtractString(regs);
        WifiPassword   = p.AddrWifiPassword?.ExtractString(regs);
        ModuleVersion  = p.AddrModuleVersion?.ExtractString(regs);
        BtDescription  = p.AddrBtDescription?.ExtractString(regs);
        BtPassword     = p.AddrBtPassword?.ExtractString(regs);

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
        if (p.AddrTimezone?.ExtractValue(regs) is uint tz)
            Timezone = (short)(ushort)tz;
        if (p.AddrSyncInterval?.ExtractValue(regs) is uint si) SyncInterval = si;
        NtpServer = p.AddrNtpServer?.ExtractString(regs);

        // ── IoT ──────────────────────────────────────────────────────────────
        if (p.AddrIotEnabled?.ExtractValue(regs) is uint iotEn)    IotEnabled = iotEn != 0;
        if (p.AddrSendOnHour?.ExtractValue(regs) is uint soh)      SendOnHour = soh != 0;
        if (p.AddrKeepAlive?.ExtractValue(regs)  is uint ka)       KeepAlive  = ka != 0;
        if (p.AddrTls?.ExtractValue(regs)        is uint tls)      Tls        = tls != 0;
        if (p.AddrSendInterval?.ExtractValue(regs) is uint sInt)   SendInterval = sInt;
        if (p.AddrMqttPort?.ExtractValue(regs)     is uint mPort)  MqttPort     = mPort;
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
    }

    private static string FormatIp(uint v) =>
        $"{(v >> 24) & 0xFF}.{(v >> 16) & 0xFF}.{(v >> 8) & 0xFF}.{v & 0xFF}";

    private static string? FormatMac(IReadOnlyDictionary<ushort, ushort> regs, RegisterField f)
    {
        var bytes = new byte[6];
        for (int i = 0; i < 3; i++)
        {
            if (!regs.TryGetValue((ushort)(f.Addr + i), out var w)) return null;
            bytes[i * 2]     = (byte)(w >> 8);
            bytes[i * 2 + 1] = (byte)(w & 0xFF);
        }
        return $"{bytes[0]:X2}:{bytes[1]:X2}:{bytes[2]:X2}:{bytes[3]:X2}:{bytes[4]:X2}:{bytes[5]:X2}";
    }

    private void ApplySeqPf(ushort raw)
    {
        string[] labels = ["F0", "F1", "F2", "EXP"];
        for (int i = 0; i < 4; i++)
        {
            int nibble = (raw >> (i * 4)) & 0xF;
            if (nibble < 0 || nibble > 3) return; // bail on invalid SQPF
            _pfPos[i] = labels[nibble];
            OnPropertyChanged($"PfPos{i}");
        }
    }

    [RelayCommand]
    private void GoBack() => _onGoBack();
}
