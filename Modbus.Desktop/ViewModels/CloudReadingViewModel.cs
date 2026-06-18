using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Modbus.Core.Cloud;
using Modbus.Core.Polling;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Modbus.Desktop.ViewModels;

/// <summary>
/// Telemetry-driven readings for a cloud (MQTT) device. Unlike <see cref="DeviceDetailViewModel"/>,
/// this is purely push and fully dynamic: rows are created the first time a quantity appears in a
/// telemetry payload, so the screen shows <b>only</b> what the meter actually publishes to the broker
/// (the user-selected G1..G50). Fields with no register definition (e.g. "CE") still appear, grouped
/// under "Outros", using the raw payload code as label.
/// </summary>
public partial class CloudReadingViewModel : ObservableObject, IDisposable
{
    private readonly IPollingEngine _pollingEngine;
    private readonly Action _onGoBack;

    // Keyed by raw payload field code, so repeat messages update the same row.
    private readonly Dictionary<string, ElectricalReadingViewModel> _readingsByCode = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ReadingGroupViewModel> _groupsByKey = new();

    public DeviceItemViewModel Device { get; }

    public ObservableCollection<ReadingGroupViewModel> ReadingGroups { get; } = new();
    public ObservableCollection<ReadingGroupViewModel> EnergyGroups { get; } = new();

    [ObservableProperty]
    private string _lastUpdate = "—";

    [ObservableProperty]
    private bool _hasData;

    // Group definitions in display order. The first matching predicate wins; quantities matching
    // none fall into the "Outros" group. Classification is on the canonical name (or raw code).
    private const string OtherKey = "GroupOther";
    private sealed record GroupSpec(string Key, bool Energy, Func<string, bool> Match);

    private static readonly GroupSpec[] Specs =
    [
        new("GroupVoltages",      false, n => n.StartsWith("U")),
        new("GroupCurrents",      false, n => n.StartsWith("I")),
        new("GroupFrequency",     false, n => n is "Freq" or "FA" or "F1"),
        new("GroupPowerFactor",   false, n => n.StartsWith("FP")),
        new("GroupActivePower",   false, n => n.StartsWith("P")),
        new("GroupReactivePower", false, n => n.StartsWith("Q")),
        new("GroupApparentPower", false, n => n.StartsWith("S")),
        new("GroupEnergies",      true,  n => n.StartsWith("EA") || n.StartsWith("ER") || n.StartsWith("ES")),
        new("GroupDemands",       true,  n => n.StartsWith("DA") || n.StartsWith("DR") ||
                                              n.StartsWith("DS") || n.StartsWith("DI") || n.StartsWith("MD")),
    ];

    public CloudReadingViewModel(DeviceItemViewModel device, IPollingEngine pollingEngine, Action onGoBack)
    {
        Device = device;
        _pollingEngine = pollingEngine;
        _onGoBack = onGoBack;

        _pollingEngine.TelemetryReceived += OnTelemetryReceived;
    }

    private void OnTelemetryReceived(object? sender, TelemetryReceivedEventArgs e)
    {
        if (e.Device.Id != Device.Id || e.Readings.Count == 0) return;

        Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var reading in e.Readings)
                GetOrCreateReading(reading).Update(reading.Value);

            HasData = true;
            LastUpdate = e.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        });
    }

    private ElectricalReadingViewModel GetOrCreateReading(TelemetryReading reading)
    {
        if (_readingsByCode.TryGetValue(reading.Code, out var existing))
            return existing;

        var name = reading.Definition?.Name ?? reading.Code;
        var row = new ElectricalReadingViewModel(
            name, reading.Definition?.Address ?? 0, reading.Definition?.Unit, reading.Definition?.Description);

        var (spec, rank) = Classify(name);
        var group = GetOrCreateGroup(spec?.Key ?? OtherKey, spec?.Energy ?? false, rank);
        group.Readings.Add(row);

        _readingsByCode[reading.Code] = row;
        return row;
    }

    private static (GroupSpec? Spec, int Rank) Classify(string name)
    {
        for (var i = 0; i < Specs.Length; i++)
            if (Specs[i].Match(name))
                return (Specs[i], i);
        return (null, int.MaxValue);
    }

    private ReadingGroupViewModel GetOrCreateGroup(string key, bool energy, int rank)
    {
        if (_groupsByKey.TryGetValue(key, out var existing))
            return existing;

        var group = new ReadingGroupViewModel(key);
        var target = energy ? EnergyGroups : ReadingGroups;

        // Keep groups in their declared display order regardless of arrival order.
        var index = 0;
        while (index < target.Count && RankOf(target[index].GroupKey) <= rank)
            index++;
        target.Insert(index, group);

        _groupsByKey[key] = group;
        return group;
    }

    private static int RankOf(string key)
    {
        if (key == OtherKey) return int.MaxValue;
        for (var i = 0; i < Specs.Length; i++)
            if (Specs[i].Key == key)
                return i;
        return int.MaxValue;
    }

    [RelayCommand]
    private void GoBack()
    {
        Dispose();
        _onGoBack();
    }

    public void Dispose()
    {
        _pollingEngine.TelemetryReceived -= OnTelemetryReceived;
    }
}
