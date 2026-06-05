using System.Security.Principal;
using IISDeploy.Core.Interfaces;
using IISDeploy.Core.Models;
using IISDeploy.Core.Models.Enums;

namespace IISDeploy.Infrastructure.Iis;

public class ValidationService : IValidationService
{
    public Task<ValidationResult> ValidateEnvironmentAsync()
    {
        var result = new ValidationResult();
        var issues = new List<ValidationIssue>();

        var adminCheck = CheckAdminPrivileges();
        issues.AddRange(adminCheck.Issues);

        try
        {
            using var manager = new Microsoft.Web.Administration.ServerManager();
            issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Information,
                Code = "IIS-001",
                Message = "IIS is installed and accessible.",
                Detail = $"IIS Version: {manager.GetType().Assembly.GetName().Version}"
            });
        }
        catch (Exception ex)
        {
            issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Critical,
                Code = "IIS-ERR-001",
                Message = "Cannot access IIS. Ensure IIS is installed and you have administrative privileges.",
                Detail = ex.Message,
                Remediation = "Install IIS via Server Manager and ensure the IIS Management Console feature is enabled."
            });

            result.Issues = issues;
            result.IsValid = false;
            return Task.FromResult(result);
        }

        var isValid = !issues.Any(i => i.Severity == ValidationSeverity.Error || i.Severity == ValidationSeverity.Critical);
        result.Issues = issues;
        result.IsValid = isValid;
        return Task.FromResult(result);
    }

    public Task<ValidationResult> ValidatePackageAsync(string packagePath)
    {
        var result = new ValidationResult();
        var issues = new List<ValidationIssue>();

        if (!File.Exists(packagePath))
        {
            issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Critical,
                Code = "PKG-ERR-001",
                Message = "Package file not found.",
                Detail = $"Path: {packagePath}"
            });
            result.Issues = issues;
            result.IsValid = false;
            return Task.FromResult(result);
        }

        var extension = Path.GetExtension(packagePath);
        if (!string.Equals(extension, ".iispackage", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Warning,
                Code = "PKG-WARN-001",
                Message = "Package file does not have .iispackage extension.",
                Detail = $"Extension: {extension}"
            });
        }

        try
        {
            using var stream = File.OpenRead(packagePath);
            var buffer = new byte[4];
            stream.ReadExactly(buffer, 0, 4);
            if (buffer[0] != 0x50 || buffer[1] != 0x4B)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Critical,
                    Code = "PKG-ERR-002",
                    Message = "Package is not a valid ZIP file.",
                    Detail = "Invalid ZIP header."
                });
            }
        }
        catch (Exception ex)
        {
            issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Critical,
                Code = "PKG-ERR-003",
                Message = "Failed to read package file.",
                Detail = ex.Message
            });
        }

        result.Issues = issues;
        result.IsValid = !issues.Any(i => i.Severity == ValidationSeverity.Error || i.Severity == ValidationSeverity.Critical);
        return Task.FromResult(result);
    }

    public Task<ValidationResult> ValidateSiteForExportAsync(string siteName)
    {
        var result = new ValidationResult();
        var issues = new List<ValidationIssue>();

        try
        {
            using var manager = new Microsoft.Web.Administration.ServerManager();
            var site = manager.Sites.FirstOrDefault(s =>
                string.Equals(s.Name, siteName, StringComparison.OrdinalIgnoreCase));

            if (site is null)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Critical,
                    Code = "SITE-ERR-001",
                    Message = $"Site '{siteName}' not found.",
                    Remediation = "Verify the site name and ensure it exists in IIS."
                });
            }
            else
            {
                var rootApp = site.Applications.FirstOrDefault(a => a.Path == "/");
                var rootVdir = rootApp?.VirtualDirectories.FirstOrDefault(v => v.Path == "/");

                if (string.IsNullOrEmpty(rootVdir?.PhysicalPath) || !Directory.Exists(rootVdir.PhysicalPath))
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Warning,
                        Code = "SITE-WARN-001",
                        Message = "Site physical path does not exist or is inaccessible.",
                        Detail = $"Path: {rootVdir?.PhysicalPath ?? "(null)"}"
                    });
                }

                if (site.State == Microsoft.Web.Administration.ObjectState.Started)
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Information,
                        Code = "SITE-INFO-001",
                        Message = "Site is currently running. Export will proceed but the site may experience brief downtime."
                    });
                }

                var missingAppPools = site.Applications
                    .Select(a => a.ApplicationPoolName)
                    .Distinct()
                    .Where(poolName => !manager.ApplicationPools.Any(p =>
                        string.Equals(p.Name, poolName, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                foreach (var poolName in missingAppPools)
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Warning,
                        Code = "POOL-WARN-001",
                        Message = $"Application pool '{poolName}' referenced by site but not found.",
                        AffectedObject = poolName
                    });
                }
            }
        }
        catch (Exception ex)
        {
            issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Critical,
                Code = "SYS-ERR-001",
                Message = "Failed to access IIS for site validation.",
                Detail = ex.Message
            });
        }

        result.Issues = issues;
        result.IsValid = !issues.Any(i => i.Severity == ValidationSeverity.Error || i.Severity == ValidationSeverity.Critical);
        return Task.FromResult(result);
    }

    public ValidationResult CheckAdminPrivileges()
    {
        var result = new ValidationResult();
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);

        if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Critical,
                Code = "SEC-ERR-001",
                Message = "Administrator privileges required.",
                Remediation = "Run the application as Administrator."
            });
            result.IsValid = false;
        }
        else
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Information,
                Code = "SEC-INFO-001",
                Message = "Running with administrator privileges."
            });
            result.IsValid = true;
        }

        return result;
    }
}
