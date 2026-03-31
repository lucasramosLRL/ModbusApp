using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Modbus.Desktop.ViewModels;

namespace Modbus.Desktop.Views;

public partial class DeviceListView : UserControl
{
    public DeviceListView()
    {
        InitializeComponent();
    }

    private async void OnDeleteClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not DeviceItemViewModel device)
            return;

        var confirmed = await ShowDeleteConfirmAsync(device.Name);
        if (!confirmed)
            return;

        if (DataContext is DeviceListViewModel vm)
            await vm.DeleteDeviceCommand.ExecuteAsync(device);
    }

    private async System.Threading.Tasks.Task<bool> ShowDeleteConfirmAsync(string deviceName)
    {
        var result = false;

        var dialog = new Window
        {
            Title = "Confirm Delete",
            Width = 380,
            Height = 160,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false
        };

        var confirmBtn = new Button
        {
            Content = "Delete",
            Padding = new Thickness(16, 8),
            Background = new SolidColorBrush(Color.Parse("#C0392B")),
            Foreground = Brushes.White
        };
        confirmBtn.Click += (_, _) => { result = true; dialog.Close(); };

        var cancelBtn = new Button
        {
            Content = "Cancel",
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
                    Text = $"Delete \"{deviceName}\"? This action cannot be undone.",
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
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
}
