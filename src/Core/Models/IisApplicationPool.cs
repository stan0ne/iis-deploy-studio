using IISDeploy.Core.Models.Enums;

namespace IISDeploy.Core.Models;

public class IisApplicationPool
{
    public string Name { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public PipelineMode PipelineMode { get; set; }
    public string ManagedRuntimeVersion { get; set; } = "v4.0";
    public bool Enable32BitAppOnWin64 { get; set; }
    public AppPoolIdentityType IdentityType { get; set; }
    public string? CustomUserName { get; set; }
    public int IdleTimeoutMinutes { get; set; }
    public int RegularTimeInterval { get; set; }
    public bool AutoStart { get; set; }
    public int MaxProcesses { get; set; }
    public long PrivateMemoryLimit { get; set; }
    public long VirtualMemoryLimit { get; set; }
    public string ProcessModelLoadUserProfile { get; set; } = string.Empty;
    public Dictionary<string, string> EnvironmentVariables { get; set; } = [];
}
