using System.Text.Json;
using IISDeploy.Application.Services;
using IISDeploy.Core.Interfaces;
using IISDeploy.Core.Models;
using IISDeploy.Core.Models.Enums;
using IISDeploy.Infrastructure.Transactions;
using Microsoft.Web.Administration;

namespace IISDeploy.Infrastructure.Iis;

public class IisImportService : IIisImportService
{
    private readonly IPackageBuilderService _packageBuilder;
    private readonly IIisDiscoveryService _discovery;
    private readonly IConflictResolutionService _conflictResolver;
    private readonly IBindingManagerService _bindingManager;
    private readonly ILoggingService _logger;
    private readonly TransactionManager _transactionManager;
    private readonly IReportGeneratorService _reportGenerator;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public IisImportService(
        IPackageBuilderService packageBuilder,
        IIisDiscoveryService discovery,
        IConflictResolutionService conflictResolver,
        IBindingManagerService bindingManager,
        ILoggingService logger,
        TransactionManager transactionManager,
        IReportGeneratorService reportGenerator)
    {
        _packageBuilder = packageBuilder;
        _discovery = discovery;
        _conflictResolver = conflictResolver;
        _bindingManager = bindingManager;
        _logger = logger;
        _transactionManager = transactionManager;
        _reportGenerator = reportGenerator;
    }

