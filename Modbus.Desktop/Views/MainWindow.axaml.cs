using Avalonia.Controls;
using Avalonia.Media;
using Modbus.Desktop.Infrastructure;

namespace Modbus.Desktop.Views;

public partial class MainWindow : Window
{
    // Teal accent (#068CB1) caption with white text — same in both themes.
    private static readonly Color CaptionColor = Color.FromRgb(0x06, 0x8C, 0xB1);
    private static readonly Color CaptionText  = Colors.White;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnOpened(System.EventArgs e)
    {
        base.OnOpened(e);
        TitleBarColorizer.Apply(this, CaptionColor, CaptionText);
    }
}
