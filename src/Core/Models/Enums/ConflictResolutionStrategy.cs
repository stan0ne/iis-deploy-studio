namespace IISDeploy.Core.Models.Enums;

public enum ConflictResolutionStrategy
{
    Overwrite,
    Clone,
    Rename,
    ChangeBinding,
    ChangePort,
    Skip,
    PromptUser
}
