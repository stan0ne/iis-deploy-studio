using IISDeploy.Core.Models;

namespace IISDeploy.Core.Interfaces;

public interface IDependencyScannerService
{
    Task<List<DependencyInfo>> ScanServerDependenciesAsync();
    Task<List<DependencyInfo>> ScanSiteDependenciesAsync(string siteName);
    Task<List<DependencyInfo>> ScanPackageDependenciesAsync(string packagePath);
    Task<bool> CheckDependencyAsync(string name, string? version = null);
}
