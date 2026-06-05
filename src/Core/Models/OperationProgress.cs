using IISDeploy.Core.Models.Enums;

namespace IISDeploy.Core.Models;

public class OperationProgress
{
    public OperationStatus Status { get; set; }
    public int Percentage { get; set; }
    public string CurrentStep { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public TimeSpan? EstimatedRemaining { get; set; }
    public int TotalItems { get; set; }
    public int CompletedItems { get; set; }
    public int FailedItems { get; set; }
}
