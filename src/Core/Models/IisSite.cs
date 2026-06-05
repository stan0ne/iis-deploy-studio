using IISDeploy.Core.Models.Enums;

namespace IISDeploy.Core.Models;

public class IisSite
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PhysicalPath { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public bool ServerAutoStart { get; set; }
    public List<BindingInfo> Bindings { get; set; } = [];
    public List<IisApplication> Applications { get; set; } = [];
    public List<IisVirtualDirectory> VirtualDirectories { get; set; } = [];
    public string? AppPoolName { get; set; }
    public Dictionary<string, string> EnvironmentVariables { get; set; } = [];
    public SiteLimits? Limits { get; set; }
    public bool PreloadEnabled { get; set; }
}
