using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Modbus.Core.Cloud;
using Modbus.Desktop.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Modbus.Desktop.ViewModels;

/// <summary>
/// Configuration screen for a cloud (MQTT) device, exposing ONLY the parameters the KS firmware
/// can change over MQTT (the "999-999" named commands and the action coils). Empty fields are left
/// unchanged. Quantities that reboot the meter (TL, IA, G1..G20) are sent fire-and-forget.
/// </summary>
public partial class CloudConfigureViewModel : ObservableObject
{
    // Action-coil WIRE addresses (the command encodes COIL = wire + 1):
    private const ushort CoilResetDevice     = 5;   // → "006"
    private const ushort CoilResetEnergies   = 39;  // → "040"
    private const ushort CoilInitHourmeter   = 61;  // → "062"
    private const ushort CoilResetMqttBuffer = 90;  // → "091"

    private readonly ICloudCommandService _commands;
    private readonly Action _onGoBack;

    public DeviceItemViewModel Device { get; }

    // ── Measurement / general (999-999 named fields) ──────────────────────────
    [ObservableProperty] private string _tp = "";
    [ObservableProperty] private string _tc = "";
    [ObservableProperty] private string _tl = "";
    [ObservableProperty] private string _ti = "";
    [ObservableProperty] private string _ke = "";
    [ObservableProperty] private string _thrs = "";
    [ObservableProperty] private string _rt = "";
    [ObservableProperty] private string _ia = "";

    // ── Digital output (relay) ────────────────────────────────────────────────
    [ObservableProperty] private bool _relayOn;

    // ── Cloud quantities G1..G20 ──────────────────────────────────────────────
    public ObservableCollection<GrandezaSlot> Grandezas { get; } = new();

    // ── Feedback ──────────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FeedbackColorHex))]
    private bool _feedbackIsError;

    [ObservableProperty] private string? _feedback;

    public string FeedbackColorHex => FeedbackIsError ? "#D32F2F" : "#388E3C";

    public CloudConfigureViewModel(DeviceItemViewModel device, ICloudCommandService commands, Action onGoBack)
    {
        Device = device;
        _commands = commands;
        _onGoBack = onGoBack;

        for (int i = 1; i <= 20; i++)
            Grandezas.Add(new GrandezaSlot(i));
    }

    [RelayCommand]
    private Task ApplyGeneral() => SendConfigAsync(loc =>
    {
        var fields = new Dictionary<string, string>();
        AddIfPresent(fields, "TP", Tp);
        AddIfPresent(fields, "TC", Tc);
        AddIfPresent(fields, "TL", Tl);
        AddIfPresent(fields, "TI", Ti);
        AddIfPresent(fields, "KE", Ke);
        AddIfPresent(fields, "THRS", Thrs);
        AddIfPresent(fields, "RT", Rt);
        AddIfPresent(fields, "IA", Ia);
        return fields;
    });

    [RelayCommand]
    private Task ApplyRelay() => SendConfigAsync(_ => new Dictionary<string, string> { ["sd1"] = RelayOn ? "1" : "0" });

    [RelayCommand]
    private Task ApplyGrandezas() => SendConfigAsync(_ =>
    {
        var fields = new Dictionary<string, string>();
        foreach (var slot in Grandezas)
            AddIfPresent(fields, slot.Key, slot.Value);
        return fields;
    });

    // ── Action coils ──────────────────────────────────────────────────────────
    [RelayCommand] private Task ResetDevice()     => SendCoilAsync(CoilResetDevice);
    [RelayCommand] private Task ResetEnergies()   => SendCoilAsync(CoilResetEnergies);
    [RelayCommand] private Task InitHourmeter()   => SendCoilAsync(CoilInitHourmeter);
    [RelayCommand] private Task ResetMqttBuffer() => SendCoilAsync(CoilResetMqttBuffer);

    [RelayCommand]
    private void GoBack() => _onGoBack();

    // ── Helpers ───────────────────────────────────────────────────────────────
    private async Task SendConfigAsync(Func<LocalizationService, Dictionary<string, string>> build)
    {
        var loc = LocalizationService.Instance;
        try
        {
            var fields = build(loc);
            if (fields.Count == 0)
            {
                SetFeedback(loc["CloudConfigNothingToSend"], isError: true);
                return;
            }

            await _commands.SendConfigAsync(Device.Device, fields);
            SetFeedback(loc["CloudConfigSent"], isError: false);
        }
        catch (Exception ex)
        {
            SetFeedback(ex.Message, isError: true);
        }
    }

    private async Task SendCoilAsync(ushort wireAddress)
    {
        var loc = LocalizationService.Instance;
        try
        {
            await _commands.WriteCoilAsync(Device.Device, wireAddress, true);
            SetFeedback(loc["CloudConfigSent"], isError: false);
        }
        catch (Exception ex)
        {
            SetFeedback(ex.Message, isError: true);
        }
    }

    private static void AddIfPresent(Dictionary<string, string> fields, string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            fields[key] = value.Trim();
    }

    private void SetFeedback(string message, bool isError)
    {
        FeedbackIsError = isError;
        Feedback = message;
    }

    public partial class GrandezaSlot : ObservableObject
    {
        public int Index { get; }
        public string Key => $"G{Index}";
        public string Label => $"G{Index}";

        [ObservableProperty] private string _value = "";

        public GrandezaSlot(int index) => Index = index;
    }
}
