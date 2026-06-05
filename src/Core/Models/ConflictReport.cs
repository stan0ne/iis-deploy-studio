using IISDeploy.Core.Models.Enums;

namespace IISDeploy.Core.Models;

public class ConflictReport
{
    public List<ConflictEntry> SiteConflicts { get; set; } = [];
    public List<ConflictEntry> PoolConflicts { get; set; } = [];
    public List<ConflictEntry> BindingConflicts { get; set; } = [];
    public bool HasConflicts => SiteConflicts.Count > 0 ||
                                PoolConflicts.Count > 0 ||
                                BindingConflicts.Count > 0;
}

public class ConflictEntry
{
    public string ObjectType { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string ConflictType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public ConflictResolutionStrategy SuggestedResolution { get; set; }
    public ConflictResolutionStrategy ChosenResolution { get; set; }
    public bool Resolved { get; set; }
}
