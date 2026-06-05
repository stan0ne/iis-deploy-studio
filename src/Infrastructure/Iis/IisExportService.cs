using System.Text.Json;
using IISDeploy.Core.Interfaces;
using IISDeploy.Core.Models;
using IISDeploy.Core.Models.Enums;

namespace IISDeploy.Infrastructure.Iis;

public class IisExportService : IIisExportService
{
    private readonly IIisDiscoveryService _discovery;
    private readonly IPackageBuilderService _packageBuilder;
    private readonly ILoggingService _logger;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public IisExportService(
        IIisDiscoveryService discovery,
        IPackageBuilderService packageBuilder,
        ILoggingService logger)
    {
        _discovery = discovery;
        _packageBuilder = packageBuilder;
        _logger = logger;
    }

    public async Task<string> ExportSitesAsync(
        IEnumerable<string> siteNames,
        string outputPath,
        ExportMode mode,
        bool includeCertificates = false,
        string? certificatePassword = null,
        bool includeNtfsPermissions = false,
        CancellationToken cancellationToken = default,
        IProgress<OperationProgress>? progress = null)
    {
        var siteNameList = siteNames.ToList();

        _logger.OperationStart("Export", $"Exporting {siteNameList.Count} sites");

        var stagingPath = Path.Combine(Path.GetTempPath(), $"iisdeploy_export_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(stagingPath);
            Directory.CreateDirectory(Path.Combine(stagingPath, "sites"));
            Directory.CreateDirectory(Path.Combine(stagingPath, "apppools"));
            Directory.CreateDirectory(Path.Combine(stagingPath, "config"));
            Directory.CreateDirectory(Path.Combine(stagingPath, "files"));
            Directory.CreateDirectory(Path.Combine(stagingPath, "bindings"));
            Directory.CreateDirectory(Path.Combine(stagingPath, "dependencies"));

            ReportProgress(progress, OperationStatus.InProgress, 0, siteNameList.Count, 0, 0, "Building manifest");

            var serverInfo = await _discovery.GetServerInfoAsync();

            var manifest = new PackageManifest
            {
                PackageVersion = "1.0.0",
                ExportTimestamp = DateTime.UtcNow,
                ExportedBy = Environment.UserName,
                SourceMachine = new MachineInfo
                {
                    MachineName = Environment.MachineName,
                    OsVersion = Environment.OSVersion.VersionString,
                    OsArchitecture = Environment.Is64BitOperatingSystem ? "x64" : "x86",
                    FrameworkVersion = Environment.Version.ToString(),
                    TotalMemoryMb = 0,
                    ProcessorCount = Environment.ProcessorCount
                },
                SourceIisInfo = serverInfo,
                ExportedSiteNames = siteNameList,
                Warnings = []
            };

            var totalSites = siteNameList.Count;
            var completedSites = 0;
            var failedSites = 0;
            var totalAppPools = new HashSet<string>();

            for (int i = 0; i < totalSites; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var siteName = siteNameList[i];
                ReportProgress(progress, OperationStatus.InProgress, completedSites, totalSites, failedSites, 0,
                    $"Exporting site: {siteName}");

                try
                {
                    var site = await _discovery.GetSiteAsync(siteName);
                    if (site is null)
                    {
                        manifest.Warnings.Add($"Site '{siteName}' not found on this server.");
                        failedSites++;
                        continue;
                    }

                    var apps = await _discovery.GetApplicationsAsync(siteName);
                    var vdirs = await _discovery.GetVirtualDirectoriesAsync(siteName);
                    var bindings = await _discovery.GetSiteBindingsAsync(siteName);

                    site.Applications = apps;
                    site.VirtualDirectories = vdirs;
                    site.Bindings = bindings;

                    // Export site JSON
                    var siteJson = JsonSerializer.Serialize(site, _jsonOptions);
                    var siteDir = Path.Combine(stagingPath, "sites", SanitizeFileName(siteName));
                    Directory.CreateDirectory(siteDir);
                    await File.WriteAllTextAsync(Path.Combine(siteDir, "site.json"), siteJson, cancellationToken);

                    // Export app pool if referenced
                    if (!string.IsNullOrEmpty(site.AppPoolName))
                    {
                        var pool = await _discovery.GetAppPoolAsync(site.AppPoolName);
                        if (pool is not null && totalAppPools.Add(pool.Name))
                        {
                            var poolJson = JsonSerializer.Serialize(pool, _jsonOptions);
                            var poolDir = Path.Combine(stagingPath, "apppools", SanitizeFileName(pool.Name));
                            Directory.CreateDirectory(poolDir);
                            await File.WriteAllTextAsync(Path.Combine(poolDir, "apppool.json"), poolJson, cancellationToken);
                            manifest.ExportedAppPoolNames.Add(pool.Name);
                        }
                    }

                    foreach (var app in apps)
                    {
                        if (!string.IsNullOrEmpty(app.ApplicationPoolName) && totalAppPools.Add(app.ApplicationPoolName))
                        {
                            var pool = await _discovery.GetAppPoolAsync(app.ApplicationPoolName);
                            if (pool is not null)
                            {
                                var poolJson = JsonSerializer.Serialize(pool, _jsonOptions);
                                var poolDir = Path.Combine(stagingPath, "apppools", SanitizeFileName(pool.Name));
                                Directory.CreateDirectory(poolDir);
                                await File.WriteAllTextAsync(Path.Combine(poolDir, "apppool.json"), poolJson, cancellationToken);
                                manifest.ExportedAppPoolNames.Add(pool.Name);
                            }
                        }
                    }

                    // Export bindings
                    var bindingsDir = Path.Combine(stagingPath, "bindings", SanitizeFileName(siteName));
                    Directory.CreateDirectory(bindingsDir);
                    var bindingsJson = JsonSerializer.Serialize(bindings, _jsonOptions);
                    await File.WriteAllTextAsync(Path.Combine(bindingsDir, "bindings.json"), bindingsJson, cancellationToken);

                    // Export physical files
                    ReportProgress(progress, OperationStatus.InProgress, completedSites, totalSites, failedSites, 0,
                        $"Copying files for: {siteName}");

                    await CopySiteFiles(site, stagingPath, cancellationToken);

                    // Scan web.config / appsettings.json
                    await CopyConfigFiles(site.PhysicalPath, Path.Combine(stagingPath, "config"), cancellationToken);

                    completedSites++;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to export site {SiteName}", siteName);
                    manifest.Warnings.Add($"Failed to export '{siteName}': {ex.Message}");
                    failedSites++;
                }
            }

            ReportProgress(progress, OperationStatus.InProgress, completedSites, totalSites, failedSites, 0,
                "Building package");

            _logger.OperationStart("BuildPackage", "Building .iispackage");

            // Build and save reports
            var reportDir = Path.Combine(stagingPath, "reports");
            Directory.CreateDirectory(reportDir);
            var report = new MigrationReport
            {
                ReportId = Guid.NewGuid().ToString("N"),
                GeneratedAt = DateTime.UtcNow,
                ReportType = "Export",
                Status = failedSites > 0 ? OperationStatus.CompletedWithWarnings : OperationStatus.Completed,
                SourceManifest = manifest,
                TargetMachine = manifest.SourceMachine,
                Summary = [$"Exported {completedSites} of {totalSites} sites successfully, {failedSites} failed."]
            };

            var reportJson = JsonSerializer.Serialize(report, _jsonOptions);
            await File.WriteAllTextAsync(Path.Combine(reportDir, "export_report.json"), reportJson, cancellationToken);

            var manifestJson = JsonSerializer.Serialize(manifest, _jsonOptions);
            await File.WriteAllTextAsync(Path.Combine(stagingPath, "manifest.json"), manifestJson, cancellationToken);

            // Build the actual .iispackage
            var packagePath = outputPath;
            if (!packagePath.EndsWith(".iispackage", StringComparison.OrdinalIgnoreCase))
                packagePath += ".iispackage";

            await _packageBuilder.BuildPackageAsync(manifest, stagingPath, packagePath, cancellationToken);

            ReportProgress(progress, failedSites > 0 ? OperationStatus.CompletedWithWarnings : OperationStatus.Completed,
                completedSites, totalSites, failedSites, 0, "Export complete");

            return packagePath;
        }
        finally
        {
            // Clean staging
            try
            {
                if (Directory.Exists(stagingPath))
                    Directory.Delete(stagingPath, recursive: true);
            }
            catch (Exception ex)
            {
                _logger.Warning("Failed to clean staging directory: {Message}", ex.Message);
            }
        }
    }

