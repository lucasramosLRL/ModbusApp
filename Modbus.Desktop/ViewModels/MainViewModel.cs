using CommunityToolkit.Mvvm.ComponentModel;
using Modbus.Desktop.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Modbus.Desktop.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DeviceListViewModel _deviceList;
    private readonly SettingsViewModel _settings;

    [ObservableProperty]
    private object? _currentPage;

    [ObservableProperty]
    private NavItem? _selectedNavItem;

    public ObservableCollection<NavItem> NavItems { get; }

    public MainViewModel(DeviceListViewModel deviceList, SettingsViewModel settings)
    {
        _deviceList = deviceList;
        _settings   = settings;

        NavItems = new ObservableCollection<NavItem>
        {
            new() { Key = "NavDevices", Icon = "📡" },
            new() { Key = "NavSettings", Icon = "⚙" }
        };

        UpdateNavTitles();
        LocalizationService.Instance.PropertyChanged += OnLocalizationChanged;

        deviceList.NavigationRequested += (_, page) =>
        {
            CurrentPage = page;
            if (page == _settings)
                SelectedNavItem = NavItems.FirstOrDefault(n => n.Key == "NavSettings");
            else
                SelectedNavItem = NavItems.FirstOrDefault(n => n.Key == "NavDevices");
        };

        SelectedNavItem = NavItems[0];
        CurrentPage = _deviceList;
    }

    partial void OnSelectedNavItemChanged(NavItem? value)
    {
        if (value?.Key == "NavDevices")
            CurrentPage = _deviceList;
        else if (value?.Key == "NavSettings")
            CurrentPage = _settings;
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "Item[]")
            UpdateNavTitles();
    }

    private void UpdateNavTitles()
    {
        foreach (var item in NavItems)
            item.Title = LocalizationService.Instance[item.Key];
    }
}