    public async Task<MigrationReport> ImportPackageAsync(
        string packagePath,
        Dictionary<string, ConflictResolutionStrategy>? conflictStrategies = null,
        Dictionary<string, string>? credentialOverrides = null,
        Dictionary<string, string>? sitePathOverrides = null,
        Dictionary<string, string>? customNames = null,
        Dictionary<string, int>? customPorts = null,
        bool dryRun = false,
        bool autoRemediate = false,
        CancellationToken cancellationToken = default,
        IProgress<OperationProgress>? progress = null)
    {
        var report = new MigrationReport
        {
            ReportId = Guid.NewGuid().ToString("N"),
            GeneratedAt = DateTime.UtcNow,
            ReportType = "Import",
            TargetMachine = new MachineInfo
            {
                MachineName = Environment.MachineName,
                OsVersion = Environment.OSVersion.VersionString
            }
        };

        var extractionPath = Path.Combine(Path.GetTempPath(), $"iisdeploy_import_{Guid.NewGuid():N}");

        try
        {
            await using var scope = _transactionManager.BeginTransaction("ImportPackage");
            var createdPoolNames = new List<string>();
            var createdSiteNames = new List<string>();

            Directory.CreateDirectory(extractionPath);
            ExtractPackage(packagePath, extractionPath);

            var manifest = await _packageBuilder.ReadManifestAsync(packagePath);
            report.SourceManifest = manifest;

            if (!string.IsNullOrEmpty(manifest.Checksum))
            {
                var previous = await _reportGenerator.FindPreviousImportAsync(
                    manifest.Checksum, report.TargetMachine.MachineName);
                if (previous is not null)
                {
                    report.Status = OperationStatus.Skipped;
                    report.Summary.Add("Skipped: package already imported on this machine.");
                    report.Summary.Add($"Previous import: {previous.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC " +
                        $"(report {previous.ReportId}).");
                    await scope.CommitAsync();
                    return report;
                }
            }

            // Read site configs
            var sites = LoadJsonFiles<IisSite>(Path.Combine(extractionPath, "sites"), "site.json");
            var pools = LoadJsonFiles<IisApplicationPool>(Path.Combine(extractionPath, "apppools"), "apppool.json");
            var stagingFilesPath = Path.Combine(extractionPath, "files");

            ReportProgress(progress, OperationStatus.InProgress, 0, sites.Count + pools.Count, 0,
                "Analyzing conflicts");

            // Conflict analysis
            var conflictReport = await _conflictResolver.AnalyzeImportConflictsAsync(sites, pools);

            if (!dryRun && conflictReport.HasConflicts)
            {
                if (!autoRemediate)
                {
                    report.Summary.Add($"{conflictReport.SiteConflicts.Count} site conflicts, " +
                        $"{conflictReport.PoolConflicts.Count} pool conflicts, " +
                        $"{conflictReport.BindingConflicts.Count} binding conflicts detected.");
                }

                // Resolve with user strategies or skip unresolved
                await ResolveConflicts(sites, conflictReport, conflictStrategies);
            }

            if (dryRun)
            {
                report.Status = OperationStatus.Completed;
                report.Summary.Add($"Dry run complete. {sites.Count} sites, {pools.Count} pools would be imported.");
                report.Summary.Add($"Conflicts: {conflictReport.SiteConflicts.Count} sites, " +
                    $"{conflictReport.PoolConflicts.Count} pools, {conflictReport.BindingConflicts.Count} bindings.");
                await scope.CommitAsync();
                return report;
            }

            // Phase 1: Import App Pools
            ReportProgress(progress, OperationStatus.InProgress, 0, pools.Count, 0,
                "Creating application pools");

            int importedPools = 0;
            int failedPools = 0;
            var importedPoolNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            using (var manager = new ServerManager())
            {
                foreach (var pool in pools)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        _logger.Debug("Importing app pool: {Name}", pool.Name);
                        var existing = manager.ApplicationPools.FirstOrDefault(p =>
                            string.Equals(p.Name, pool.Name, StringComparison.OrdinalIgnoreCase));

                        var originalPoolName = pool.Name;

                        if (existing is not null)
                        {
                            var strategy = conflictReport.PoolConflicts
                                .FirstOrDefault(c => c.ObjectName == originalPoolName)?.ChosenResolution
                                ?? ConflictResolutionStrategy.Overwrite;

                            if (strategy == ConflictResolutionStrategy.Skip)
                            {
                                importedPoolNameMap[originalPoolName] = existing.Name;
                                report.Entries.Add(new ReportEntry
                                {
                                    Category = "AppPool",
                                    Item = originalPoolName,
                                    Success = true,
                                    Message = "Skipped (already exists)"
                                });
                                importedPools++;
                                continue;
                            }

                            if (strategy == ConflictResolutionStrategy.Overwrite)
                            {
                                manager.ApplicationPools.Remove(existing);
                                importedPoolNameMap[originalPoolName] = originalPoolName;
                            }
                            else if (strategy == ConflictResolutionStrategy.Rename
                                  || strategy == ConflictResolutionStrategy.Clone)
                            {
                                pool.Name = customNames is not null && customNames.TryGetValue(originalPoolName, out var poolName)
                                    ? poolName
                                    : $"{originalPoolName}_{(strategy == ConflictResolutionStrategy.Rename ? "Imported" : "Clone")}_{DateTime.Now:yyyyMMddHHmmss}";
                                importedPoolNameMap[originalPoolName] = pool.Name;
                            }
                        }
                        else
                        {
                            importedPoolNameMap[originalPoolName] = pool.Name;
                        }

                        ImportAppPool(manager, pool, credentialOverrides);
                        importedPools++;
                        createdPoolNames.Add(pool.Name);

                        report.Entries.Add(new ReportEntry
                        {
                            Category = "AppPool",
                            Item = pool.Name,
                            Success = true,
                            Message = "Created"
                        });
                    }
                    catch (Exception ex)
                    {
                        failedPools++;
                        report.Entries.Add(new ReportEntry
                        {
                            Category = "AppPool",
                            Item = pool.Name,
                            Success = false,
                            Message = ex.Message
                        });
                        _logger.Error(ex, "Failed to import app pool {Name}", pool.Name);
                    }
                }

                manager.CommitChanges();
            }

            // Phase 2: Import Sites
            ReportProgress(progress, OperationStatus.InProgress, importedPools, pools.Count, failedPools,
                "Creating sites");

            int importedSites = 0;
            int failedSites = 0;

