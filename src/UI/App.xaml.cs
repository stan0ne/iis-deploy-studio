using IISDeploy.Application;
using IISDeploy.Infrastructure;
using IISDeploy.Infrastructure.Plugins;
using IISDeploy.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace IISDeploy.UI;

public partial class App : System.Windows.Application
{
    private readonly IHost _host;

    public App()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File("logs/iisdeploy-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30)
            .CreateLogger();

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((context, services) =>
            {
                services.AddInfrastructure();
                services.AddApplication();
                services.AddTransient<MainViewModel>();
                services.AddTransient<MainWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        await _host.StartAsync();

        var pluginHost = _host.Services.GetRequiredService<PluginHost>();
        await pluginHost.LoadPluginsAsync();

        Log.Information("IISDeploy Studio starting...");
        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.DataContext = _host.Services.GetRequiredService<MainViewModel>();
        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(System.Windows.ExitEventArgs e)
    {
        using (_host)
        {
            Log.Information("IISDeploy Studio shutting down.");
            var pluginHost = _host.Services.GetRequiredService<PluginHost>();
            await pluginHost.ShutdownAsync();
            await _host.StopAsync();
            await Log.CloseAndFlushAsync();
        }
        base.OnExit(e);
    }
}
