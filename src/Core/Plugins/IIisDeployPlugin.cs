namespace IISDeploy.Core.Plugins;

public interface IIisDeployPlugin
{
    string Name { get; }
    string Version { get; }
    string Description { get; }
    Task InitializeAsync(IPluginContext context);
    Task ShutdownAsync();
}

public interface IPluginContext
{
    T GetService<T>() where T : class;
    void Log(string level, string message);
}

public enum PluginLifecycle
{
    NotLoaded,
    Initializing,
    Active,
    Error,
    ShuttingDown,
    Stopped
}
