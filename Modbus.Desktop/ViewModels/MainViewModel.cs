using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Modbus.Desktop.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DeviceListViewModel _deviceList;

    [ObservableProperty]
    private object? _currentPage;

    [ObservableProperty]
    private NavItem? _selectedNavItem;

    public ObservableCollection<NavItem> NavItems { get; }

    public MainViewModel(DeviceListViewModel deviceList)
    {
        _deviceList = deviceList;

        NavItems = new ObservableCollection<NavItem>
        {
            new() { Title = "Devices", Icon = "⚙" }
        };

        deviceList.NavigationRequested += (_, page) => CurrentPage = page;

        SelectedNavItem = NavItems[0];
        CurrentPage = _deviceList;
    }

    partial void OnSelectedNavItemChanged(NavItem? value)
    {
        if (value?.Title == "Devices")
            CurrentPage = _deviceList;
    }
}
