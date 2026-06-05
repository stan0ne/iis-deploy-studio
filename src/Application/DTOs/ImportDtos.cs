using IISDeploy.Core.Models;
using IISDeploy.Core.Models.Enums;

namespace IISDeploy.Application.DTOs;

public class ImportRequest
{
    public string PackagePath { get; set; } = string.Empty;
    public Dictionary<string, ConflictResolutionStrategy>? ConflictStrategies { get; set; }
    public Dictionary<string, string>? CredentialOverrides { get; set; }
    public Dictionary<string, string>? ConnectionStringOverrides { get; set; }
    public Dictionary<string, string>? EnvironmentVariableOverrides { get; set; }
    public Dictionary<string, string>? SitePathOverrides { get; set; }
    public Dictionary<string, string>? CustomNames { get; set; }
    public Dictionary<string, int>? CustomPorts { get; set; }
    public bool DryRun { get; set; }
    public bool AutoRemediate { get; set; }
    public bool InstallMissingFeatures { get; set; }
}

public class ImportResult
{
    public bool Success { get; set; }
    public MigrationReport? Report { get; set; }
    public List<string> RemediationsPerformed { get; set; } = [];
    public List<string> Errors { get; set; } = [];
    public TimeSpan Duration { get; set; }
}
