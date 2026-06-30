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
using Modbus.Mobile.Avalonia.ViewModels;
using Modbus.Mobile.Avalonia.Views;
using System;
using System.IO;

namespace Modbus.Mobile.Avalonia;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        Services = serviceCollection.BuildServiceProvider();

        // Phase A1: boot the Core stack and surface the result on screen so we can
        // confirm DB migration + model seeding work on the Android device itself.
        var vm = new MainViewModel();
        try
        {
            using (var db = Services.GetRequiredService<ModbusDbContext>())
            {
                DatabaseInitializer.Initialize(db);
            }

            var seeder = new DeviceModelSeeder(Services.GetRequiredService<IDeviceModelRepository>());
            seeder.SeedAsync().GetAwaiter().GetResult();

            var models = Services.GetRequiredService<IDeviceModelRepository>()
                .GetAllAsync().GetAwaiter().GetResult();

            foreach (var m in models)
                vm.Models.Add($"{m.Name}  (0x{m.DeviceCode:X2})");

            vm.Status = $"DB inicializado · {models.Count} modelo(s) seedado(s)";
        }
        catch (Exception ex)
        {
            vm.Status = "ERRO no boot: " + ex.Message;
        }

        if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = new MainView { DataContext = vm };
        }

        base.OnFrameworkInitializationCompleted();
    }

    // MVP subset of the desktop DI (Modbus.Desktop/App.axaml.cs): no RTU scan,
    // no configure/mass-memory services. TCP + Cloud (MQTT) only.
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
    }
}
