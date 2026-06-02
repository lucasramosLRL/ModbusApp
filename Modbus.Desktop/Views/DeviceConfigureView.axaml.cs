using Avalonia.Controls;
using Avalonia.Interactivity;
using Modbus.Desktop.ViewModels;

namespace Modbus.Desktop.Views;

public partial class DeviceConfigureView : UserControl
{
    public DeviceConfigureView()
    {
        InitializeComponent();
    }

    // Handled in code-behind (not via CommandParameter) so that after the add we can
    // re-focus the available list and move the selection to the next item — letting the
    // user keep pressing "→" to add grandezas in sequence without re-clicking the list.
    private void OnAddGrandezasClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DeviceConfigureViewModel vm) return;
        var list = this.FindControl<ListBox>("AvailableList");
        if (list is null) return;

        int nextIndex = vm.AddGrandezasReturningNextIndex(list.SelectedItems);
        list.SelectedItems?.Clear();
        if (nextIndex >= 0 && nextIndex < list.ItemCount)
        {
            list.SelectedIndex = nextIndex;
            list.Focus();
        }
    }

    private void OnRemoveGrandezasClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DeviceConfigureViewModel vm) return;
        var list = this.FindControl<ListBox>("SelectedList");
        if (list is null) return;

        int nextIndex = vm.RemoveGrandezasReturningNextIndex(list.SelectedItems);
        list.SelectedItems?.Clear();
        if (nextIndex >= 0 && nextIndex < list.ItemCount)
        {
            list.SelectedIndex = nextIndex;
            list.Focus();
        }
    }

    private void OnClearGrandezasClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DeviceConfigureViewModel vm) return;
        vm.ClearGrandezasCommand.Execute(null);
        this.FindControl<ListBox>("AvailableList")?.SelectedItems?.Clear();
        this.FindControl<ListBox>("SelectedList")?.SelectedItems?.Clear();
    }
}
