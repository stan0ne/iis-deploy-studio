using IISDeploy.Core.Models;
using IISDeploy.Core.Models.Enums;

namespace IISDeploy.Application.DTOs;

public class ExportRequest
{
    public List<string> SiteNames { get; set; } = [];
    public string OutputPath { get; set; } = string.Empty;
    public ExportMode Mode { get; set; }
    public bool IncludeCertificates { get; set; }
    public string? CertificatePassword { get; set; }
    public bool IncludeNtfsPermissions { get; set; }
    public bool RunDependencyScan { get; set; } = true;
}

public class ExportResult
{
    public bool Success { get; set; }
    public string? PackagePath { get; set; }
    public MigrationReport? Report { get; set; }
    public List<string> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public TimeSpan Duration { get; set; }
}
