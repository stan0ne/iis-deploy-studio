namespace IISDeploy.Application.DTOs;

public class SiteTreeNode
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Status { get; set; }
    public string? PhysicalPath { get; set; }
    public string? AppPoolName { get; set; }
    public int BindingCount { get; set; }
    public int ChildCount { get; set; }
    public List<SiteTreeNode> Children { get; set; } = [];
    public bool HasIssues { get; set; }
    public bool HasDependencies { get; set; }
    public bool IsChecked { get; set; }
}

public class AppPoolSummary
{
    public string Name { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PipelineMode { get; set; } = string.Empty;
    public string ManagedRuntimeVersion { get; set; } = string.Empty;
    public string IdentityType { get; set; } = string.Empty;
    public int ApplicationCount { get; set; }
}

public class ServerSummary
{
    public string MachineName { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string IisVersion { get; set; } = string.Empty;
    public int TotalSites { get; set; }
    public int RunningSites { get; set; }
    public int StoppedSites { get; set; }
    public int TotalAppPools { get; set; }
    public int RunningAppPools { get; set; }
    public int PendingWarnings { get; set; }
    public DateTime LastScan { get; set; }
}
