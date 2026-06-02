using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Modbus.Desktop.Services;
using Modbus.Desktop.ViewModels;
using System.Linq;
using System.Threading.Tasks;

namespace Modbus.Desktop.Views;

public partial class DeviceConfigureView : UserControl
{
    public DeviceConfigureView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    // Wire the VM's mass-memory-reset confirmation to a real dialog. The VM stays UI-agnostic
    // and just awaits a Func<Task<bool>>; the View owns the actual window.
    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is DeviceConfigureViewModel vm)
            vm.ConfirmMassMemoryReset = ShowMassMemoryResetConfirmAsync;
    }

    private async Task<bool> ShowMassMemoryResetConfirmAsync()
    {
        var result = false;
        var loc = LocalizationService.Instance;

        var dialog = new Window
        {
            Title = loc["CfgMemResetTitle"],
            Width = 420,
            Height = 180,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false
        };

        var confirmBtn = new Button
        {
            Content = loc["CfgMemResetConfirm"],
            Padding = new Thickness(16, 8),
            Background = new SolidColorBrush(Color.Parse("#C0392B")),
            Foreground = Brushes.White
        };
        confirmBtn.Click += (_, _) => { result = true; dialog.Close(); };

        var cancelBtn = new Button
        {
            Content = loc["Cancel"],
            Padding = new Thickness(16, 8)
        };
        cancelBtn.Click += (_, _) => dialog.Close();

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 20,
            Children =
            {
                new TextBlock
                {
                    Text = loc["CfgMemResetMsg"],
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 14
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { cancelBtn, confirmBtn }
                }
            }
        };

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is not null)
            await dialog.ShowDialog(owner);
        else
            dialog.Show();

        return result;
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
