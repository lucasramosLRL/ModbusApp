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
        // TODO: map registers to observable properties once addresses are filled in DeviceConfigProfileRegistry.
        // Example:
        //   if (p.AddrSlaveId is ushort sid && regs.TryGetValue(sid, out var slaveId))
        //       EditableSlaveId = slaveId;
    }

    [RelayCommand]
    private void GoBack() => _onGoBack();
}
