using IISDeploy.Core.Models;
using IISDeploy.Core.Models.Enums;

namespace IISDeploy.Application.DTOs;

public class DependencyScanRequest
{
    public List<string>? SiteNames { get; set; }
    public bool FullServerScan { get; set; }
    public string? PackagePath { get; set; }
}

public class DependencyScanResult
{
    public List<DependencyInfo> FoundDependencies { get; set; } = [];
    public List<DependencyInfo> MissingDependencies { get; set; } = [];
    public List<DependencyInfo> VersionMismatches { get; set; } = [];
    public ValidationResult? OverallValidation { get; set; }
    public TimeSpan ScanDuration { get; set; }
}
