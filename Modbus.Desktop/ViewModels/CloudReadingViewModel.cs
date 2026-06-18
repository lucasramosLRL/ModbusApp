using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Modbus.Core.Domain.Entities;
using Modbus.Core.Domain.Enums;
using Modbus.Core.Polling;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Modbus.Desktop.ViewModels;

/// <summary>
/// Telemetry-driven readings for a cloud (MQTT) device. Unlike <see cref="DeviceDetailViewModel"/>,
/// this is purely push: it subscribes to telemetry and updates only the quantities the meter
/// actually publishes (its configured G1..G20). No polling, no I/O/mass-memory tabs.
/// </summary>
public partial class CloudReadingViewModel : ObservableObject, IDisposable
{
    private readonly IPollingEngine _pollingEngine;
    private readonly Action _onGoBack;
    private readonly Dictionary<ushort, ElectricalReadingViewModel> _readingsByAddress = new();

    public DeviceItemViewModel Device { get; }

    public ObservableCollection<ReadingGroupViewModel> ReadingGroups { get; } = new();
    public ObservableCollection<ReadingGroupViewModel> EnergyGroups { get; } = new();

    [ObservableProperty]
    private string _lastUpdate = "—";

    [ObservableProperty]
    private bool _hasData;

    public CloudReadingViewModel(DeviceItemViewModel device, IPollingEngine pollingEngine, Action onGoBack)
    {
        Device = device;
        _pollingEngine = pollingEngine;
        _onGoBack = onGoBack;

        BuildReadingGroups();
        _pollingEngine.RegisterValuesUpdated += OnRegisterValuesUpdated;
    }

    private void BuildReadingGroups()
    {
        var registers = Device.Device.DeviceModel?.Registers;
        if (registers is null || registers.Count == 0) return;

        var inputRegisters = registers
            .Where(r => r.RegisterType == RegisterType.Input && r.Name != "NS")
            .OrderBy(r => r.Address)
            .ToList();

        var groupDefs = new (string Key, Func<RegisterDefinition, bool> Match)[]
        {
            ("GroupVoltages",      r => r.Name.StartsWith("U")),
            ("GroupCurrents",      r => r.Name.StartsWith("I")),
            ("GroupFrequency",     r => r.Name == "Freq"),
            ("GroupActivePower",   r => r.Name.StartsWith("P")),
            ("GroupReactivePower", r => r.Name.StartsWith("Q")),
            ("GroupApparentPower", r => r.Name.StartsWith("S")),
            ("GroupPowerFactor",   r => r.Name.StartsWith("FP")),
        };

        AddGroups(groupDefs, inputRegisters, ReadingGroups);

        var energyGroupDefs = new (string Key, Func<RegisterDefinition, bool> Match)[]
        {
            ("GroupEnergies", r => r.Name.StartsWith("EA") || r.Name.StartsWith("ER") || r.Name == "ES"),
            ("GroupDemands",  r => r.Name.StartsWith("DA") || r.Name.StartsWith("DR") ||
                                   r.Name.StartsWith("DS") || r.Name.StartsWith("DI") ||
                                   r.Name.StartsWith("MD")),
        };

        AddGroups(energyGroupDefs, inputRegisters, EnergyGroups);
    }

    private void AddGroups(
        (string Key, Func<RegisterDefinition, bool> Match)[] groupDefs,
        List<RegisterDefinition> inputRegisters,
        ObservableCollection<ReadingGroupViewModel> target)
    {
        foreach (var (key, match) in groupDefs)
        {
            var matched = inputRegisters.Where(match).ToList();
            if (matched.Count == 0) continue;

            var group = new ReadingGroupViewModel(key);
            foreach (var reg in matched)
            {
                var reading = new ElectricalReadingViewModel(reg.Name, reg.Address, reg.Unit);
                group.Readings.Add(reading);
                _readingsByAddress[reg.Address] = reading;
            }
            target.Add(group);
        }
    }

    private void OnRegisterValuesUpdated(object? sender, RegisterValuesUpdatedEventArgs e)
    {
        if (e.Device.Id != Device.Id || e.Values.Count == 0) return;

        Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var val in e.Values)
                if (_readingsByAddress.TryGetValue(val.Address, out var reading))
                    reading.Update(val.Value);

            HasData = true;
            LastUpdate = e.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        });
    }

    [RelayCommand]
    private void GoBack()
    {
        Dispose();
        _onGoBack();
    }

    public void Dispose()
    {
        _pollingEngine.RegisterValuesUpdated -= OnRegisterValuesUpdated;
    }
}
