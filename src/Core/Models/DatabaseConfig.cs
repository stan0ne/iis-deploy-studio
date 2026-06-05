namespace IISDeploy.Core.Models;

public class DatabaseConfig
{
    public string? Name { get; set; }
    public string? ConnectionString { get; set; }
    public string? ProviderName { get; set; }
    public string Source { get; set; } = string.Empty;
}

public class EnvironmentFileConfig
{
    public string Path { get; set; } = string.Empty;
    public Dictionary<string, string> Variables { get; set; } = [];
}
