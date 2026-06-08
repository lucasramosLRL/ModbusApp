using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Modbus.Core.Domain.Entities;
using Modbus.Core.Domain.Enums;
using Modbus.Core.Domain.Repositories;
using Modbus.Core.Polling;
using Modbus.Core.Services;
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
    private readonly IDeviceConfigService _configService;
    private readonly Func<Task>? _pausePolling;   // RTU: SuspendRtu | TCP: AcquireDeviceLock
    private readonly Action? _resumePolling;       // RTU: ResumeRtu  | TCP: ReleaseDeviceLock
    private readonly Action _onGoBack;
    private readonly Dictionary<ushort, ElectricalReadingViewModel> _readingsByAddress = new();
    private readonly Dictionary<ushort, Action<double>> _ioUpdatesByAddress = new();

    public DeviceItemViewModel Device { get; }

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _selectedTabIndex;

    public ObservableCollection<ReadingGroupViewModel> ReadingGroups { get; } = new();
    public ObservableCollection<ReadingGroupViewModel> EnergyGroups { get; } = new();
    public ObservableCollection<RegisterValueViewModel> RegisterValues { get; } = new();

    public IReadOnlyList<DigitalInputViewModel>  DigitalInputs  { get; private set; } = [];
    public IReadOnlyList<DigitalOutputViewModel> DigitalOutputs { get; private set; } = [];
    public HourmeterViewModel? Hourmeter { get; private set; }
    public StatusViewModel? Status { get; private set; }

    public DeviceDetailViewModel(
        DeviceItemViewModel device,
        IRegisterValueRepository registerValueRepository,
        IPollingEngine pollingEngine,
        IDeviceConfigService configService,
        Func<Task>? pausePolling,
        Action? resumePolling,
        Action onGoBack,
        ushort inOutCfg = 0x001F)
    {
        Device = device;
        _registerValueRepository = registerValueRepository;
        _pollingEngine = pollingEngine;
        _configService = configService;
        _pausePolling = pausePolling;
        _resumePolling = resumePolling;
        _onGoBack = onGoBack;

        BuildReadingGroups();
        BuildHourmeterChannel();
        BuildIoChannels(inOutCfg);
        BuildStatusChannel();

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

        var energyGroupDefs = new (string Key, Func<RegisterDefinition, bool> Match)[]
        {
            ("GroupEnergies", r => r.Name.StartsWith("EA") || r.Name.StartsWith("ER") || r.Name == "ES"),
            ("GroupDemands",  r => r.Name.StartsWith("DA") || r.Name.StartsWith("DR") ||
                                   r.Name.StartsWith("DS") || r.Name.StartsWith("DI") ||
                                   r.Name.StartsWith("MD")),
        };

        foreach (var (key, match) in energyGroupDefs)
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
            EnergyGroups.Add(group);
        }
    }

    private void BuildHourmeterChannel()
    {
        // Coil 62 (Modicon 1-based) → 0-based address 61
        var hm = new HourmeterViewModel(() => WriteCoilSafeAsync(61, true));
        Hourmeter = hm;

        _ioUpdatesByAddress[150] = v => hm.UpdateStatus(v);
        _ioUpdatesByAddress[160] = v => hm.UpdateHour(v);
    }

    // InOutCfg bitmask constants (wire address in parentheses for coil writes):
    // bit0=EDP1(coil 20), bit1=EDP2(coil 21), bit2=EDP3(coil 22), bit3=SD1(coil 30), bit4=SD2(coil 31)
    private void BuildIoChannels(ushort inOutCfg)
    {
        var inputs  = new List<DigitalInputViewModel>();
        var outputs = new List<DigitalOutputViewModel>();

        if ((inOutCfg & 0x01) != 0)
        {
            var edp1 = new DigitalInputViewModel("EDP-1", () => WriteCoilSafeAsync(20, true));
            inputs.Add(edp1);
            _ioUpdatesByAddress[94]  = v => edp1.UpdateCounter(v);
            _ioUpdatesByAddress[110] = v => edp1.UpdateStatus(v);
            _ioUpdatesByAddress[130] = v => edp1.UpdatePulse(v);
        }

        if ((inOutCfg & 0x02) != 0)
        {
            var edp2 = new DigitalInputViewModel("EDP-2", () => WriteCoilSafeAsync(21, true));
            inputs.Add(edp2);
            _ioUpdatesByAddress[96]  = v => edp2.UpdateCounter(v);
            _ioUpdatesByAddress[111] = v => edp2.UpdateStatus(v);
            _ioUpdatesByAddress[131] = v => edp2.UpdatePulse(v);
        }

        if ((inOutCfg & 0x04) != 0)
        {
            var edp3 = new DigitalInputViewModel("EDP-3", () => WriteCoilSafeAsync(22, true));
            inputs.Add(edp3);
            _ioUpdatesByAddress[98]  = v => edp3.UpdateCounter(v);
            _ioUpdatesByAddress[112] = v => edp3.UpdateStatus(v);
            _ioUpdatesByAddress[132] = v => edp3.UpdatePulse(v);
        }

        if ((inOutCfg & 0x08) != 0)
        {
            var sd1 = new DigitalOutputViewModel("SD-1", v => WriteCoilSafeAsync(30, v));
            outputs.Add(sd1);
            _ioUpdatesByAddress[113] = v => sd1.UpdateStatus(v);
        }

        if ((inOutCfg & 0x10) != 0)
        {
            var sd2 = new DigitalOutputViewModel("SD-2", v => WriteCoilSafeAsync(31, v));
            outputs.Add(sd2);
            _ioUpdatesByAddress[114] = v => sd2.UpdateStatus(v);
        }

        DigitalInputs  = inputs;
        DigitalOutputs = outputs;
    }

    private void BuildStatusChannel()
    {
        var modelName = Device.Device.DeviceModel?.Name ?? string.Empty;
        var status = new StatusViewModel(modelName);
        Status = status;

        _ioUpdatesByAddress[3900] = v => status.UpdateMeterStatus(v);
        _ioUpdatesByAddress[3902] = v => status.UpdateModuleStatus(v);
    }

    private async Task WriteCoilSafeAsync(ushort coilAddress, bool value)
    {
        // RTU: SuspendRtuPollingAsync releases the serial port.
        // TCP: AcquireDeviceLockAsync waits for the current poll to finish and disconnects
        //      the transport — KS-3000 and similar devices only accept one TCP connection.
        if (_pausePolling is not null) await _pausePolling();
        try
        {
            await _configService.WriteCoilAsync(Device.Device, coilAddress, value);
        }
        finally
        {
            _resumePolling?.Invoke();
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

                if (_ioUpdatesByAddress.TryGetValue(val.Address, out var ioUpdate))
                    ioUpdate(val.Value);
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
        _onGoBack();
    }

    public void Dispose()
    {
        _pollingEngine.RegisterValuesUpdated -= OnRegisterValuesUpdated;
    }
}
