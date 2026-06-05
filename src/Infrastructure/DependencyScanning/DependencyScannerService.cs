using IISDeploy.Core.Interfaces;
using IISDeploy.Core.Models;

namespace IISDeploy.Infrastructure.DependencyScanning;

public class DependencyScannerService : IDependencyScannerService
{
    private readonly RuntimeDetectionService _runtimeScanner;
    private readonly IisFeatureScanner _featureScanner;
    private readonly ThirdPartyScanner _thirdPartyScanner;
    private readonly IIisDiscoveryService _discovery;
    private readonly ILoggingService _logger;

    public DependencyScannerService(
        RuntimeDetectionService runtimeScanner,
        IisFeatureScanner featureScanner,
        ThirdPartyScanner thirdPartyScanner,
        IIisDiscoveryService discovery,
        ILoggingService logger)
    {
        _runtimeScanner = runtimeScanner;
        _featureScanner = featureScanner;
        _thirdPartyScanner = thirdPartyScanner;
        _discovery = discovery;
        _logger = logger;
    }

    public async Task<List<DependencyInfo>> ScanServerDependenciesAsync()
    {
        _logger.Information("Starting full server dependency scan");

        var allDeps = new List<DependencyInfo>();

        try
        {
            var runtimeDeps = await _runtimeScanner.ScanRuntimesAsync();
            allDeps.AddRange(runtimeDeps);
            _logger.Information("Runtime scan found {Count} items", runtimeDeps.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Runtime scan failed");
            allDeps.Add(new DependencyInfo
            {
                Name = "Runtime Scanner",
                Type = "ScannerError",
                Version = ex.Message,
                Required = false
            });
        }

        try
        {
            var featureDeps = await _featureScanner.ScanIisFeaturesAsync();
            allDeps.AddRange(featureDeps);
            _logger.Information("IIS feature scan found {Count} items", featureDeps.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "IIS feature scan failed");
            allDeps.Add(new DependencyInfo
            {
                Name = "IIS Feature Scanner",
                Type = "ScannerError",
                Version = ex.Message,
                Required = false
            });
        }

        try
        {
            var thirdPartyDeps = await _thirdPartyScanner.ScanAsync();
            allDeps.AddRange(thirdPartyDeps);
            _logger.Information("Third-party scan found {Count} items", thirdPartyDeps.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Third-party scan failed");
            allDeps.Add(new DependencyInfo
            {
                Name = "Third-Party Scanner",
                Type = "ScannerError",
                Version = ex.Message,
                Required = false
            });
        }

        // Add IIS info
        try
        {
            var serverInfo = await _discovery.GetServerInfoAsync();
            allDeps.Add(new DependencyInfo
            {
                Name = "IIS Server",
                Type = "IIS",
                Version = serverInfo.IisVersion,
                Required = true
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get IIS server info");
            allDeps.Add(new DependencyInfo
            {
                Name = "IIS Server",
                Type = "IIS",
                Version = $"Detection failed: {ex.Message}",
                Required = true
            });
        }

        _logger.Information("Dependency scan complete: {Total} total", allDeps.Count);

        return allDeps;
    }

    public async Task<List<DependencyInfo>> ScanSiteDependenciesAsync(string siteName)
    {
        _logger.Information("Scanning dependencies for site: {Site}", siteName);

        var site = await _discovery.GetSiteAsync(siteName);
        var deps = new List<DependencyInfo>();

        if (site is null)
        {
            deps.Add(new DependencyInfo
            {
                Name = $"Site: {siteName}",
                Type = "Site",
                Version = "Not Found",
                Required = true
            });
            return deps;
        }

        deps.Add(new DependencyInfo
        {
            Name = $"Site: {site.Name}",
            Type = "Site",
            Version = site.State,
            Required = true,
            InstallPath = site.PhysicalPath
        });

        if (!string.IsNullOrEmpty(site.AppPoolName))
        {
            var pool = await _discovery.GetAppPoolAsync(site.AppPoolName);
            if (pool is not null)
            {
                deps.Add(new DependencyInfo
                {
                    Name = $"App Pool: {pool.Name}",
                    Type = "AppPool",
                    Version = $"{pool.ManagedRuntimeVersion} ({pool.PipelineMode})",
                    Required = true
                });
            }
        }

        // Check for web.config to determine framework requirements
        var webConfigPath = Path.Combine(site.PhysicalPath, "web.config");
        if (File.Exists(webConfigPath))
        {
            deps.Add(new DependencyInfo
            {
                Name = "ASP.NET Framework",
                Type = "Runtime",
                Version = "Required (web.config found)",
                Required = true
            });
        }

        var appSettingsPath = Path.Combine(site.PhysicalPath, "appsettings.json");
        if (File.Exists(appSettingsPath))
        {
            deps.Add(new DependencyInfo
            {
                Name = "ASP.NET Core Runtime",
                Type = "Runtime",
                Version = "Required (appsettings.json found)",
                Required = true
            });
        }

        return deps;
    }

    public async Task<List<DependencyInfo>> ScanPackageDependenciesAsync(string packagePath)
    {
        _logger.Information("Scanning dependencies from package: {Path}", packagePath);

        var deps = new List<DependencyInfo>();

        try
        {
            var manifest = await new Packaging.ZipPackageBuilderService()
                .ReadManifestAsync(packagePath);

            if (manifest.Dependencies.Count > 0)
            {
                deps.AddRange(manifest.Dependencies);
            }

            // Check which dependencies from the package are missing on this server
            var serverDeps = await ScanServerDependenciesAsync();

            foreach (var pkgDep in manifest.Dependencies)
            {
                var found = serverDeps.Any(sd =>
                    string.Equals(sd.Name, pkgDep.Name, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(sd.Type, pkgDep.Type, StringComparison.OrdinalIgnoreCase));

                if (!found && pkgDep.Required)
                {
                    deps.Add(new DependencyInfo
                    {
                        Name = pkgDep.Name,
                        Type = "Missing",
                        Version = pkgDep.Version,
                        Required = true,
                        DownloadUrl = pkgDep.DownloadUrl
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warning("Could not scan package dependencies: {Message}", ex.Message);
            deps.Add(new DependencyInfo
            {
                Name = "Package analysis",
                Type = "Error",
                Version = ex.Message,
                Required = true
            });
        }

        return deps;
    }

    public async Task<bool> CheckDependencyAsync(string name, string? version = null)
    {
        var allDeps = await ScanServerDependenciesAsync();
        return allDeps.Any(d =>
            d.Name.Contains(name, StringComparison.OrdinalIgnoreCase) &&
            (version is null || d.Version?.Contains(version, StringComparison.OrdinalIgnoreCase) == true));
    }
}
