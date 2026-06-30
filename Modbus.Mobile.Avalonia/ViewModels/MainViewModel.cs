using CommunityToolkit.Mvvm.ComponentModel;

namespace Modbus.Mobile.Avalonia.ViewModels;

/// <summary>
/// Navigation shell: hosts the current page (device list / add device). The single-view
/// host (Android) binds a ContentControl to <see cref="CurrentPage"/>; pages are resolved
/// by the ViewLocator. Mirrors the desktop MainViewModel.CurrentPage pattern.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly DeviceListViewModel _deviceList;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAddDeviceOpen))]
    private object? _currentPage;

    /// <summary>Boot error surfaced by App startup; null on success.</summary>
    [ObservableProperty]
    private string? _bootError;

    public MainViewModel(DeviceListViewModel deviceList)
    {
        _deviceList = deviceList;
        _deviceList.NavigationRequested += (_, page) => CurrentPage = page;
        CurrentPage = _deviceList;
    }

    public bool IsAddDeviceOpen => CurrentPage is AddDeviceViewModel;

    public void NavigateBack() => CurrentPage = _deviceList;
}
