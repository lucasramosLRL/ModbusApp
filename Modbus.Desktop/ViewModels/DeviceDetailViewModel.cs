using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Modbus.Core.Domain.Entities;
using Modbus.Core.Domain.Enums;
using Modbus.Core.Domain.Repositories;
using Modbus.Core.Polling;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Modbus.Desktop.ViewModels;

public partial class DeviceDetailViewModel : ObservableObject, IDisposable
{
    private readonly IRegisterValueRepository _registerValueRepository;
    private readonly IPollingEngine _pollingEngine;
    private readonly DeviceListViewModel _parent;
    private readonly Dictionary<ushort, ElectricalReadingViewModel> _readingsByAddress = new();

    public DeviceItemViewModel Device { get; }

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _selectedTabIndex;

    public ObservableCollection<ReadingGroupViewModel> ReadingGroups { get; } = new();
    public ObservableCollection<RegisterValueViewModel> RegisterValues { get; } = new();

    public DeviceDetailViewModel(
        DeviceItemViewModel device,
        IRegisterValueRepository registerValueRepository,
        IPollingEngine pollingEngine,
        DeviceListViewModel parent)
    {
        Device = device;
        _registerValueRepository = registerValueRepository;
        _pollingEngine = pollingEngine;
        _parent = parent;

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

        // Group registers by name prefix into logical categories
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
            ReadingGroups.Add(group);
        }
    }

    private void OnRegisterValuesUpdated(object? sender, RegisterValuesUpdatedEventArgs e)
    {
        if (e.Device.Id != Device.Id) return;

        Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var val in e.Values)
            {
                if (_readingsByAddress.TryGetValue(val.Address, out var reading))
                    reading.Update(val.Value);
            }
        });
    }

    public async Task LoadValuesAsync()
    {
        IsLoading = true;
        RegisterValues.Clear();

        try
        {
            var values = await _registerValueRepository.GetByDeviceIdAsync(Device.Id);
            var definitions = Device.Device.DeviceModel?.Registers ?? [];
            var defMap = definitions.ToDictionary(d => d.Address);

            foreach (var val in values.OrderBy(v => v.Address))
            {
                defMap.TryGetValue(val.Address, out var def);
                RegisterValues.Add(new RegisterValueViewModel(val, def));

                // Also populate live readings from stored values
                if (_readingsByAddress.TryGetValue(val.Address, out var reading))
                    reading.Update(val.Value);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadValuesAsync();

    [RelayCommand]
    private void GoBack()
    {
        Dispose();
        _parent.NavigateBack();
    }

    public void Dispose()
    {
        _pollingEngine.RegisterValuesUpdated -= OnRegisterValuesUpdated;
    }
}
