using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modbus.Core.Cloud;
using Modbus.Core.Domain.Repositories;
using Modbus.Core.Persistence;
using Modbus.Core.Persistence.Repositories;
using Modbus.Core.Polling;
using Modbus.Core.Services;
using Modbus.Core.Services.Scanning;
using Modbus.Mobile.Avalonia.Services;
using Modbus.Mobile.Avalonia.ViewModels;
using Modbus.Mobile.Avalonia.Views;
using System;
using System.IO;

namespace Modbus.Mobile.Avalonia;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// Lets a platform head (e.g. Android) register platform-specific services
    /// (such as <see cref="INetworkScanLock"/>) into the shared container. The last
    /// registration wins for <c>GetRequiredService</c>.
    /// </summary>
    public static Action<IServiceCollection>? ConfigurePlatformServices { get; set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        Services = serviceCollection.BuildServiceProvider();

        string? bootError = null;
        try
        {
            using (var db = Services.GetRequiredService<ModbusDbContext>())
            {
                DatabaseInitializer.Initialize(db);
            }

            var seeder = new DeviceModelSeeder(Services.GetRequiredService<IDeviceModelRepository>());
            seeder.SeedAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            bootError = "Erro no boot: " + ex.Message;
        }

        var shell = Services.GetRequiredService<MainViewModel>();
        shell.BootError = bootError;

        if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
            singleView.MainView = new MainView { DataContext = shell };

        base.OnFrameworkInitializationCompleted();
    }

    // MVP subset of the desktop DI (Modbus.Desktop/App.axaml.cs): TCP + Cloud (MQTT).
    // RTU scan dropped; TCP broadcast scan kept. No configure/mass-memory services.
    private static void ConfigureServices(IServiceCollection services)
    {
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ModbusApp", "modbusapp.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        services.AddDbContext<ModbusDbContext>(
            options => options.UseSqlite(
                $"Data Source={dbPath}",
                b => b.MigrationsAssembly("Modbus.Core")),
            ServiceLifetime.Transient);

        services.AddTransient<IDeviceRepository, DeviceRepository>();
        services.AddTransient<IDeviceModelRepository, DeviceModelRepository>();
        services.AddTransient<IRegisterValueRepository, RegisterValueRepository>();

        services.AddTransient<IDeviceScanService, DeviceScanService>();

        // No-op by default; the Android head replaces this with a MulticastLock-backed impl.
        services.AddSingleton<INetworkScanLock, NoopNetworkScanLock>();

        // Cloud (MQTT broker) layer — shared across all cloud devices.
        services.AddSingleton<IMqttBrokerClient, MqttBrokerClient>();
        services.AddSingleton<ITelemetryPayloadMapper, JsonTelemetryPayloadMapper>();
        services.AddSingleton<ICloudCommandService>(sp =>
            new CloudCommandService(sp.GetRequiredService<IMqttBrokerClient>()));

        services.AddSingleton<IModbusServiceFactory, ModbusServiceFactory>();
        services.AddSingleton<IPollingEngine>(sp =>
            new PollingEngine(
                sp.GetRequiredService<IModbusServiceFactory>(),
                TimeSpan.FromSeconds(5),
                sp.GetRequiredService<IMqttBrokerClient>(),
                sp.GetRequiredService<ITelemetryPayloadMapper>()));

        services.AddSingleton<DeviceListViewModel>();
        services.AddSingleton<MainViewModel>();

        // Platform-specific overrides (registered last so they win).
        ConfigurePlatformServices?.Invoke(services);
    }
}
