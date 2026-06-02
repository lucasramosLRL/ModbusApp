using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Modbus.Desktop.ViewModels;
using System.Linq;

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

        var available = this.FindControl<ListBox>("AvailableList");
        available?.SelectedItems?.Clear();

        this.FindControl<ListBox>("SelectedList")?.SelectedItems?.Clear();

        // Defer the scroll reset to after the ListBox layout settles — calling it
        // inline gets overridden by Avalonia bringing the just-inserted items into view.
        if (available is not null)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var sv = available.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
                if (sv is not null)
                    sv.Offset = new Vector(sv.Offset.X, 0);
            }, DispatcherPriority.Background);
        }
    }
}
