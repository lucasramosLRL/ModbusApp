using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Modbus.Mobile.Avalonia.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _title = "ModbusApp Mobile · Avalonia";

    [ObservableProperty]
    private string _status = "Inicializando…";

    public ObservableCollection<string> Models { get; } = new();
}