            using (var manager = new ServerManager())
            {
                foreach (var site in sites)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        _logger.Debug("Importing site: {Name}", site.Name);
                        ImportSite(manager, site, stagingFilesPath, conflictReport, conflictStrategies,
                            sitePathOverrides, customNames, customPorts, importedPoolNameMap);
                        importedSites++;
                        createdSiteNames.Add(site.Name);

                        report.Entries.Add(new ReportEntry
                        {
                            Category = "Site",
                            Item = site.Name,
                            Success = true,
                            Message = "Created"
                        });
                    }
                    catch (Exception ex)
                    {
                        failedSites++;
                        report.Entries.Add(new ReportEntry
                        {
                            Category = "Site",
                            Item = site.Name,
                            Success = false,
                            Message = ex.Message
                        });
                        _logger.Error(ex, "Failed to import site {Name}", site.Name);
                    }
                }

                manager.CommitChanges();
            }

            PostValidateImport(createdPoolNames, createdSiteNames, report);

            foreach (var poolName in createdPoolNames)
                scope.RegisterRollback(() => DeletePoolByNameAsync(poolName));
            foreach (var siteName in createdSiteNames)
                scope.RegisterRollback(() => DeleteSiteByNameAsync(siteName));

            await scope.CommitAsync();

            report.Status = failedSites > 0 || failedPools > 0
                ? OperationStatus.CompletedWithWarnings
                : OperationStatus.Completed;

            report.Summary.Add($"Imported {importedSites} sites ({failedSites} failed), " +
                $"{importedPools} app pools ({failedPools} failed).");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Import failed: {Message}", ex.Message);
            report.Status = OperationStatus.Failed;
            report.Summary.Add($"Import failed: {ex.Message}");
            if (ex.InnerException is not null)
                report.Summary.Add($"Inner: {ex.InnerException.Message}");
        }
        finally
        {
            try { if (Directory.Exists(extractionPath)) Directory.Delete(extractionPath, recursive: true); }
            catch { /* cleanup best-effort */ }
        }

        ReportProgress(progress, report.Status, 0, 0, 0, "Import complete");
        return report;
    }

    private void ImportAppPool(
        ServerManager manager,
        IisApplicationPool poolModel,
        Dictionary<string, string>? credentialOverrides)
    {
        try
        {
        var pool = manager.ApplicationPools.FirstOrDefault(p =>
            string.Equals(p.Name, poolModel.Name, StringComparison.OrdinalIgnoreCase));

        if (pool is null)
        {
            pool = manager.ApplicationPools.Add(poolModel.Name);
        }

        pool.ManagedPipelineMode = poolModel.PipelineMode == PipelineMode.Integrated
            ? ManagedPipelineMode.Integrated
            : ManagedPipelineMode.Classic;

        pool.ManagedRuntimeVersion = poolModel.ManagedRuntimeVersion;
        pool.Enable32BitAppOnWin64 = poolModel.Enable32BitAppOnWin64;
        pool.AutoStart = poolModel.AutoStart;
        pool.ProcessModel.IdleTimeout = TimeSpan.FromMinutes(poolModel.IdleTimeoutMinutes);
        pool.ProcessModel.MaxProcesses = poolModel.MaxProcesses;
        pool.Recycling.PeriodicRestart.Time = TimeSpan.FromMinutes(poolModel.RegularTimeInterval);
        pool.Recycling.PeriodicRestart.PrivateMemory = poolModel.PrivateMemoryLimit;
        pool.Recycling.PeriodicRestart.Memory = poolModel.VirtualMemoryLimit;

        // Identity
        pool.ProcessModel.IdentityType = poolModel.IdentityType switch
        {
            AppPoolIdentityType.LocalSystem => ProcessModelIdentityType.LocalSystem,
            AppPoolIdentityType.LocalService => ProcessModelIdentityType.LocalService,
            AppPoolIdentityType.NetworkService => ProcessModelIdentityType.NetworkService,
            AppPoolIdentityType.ApplicationPoolIdentity => ProcessModelIdentityType.ApplicationPoolIdentity,
            AppPoolIdentityType.SpecificUser => ProcessModelIdentityType.SpecificUser,
            _ => ProcessModelIdentityType.ApplicationPoolIdentity
        };

        if (poolModel.IdentityType == AppPoolIdentityType.SpecificUser)
        {
            pool.ProcessModel.UserName = poolModel.CustomUserName ?? string.Empty;

            var credentialKey = $"pool:{poolModel.Name}";
            if (credentialOverrides is not null && credentialOverrides.TryGetValue(credentialKey, out var password))
            {
                pool.ProcessModel.Password = password;
            }
        }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to import app pool '{poolModel.Name}': {ex.Message}", ex);
        }
    }

    private void ImportSite(
        ServerManager manager,
        IisSite siteModel,
        string stagingFilesPath,
        ConflictReport conflictReport,
        Dictionary<string, ConflictResolutionStrategy>? conflictStrategies,
        Dictionary<string, string>? sitePathOverrides,
        Dictionary<string, string>? customNames,
        Dictionary<string, int>? customPorts,
        Dictionary<string, string>? importedPoolNameMap)
    {
        try
        {
        var originalName = siteModel.Name;
        AppPoolNameResolver.ApplyResolvedPoolNames(siteModel, importedPoolNameMap);

        var existingSite = manager.Sites.FirstOrDefault(s =>
            string.Equals(s.Name, siteModel.Name, StringComparison.OrdinalIgnoreCase));

        var strategy = conflictReport.SiteConflicts
            .FirstOrDefault(c => c.ObjectName == originalName)?.ChosenResolution
            ?? ConflictResolutionStrategy.Overwrite;

        if (existingSite is not null)
        {
            switch (strategy)
            {
                case ConflictResolutionStrategy.Skip:
                    return;

                case ConflictResolutionStrategy.Overwrite:
                    manager.Sites.Remove(existingSite);
                    break;

                case ConflictResolutionStrategy.Rename:
                    siteModel.Name = customNames is not null && customNames.TryGetValue(originalName, out var customName)
                        ? customName
                        : $"{originalName}_Imported_{DateTime.Now:yyyyMMddHHmmss}";
                    break;

                case ConflictResolutionStrategy.Clone:
                    siteModel.Name = customNames is not null && customNames.TryGetValue(originalName, out var cloneName)
                        ? cloneName
                        : $"{originalName}_Clone_{DateTime.Now:yyyyMMddHHmmss}";
                    break;

                case ConflictResolutionStrategy.ChangeBinding:
                    manager.Sites.Remove(existingSite);
                    if (customNames is not null && customNames.TryGetValue(originalName, out var altName))
                        siteModel.Name = altName;
                    break;

                default:
                    manager.Sites.Remove(existingSite);
                    break;
            }
        }

        // Apply custom port overrides regardless of site strategy
        ApplyCustomPorts(siteModel, customPorts);

        // Determine physical path
        var siteFilesSource = Path.Combine(stagingFilesPath, SanitizeFileName(originalName));
        if (!Directory.Exists(siteFilesSource))
            siteFilesSource = Path.Combine(stagingFilesPath, SanitizeFileName(siteModel.Name));

        var physicalPath = siteModel.PhysicalPath;

        // Use user-specified path override if provided (check both original and new name)
        if (sitePathOverrides is not null &&
            (sitePathOverrides.TryGetValue(originalName, out var overridePath)
             || sitePathOverrides.TryGetValue(siteModel.Name, out overridePath))
            && !string.IsNullOrWhiteSpace(overridePath))
        {
            physicalPath = overridePath;
        }

        if (Directory.Exists(siteFilesSource))
        {
            _logger.Debug("Copying site files from {Source} to {Dest}", siteFilesSource, physicalPath);
            if (!Directory.Exists(physicalPath))
                Directory.CreateDirectory(physicalPath);

            CopyDirectory(siteFilesSource, physicalPath);
        }
        else
        {
            _logger.Debug("Site files source not found: {Source}", siteFilesSource);
        }

        var site = manager.Sites.Add(siteModel.Name, siteModel.Bindings.FirstOrDefault()?.Protocol ?? "http",
            $"{siteModel.Bindings.FirstOrDefault()?.BindingInformation ?? "*:80:"}", physicalPath);

        site.ServerAutoStart = siteModel.ServerAutoStart;

        // Import bindings (skip the first which was created with the site)
        foreach (var binding in siteModel.Bindings.Skip(1))
        {
            try
            {
                site.Bindings.Add(binding.BindingInformation, binding.Protocol);
            }
            catch (Exception ex)
            {
                _logger.Warning("Could not add binding {Binding}: {Message}",
                    binding.BindingInformation, ex.Message);
            }
        }

        // Set SSL flags if present
        foreach (var binding in site.Bindings)
        {
            var sourceBinding = siteModel.Bindings.FirstOrDefault(b =>
                string.Equals(b.BindingInformation, binding.BindingInformation,
                    StringComparison.OrdinalIgnoreCase));

            if (sourceBinding?.SslFlags is not null && int.TryParse(sourceBinding.SslFlags, out var flags))
            {
                binding.SetAttributeValue("sslFlags", flags);
            }

            if (!string.IsNullOrEmpty(sourceBinding?.CertificateHash))
            {
                var certHash = SafeStringToByteArray(sourceBinding.CertificateHash);
                if (certHash is not null)
                {
                    binding.CertificateHash = certHash;
                    binding.CertificateStoreName = sourceBinding.CertificateStoreName ?? "My";
                }
            }
        }

        // Set root application pool from the site definition.
        var rootApp = site.Applications.FirstOrDefault(a => a.Path == "/");
        if (rootApp is not null && !string.IsNullOrWhiteSpace(siteModel.AppPoolName))
        {
            rootApp.ApplicationPoolName = siteModel.AppPoolName;
        }

        // Import child applications (the root app already exists from Site.Add).
        foreach (var appModel in siteModel.Applications.Where(a => a.Path != "/"))
        {
            try
            {
                var app = site.Applications.Add(appModel.Path, appModel.PhysicalPath);
                if (!string.IsNullOrEmpty(appModel.ApplicationPoolName))
                    app.ApplicationPoolName = appModel.ApplicationPoolName;

                foreach (var vdir in appModel.VirtualDirectories)
                {
                    app.VirtualDirectories.Add(vdir.Path, vdir.PhysicalPath);
                }
            }
            catch (Exception ex)
            {
                _logger.Warning("Could not add application {Path}: {Message}",
                    appModel.Path, ex.Message);
            }
        }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to import site '{siteModel.Name}': {ex.Message}", ex);
        }
    }

    private async Task ResolveConflicts(
        List<IisSite> sites,
        ConflictReport report,
        Dictionary<string, ConflictResolutionStrategy>? userStrategies)
    {
        foreach (var entry in report.SiteConflicts)
        {
            entry.ChosenResolution = _conflictResolver.GetEffectiveStrategy(
                $"site:{entry.ObjectName}", userStrategies,
                entry.SuggestedResolution);
        }

        foreach (var entry in report.PoolConflicts)
        {
            entry.ChosenResolution = _conflictResolver.GetEffectiveStrategy(
                $"pool:{entry.ObjectName}", userStrategies,
                entry.SuggestedResolution);
        }

        foreach (var entry in report.BindingConflicts)
        {
            entry.ChosenResolution = _conflictResolver.GetEffectiveStrategy(
                $"binding:{entry.ObjectName}", userStrategies,
                entry.SuggestedResolution);
        }

        await Task.CompletedTask;
    }

    private async Task<int> FindAlternativePort(int basePort)
    {
        for (int offset = 1; offset < 1000; offset++)
        {
            var candidate = basePort + offset;
            if (candidate > 65535) break;
            if (await _bindingManager.IsPortAvailableAsync("*", candidate))
                return candidate;
        }

        return basePort + 10000;
    }

    private static void ExtractPackage(string packagePath, string destinationDir)
    {
        System.IO.Compression.ZipFile.ExtractToDirectory(packagePath, destinationDir, overwriteFiles: true);
    }

    private List<T> LoadJsonFiles<T>(string directory, string fileName)
    {
        var results = new List<T>();
        if (!Directory.Exists(directory)) return results;

        foreach (var subDir in Directory.GetDirectories(directory))
        {
            var filePath = Path.Combine(subDir, fileName);
            if (!File.Exists(filePath)) continue;

            try
            {
                var json = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    _logger.Warning("Empty JSON file: {Path}", filePath);
                    continue;
                }

                var obj = JsonSerializer.Deserialize<T>(json, _jsonOptions);
                if (obj is not null) results.Add(obj);
            }
            catch (JsonException ex)
            {
                _logger.Warning("JSON parse error in {Path}: {Message} (Path: {JsonPath})",
                    filePath, ex.Message, ex.Path);
            }
            catch (FormatException ex)
            {
                _logger.Warning("Numeric format error in {Path}: {Message}", filePath, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.Warning("Failed to deserialize {Path}: {Message}", filePath, ex.Message);
            }
        }

        return results;
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        DirectoryCopyHelper.CopyDirectory(sourceDir, destDir);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(safe) ? "unnamed" : safe;
    }

    private static void ApplyCustomPorts(IisSite siteModel, Dictionary<string, int>? customPorts)
    {
        if (customPorts is null || customPorts.Count == 0)
            return;

        foreach (var b in siteModel.Bindings)
        {
            if (customPorts.TryGetValue(b.BindingInformation, out var newPort))
            {
                b.Port = newPort.ToString();
                UpdateBindingInformation(b, newPort);
            }
        }
    }

    private static void UpdateBindingInformation(BindingInfo binding, int newPort)
    {
        var parts = binding.BindingInformation.Split(':');
        if (parts.Length >= 2)
        {
            parts[1] = newPort.ToString();
            binding.BindingInformation = string.Join(":", parts);
        }
    }

    private byte[]? SafeStringToByteArray(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return null;

        try
        {
            var cleaned = hex
                .Replace("-", "")
                .Replace(" ", "")
                .Replace(":", "")
                .Replace("\n", "")
                .Replace("\r", "")
                .Replace("\t", "")
                .Trim();

            if (cleaned.Length % 2 != 0)
            {
                _logger.Warning("Certificate hash has odd length ({Length}) after cleaning", cleaned.Length);
                return null;
            }

            var bytes = new byte[cleaned.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(cleaned.Substring(i * 2, 2), 16);

            return bytes;
        }
        catch (Exception ex)
        {
            _logger.Warning("Failed to parse certificate hash: {Message}", ex.Message);
            return null;
        }
    }

    private static void ReportProgress(
        IProgress<OperationProgress>? progress,
        OperationStatus status, int completed, int total, int failed, string step)
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

    public static List<ReportEntry> BuildPostValidationEntries(
        List<string> createdPoolNames,
        List<string> createdSiteNames,
        IReadOnlyDictionary<string, string> poolStates,
        IReadOnlyDictionary<string, string> siteStates)
    {
        var entries = new List<ReportEntry>();

        foreach (var name in createdPoolNames)
        {
            if (!poolStates.TryGetValue(name, out var state))
            {
                entries.Add(new ReportEntry
                {
                    Category = "PostValidation",
                    Item = name,
                    Success = false,
                    Message = $"App pool '{name}' not found in IIS after import."
                });
            }
            else if (!string.Equals(state, "Started", StringComparison.OrdinalIgnoreCase))
            {
                entries.Add(new ReportEntry
                {
                    Category = "PostValidation",
                    Item = name,
                    Success = false,
                    Message = $"App pool '{name}' state is {state} (expected Started)."
                });
            }
        }

        foreach (var name in createdSiteNames)
        {
            if (!siteStates.TryGetValue(name, out _))
            {
                entries.Add(new ReportEntry
                {
                    Category = "PostValidation",
                    Item = name,
                    Success = false,
                    Message = $"Site '{name}' not found in IIS after import."
                });
            }
        }

        return entries;
    }

    private void PostValidateImport(
        List<string> createdPoolNames,
        List<string> createdSiteNames,
        MigrationReport report)
    {
        try
        {
            var poolStates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var siteStates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            using (var manager = new Microsoft.Web.Administration.ServerManager())
            {
                foreach (var name in createdPoolNames)
                {
                    var p = manager.ApplicationPools.FirstOrDefault(x =>
                        string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
                    poolStates[name] = p?.State.ToString() ?? "__missing__";
                }
                foreach (var name in createdSiteNames)
                {
                    var s = manager.Sites.FirstOrDefault(x =>
                        string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
                    siteStates[name] = s is null ? "__missing__" : "present";
                }
            }

            foreach (var entry in BuildPostValidationEntries(createdPoolNames, createdSiteNames, poolStates, siteStates))
            {
                report.Entries.Add(entry);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning("Post-validation failed: {Message}", ex.Message);
        }
    }

    // Best-effort cleanup helpers used by transaction rollback actions.
    // Each opens a fresh ServerManager to see committed IIS state.
    // Exceptions propagate to TransactionScope.RollbackAsync, which logs and continues.

    private static Task DeletePoolByNameAsync(string name)
    {
        using var manager = new Microsoft.Web.Administration.ServerManager();
        var pool = manager.ApplicationPools.FirstOrDefault(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (pool is not null)
        {
            manager.ApplicationPools.Remove(pool);
            manager.CommitChanges();
        }
        return Task.CompletedTask;
    }

    private static Task DeleteSiteByNameAsync(string name)
    {
        using var manager = new Microsoft.Web.Administration.ServerManager();
        var site = manager.Sites.FirstOrDefault(s =>
            string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        if (site is not null)
        {
            manager.Sites.Remove(site);
            manager.CommitChanges();
        }
        return Task.CompletedTask;
    }
}
