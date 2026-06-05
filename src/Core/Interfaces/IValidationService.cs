using IISDeploy.Core.Models;

namespace IISDeploy.Core.Interfaces;

public interface IValidationService
{
    Task<ValidationResult> ValidateEnvironmentAsync();
    Task<ValidationResult> ValidatePackageAsync(string packagePath);
    Task<ValidationResult> ValidateSiteForExportAsync(string siteName);
    ValidationResult CheckAdminPrivileges();
}
