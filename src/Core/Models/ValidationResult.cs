using IISDeploy.Core.Models.Enums;

namespace IISDeploy.Core.Models;

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<ValidationIssue> Issues { get; set; } = [];
}

public class ValidationIssue
{
    public ValidationSeverity Severity { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string? Remediation { get; set; }
    public string? AffectedObject { get; set; }
}
