using System.Reflection;
using IISDeploy.Core.Interfaces;
using IISDeploy.Core.Plugins;

namespace IISDeploy.Infrastructure.Plugins;

public class PluginHost : IPluginContext, IAsyncDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILoggingService _logger;
    private readonly List<LoadedPlugin> _plugins = [];
    private readonly string _pluginsDirectory;

    public PluginHost(IServiceProvider serviceProvider, ILoggingService logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        _pluginsDirectory = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "plugins");
    }

    public T GetService<T>() where T : class =>
        _serviceProvider.GetService(typeof(T)) as T
            ?? throw new InvalidOperationException($"Service {typeof(T).Name} not registered.");

    public void Log(string level, string message)
    {
        switch (level.ToLower())
        {
            case "error": _logger.Error("Plugin: {Message}", message); break;
            case "warning": _logger.Warning("Plugin: {Message}", message); break;
            default: _logger.Information("Plugin: {Message}", message); break;
        }
    }

    public IReadOnlyList<LoadedPlugin> Plugins => _plugins.AsReadOnly();

    public async Task LoadPluginsAsync()
    {
        if (!Directory.Exists(_pluginsDirectory))
        {
            Directory.CreateDirectory(_pluginsDirectory);
            return;
        }

        _logger.Information("Loading plugins from {Dir}", _pluginsDirectory);

        foreach (var dll in Directory.GetFiles(_pluginsDirectory, "*.dll"))
        {
            await TryLoadPluginAsync(dll);
        }

        _logger.Information("Loaded {Count} plugins", _plugins.Count);
    }

    private async Task TryLoadPluginAsync(string assemblyPath)
    {
        try
        {
            var assembly = Assembly.LoadFrom(assemblyPath);
            var pluginTypes = assembly.GetTypes()
                .Where(t => typeof(IIisDeployPlugin).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false });

            foreach (var type in pluginTypes)
            {
                if (Activator.CreateInstance(type) is IIisDeployPlugin plugin)
                {
                    var loaded = new LoadedPlugin
                    {
                        Plugin = plugin,
                        AssemblyPath = assemblyPath
                    };

                    try
                    {
                        loaded.State = PluginLifecycle.Initializing;
                        await plugin.InitializeAsync(this);
                        loaded.State = PluginLifecycle.Active;
                        _plugins.Add(loaded);

                        _logger.Information("Plugin loaded: {Name} v{Version} — {Description}",
                            plugin.Name, plugin.Version, plugin.Description);
                    }
                    catch (Exception ex)
                    {
                        loaded.State = PluginLifecycle.Error;
                        loaded.Error = ex.Message;
                        _logger.Error(ex, "Plugin {Name} failed to initialize", plugin.Name);
                        _plugins.Add(loaded);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warning("Could not load plugin from {Path}: {Message}",
                assemblyPath, ex.Message);
        }
    }

    public async Task ShutdownAsync()
    {
        foreach (var loaded in _plugins)
        {
            if (loaded.State == PluginLifecycle.Active)
            {
                try
                {
                    loaded.State = PluginLifecycle.ShuttingDown;
                    await loaded.Plugin.ShutdownAsync();
                    loaded.State = PluginLifecycle.Stopped;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error shutting down plugin {Name}", loaded.Plugin.Name);
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await ShutdownAsync();
    }
}

public class LoadedPlugin
{
    public IIisDeployPlugin Plugin { get; init; } = null!;
    public string AssemblyPath { get; init; } = string.Empty;
    public PluginLifecycle State { get; set; } = PluginLifecycle.NotLoaded;
    public string? Error { get; set; }
}
