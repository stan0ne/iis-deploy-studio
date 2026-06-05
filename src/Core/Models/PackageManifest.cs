namespace IISDeploy.Core.Models;

public class PackageManifest
{
    public string PackageVersion { get; set; } = "1.0.0";
    public DateTime ExportTimestamp { get; set; }
    public string ExportedBy { get; set; } = string.Empty;
    public MachineInfo SourceMachine { get; set; } = new();
    public IisServerInfo SourceIisInfo { get; set; } = new();
    public List<string> ExportedSiteNames { get; set; } = [];
    public List<string> ExportedAppPoolNames { get; set; } = [];
    public List<DependencyInfo> Dependencies { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public List<string> Conflicts { get; set; } = [];
    public string? Checksum { get; set; }
}

public class MachineInfo
{
    public string MachineName { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string OsArchitecture { get; set; } = string.Empty;
    public string FrameworkVersion { get; set; } = string.Empty;
    public long TotalMemoryMb { get; set; }
    public int ProcessorCount { get; set; }
}

public class IisServerInfo
{
    public string IisVersion { get; set; } = string.Empty;
    public int SiteCount { get; set; }
    public int AppPoolCount { get; set; }
    public List<string> InstalledFeatures { get; set; } = [];
}

public record DependencyInfo
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Version { get; set; }
    public bool Required { get; set; } = true;
    public string? InstallPath { get; set; }
    public string? DownloadUrl { get; set; }
}
