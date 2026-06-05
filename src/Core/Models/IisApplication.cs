namespace IISDeploy.Core.Models;

public class IisApplication
{
    public string Path { get; set; } = "/";
    public string PhysicalPath { get; set; } = string.Empty;
    public string ApplicationPoolName { get; set; } = string.Empty;
    public bool PreloadEnabled { get; set; }
    public bool ServiceAutoStartEnabled { get; set; }
    public Dictionary<string, string> EnvironmentVariables { get; set; } = [];
    public List<IisVirtualDirectory> VirtualDirectories { get; set; } = [];
}