    private async Task CopySiteFiles(IisSite site, string stagingPath, CancellationToken ct)
    {
        var siteFilesDir = Path.Combine(stagingPath, "files", SanitizeFileName(site.Name));
        Directory.CreateDirectory(siteFilesDir);

        if (!string.IsNullOrEmpty(site.PhysicalPath) && Directory.Exists(site.PhysicalPath))
        {
            await CopyDirectoryAsync(site.PhysicalPath, siteFilesDir, ct);
        }
    }

    private async Task CopyConfigFiles(string sourceDir, string configDir, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(sourceDir) || !Directory.Exists(sourceDir))
            return;

        var configFiles = new[] { "web.config", "appsettings.json", "appsettings.*.json", ".env" };
        var siteConfigDir = Path.Combine(configDir, "site_configs");
        Directory.CreateDirectory(siteConfigDir);

        foreach (var pattern in configFiles)
        {
            foreach (var file in Directory.GetFiles(sourceDir, pattern).Take(50))
            {
                var destFile = Path.Combine(siteConfigDir, Path.GetFileName(file));
                if (!File.Exists(destFile))
                    File.Copy(file, destFile, overwrite: false);
            }
        }

        await Task.CompletedTask;
    }

    private static async Task CopyDirectoryAsync(string sourceDir, string destinationDir, CancellationToken ct)
    {
        await DirectoryCopyHelper.CopyDirectoryAsync(sourceDir, destinationDir, ct);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(safe) ? "unnamed" : safe;
    }

    private static void ReportProgress(
        IProgress<OperationProgress>? progress,
        OperationStatus status,
        int completed,
        int total,
        int failed,
        int warnings,
        string step)
    {
        progress?.Report(new OperationProgress
        {
            Status = status,
            CurrentStep = step,
            TotalItems = total,
            CompletedItems = completed,
            FailedItems = failed,
            Percentage = total > 0 ? (int)((double)completed / total * 100) : 0
        });
    }
}
