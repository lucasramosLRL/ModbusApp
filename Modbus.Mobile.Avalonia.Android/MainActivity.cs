using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;
using Microsoft.Extensions.DependencyInjection;
using Modbus.Mobile.Avalonia.Services;
using Modbus.Mobile.Avalonia.ViewModels;

namespace Modbus.Mobile.Avalonia.Android;

[Activity(
    Label = "ModbusApp",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        // Contribute the Android MulticastLock impl into the shared DI container
        // before App.OnFrameworkInitializationCompleted builds the provider.
        App.ConfigurePlatformServices = services =>
            services.AddSingleton<INetworkScanLock, AndroidNetworkScanLock>();

        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }

    // System back: pop the add-device page instead of leaving the app.
    public override void OnBackPressed()
    {
        var shell = App.Services?.GetService<MainViewModel>();
        if (shell is { IsAddDeviceOpen: true })
        {
            shell.NavigateBack();
            return;
        }

        base.OnBackPressed();
    }
}
