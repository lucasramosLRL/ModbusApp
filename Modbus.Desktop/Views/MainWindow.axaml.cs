using Avalonia.Controls;
using Avalonia.Media;
using Modbus.Desktop.Infrastructure;
using Modbus.Desktop.Services;

namespace Modbus.Desktop.Views;

public partial class MainWindow : Window
{
    // Light theme: teal accent (#068CB1) caption with white text.
    private static readonly Color LightCaptionColor = Color.FromRgb(0x06, 0x8C, 0xB1);
    private static readonly Color LightCaptionText  = Colors.White;

    // Dark theme: dark caption matching the sidebar (#1A1A2E) with white text.
    private static readonly Color DarkCaptionColor = Color.FromRgb(0x1A, 0x1A, 0x2E);
    private static readonly Color DarkCaptionText  = Colors.White;

    public MainWindow()
    {
        InitializeComponent();
        ThemeService.Instance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ThemeService.CurrentTheme))
                ApplyCaption();
        };
    }

    protected override void OnOpened(System.EventArgs e)
    {
        base.OnOpened(e);
        ApplyCaption();
    }

    private void ApplyCaption()
    {
        if (ThemeService.Instance.CurrentTheme == AppTheme.Light)
            TitleBarColorizer.Apply(this, LightCaptionColor, LightCaptionText);
        else
            TitleBarColorizer.Apply(this, DarkCaptionColor, DarkCaptionText);
    }
}
