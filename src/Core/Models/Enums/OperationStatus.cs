namespace IISDeploy.Core.Models.Enums;

public enum OperationStatus
{
    Pending,
    InProgress,
    Completed,
    CompletedWithWarnings,
    Failed,
    Cancelled,
    RollingBack,
    RolledBack,
    Skipped
}
