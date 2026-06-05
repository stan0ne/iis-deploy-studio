using IISDeploy.Core.Models.Enums;

namespace IISDeploy.Core.Models;

public class MigrationReport
{
    public string ReportId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public string ReportType { get; set; } = string.Empty;
    public OperationStatus Status { get; set; }
    public PackageManifest? SourceManifest { get; set; }
    public MachineInfo TargetMachine { get; set; } = new();
    public List<ReportEntry> Entries { get; set; } = [];
    public List<string> Summary { get; set; } = [];
}

public class ReportEntry
{
    public string Category { get; set; } = string.Empty;
    public string Item { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Message { get; set; }
    public TimeSpan Duration { get; set; }
}
