using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Modbus.Core.Domain.Repositories;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Modbus.Desktop.ViewModels;

public partial class DeviceDetailViewModel : ObservableObject
{
    private readonly IRegisterValueRepository _registerValueRepository;
    private readonly DeviceListViewModel _parent;

    public DeviceItemViewModel Device { get; }

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<RegisterValueViewModel> RegisterValues { get; } = new();

    public DeviceDetailViewModel(
        DeviceItemViewModel device,
        IRegisterValueRepository registerValueRepository,
        DeviceListViewModel parent)
    {
        Device = device;
        _registerValueRepository = registerValueRepository;
        _parent = parent;
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
    private void GoBack() => _parent.NavigateBack();
}
