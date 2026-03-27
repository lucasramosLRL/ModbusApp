using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modbus.Core.Domain.Repositories;
using Modbus.Core.Persistence;
using Modbus.Core.Persistence.Repositories;
using Modbus.Core.Polling;
using Modbus.Core.Services;
using Modbus.Core.Services.Scanning;
using Modbus.Desktop.ViewModels;
using Modbus.Desktop.Views;
using System;
using System.IO;

namespace Modbus.Desktop;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        Services = serviceCollection.BuildServiceProvider();

        using var db = Services.GetRequiredService<ModbusDbContext>();
        db.Database.EnsureCreated();

        var seeder = new DeviceModelSeeder(Services.GetRequiredService<IDeviceModelRepository>());
        seeder.SeedAsync().GetAwaiter().GetResult();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainViewModel>()
            };

            desktop.Exit += (_, _) =>
            {
                Services.GetRequiredService<IPollingEngine>().StopAsync().GetAwaiter().GetResult();
                (Services as IDisposable)?.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ModbusApp", "modbus.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        services.AddDbContext<ModbusDbContext>(
            options => options.UseSqlite($"Data Source={dbPath}"),
            ServiceLifetime.Transient);

        services.AddTransient<IDeviceRepository, DeviceRepository>();
        services.AddTransient<IDeviceModelRepository, DeviceModelRepository>();
        services.AddTransient<IRegisterValueRepository, RegisterValueRepository>();

        services.AddTransient<IDeviceScanService, DeviceScanService>();
        services.AddSingleton<IModbusServiceFactory, ModbusServiceFactory>();
        services.AddSingleton<IPollingEngine>(sp =>
            new PollingEngine(
                sp.GetRequiredService<IModbusServiceFactory>(),
                TimeSpan.FromSeconds(5)));

        services.AddSingleton<DeviceListViewModel>();
        services.AddSingleton<MainViewModel>();
    }
}
