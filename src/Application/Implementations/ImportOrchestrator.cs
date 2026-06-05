using IISDeploy.Application.DTOs;
using IISDeploy.Application.Services;
using IISDeploy.Core.Interfaces;
using IISDeploy.Core.Models;
using IISDeploy.Core.Models.Enums;

namespace IISDeploy.Application.Implementations;

public class ImportOrchestrator : IImportOrchestrator
{
    private readonly IIisImportService _importService;
    private readonly IPackageBuilderService _packageBuilder;
    private readonly IValidationService _validationService;
    private readonly IConflictResolutionService _conflictResolver;
    private readonly ITransactionManager _transactionManager;
    private readonly IReportGeneratorService _reportGenerator;
    private readonly ILoggingService _logger;
    private readonly INotificationSoundService _soundService;

    public ImportOrchestrator(
        IIisImportService importService,
        IPackageBuilderService packageBuilder,
        IValidationService validationService,
        IConflictResolutionService conflictResolver,
        ITransactionManager transactionManager,
        IReportGeneratorService reportGenerator,
        ILoggingService logger,
        INotificationSoundService soundService)
    {
        _importService = importService;
        _packageBuilder = packageBuilder;
        _validationService = validationService;
        _conflictResolver = conflictResolver;
        _transactionManager = transactionManager;
        _reportGenerator = reportGenerator;
        _logger = logger;
        _soundService = soundService;
    }

    public async Task<ImportResult> ImportAsync(
        ImportRequest request,
        CancellationToken cancellationToken = default,
        IProgress<OperationProgress>? progress = null)
    {
        var startTime = DateTime.UtcNow;
        var result = new ImportResult();

        try
        {
            _logger.OperationStart("ImportOrchestration", $"Importing from {request.PackagePath}");

            var preReqs = await ValidateImportPrerequisitesAsync(request.PackagePath, cancellationToken);
            if (!preReqs.IsValid)
            {
                var critical = preReqs.Issues
                    .FirstOrDefault(i => i.Severity == ValidationSeverity.Critical
                        || i.Severity == ValidationSeverity.Error);

                result.Errors.Add(critical?.Message ?? "Import prerequisites not met.");
                result.Success = false;
                return result;
            }

            var manifest = await _packageBuilder.ReadManifestAsync(request.PackagePath);
            _logger.Information("Manifest loaded: {Sites} sites, {Pools} pools, exported from {Machine}",
                manifest.ExportedSiteNames.Count, manifest.ExportedAppPoolNames.Count,
                manifest.SourceMachine.MachineName);

            // Execute import with retry support
            MigrationReport? report = null;

            await _transactionManager.ExecuteWithRetryAsync(async ct =>
            {
                report = await _importService.ImportPackageAsync(
                    request.PackagePath,
                    request.ConflictStrategies,
                    request.CredentialOverrides ?? [],
                    request.SitePathOverrides,
                    request.CustomNames,
                    request.CustomPorts,
                    request.DryRun,
                    request.AutoRemediate,
                    ct,
                    progress);

            }, maxRetries: request.AutoRemediate ? 1 : 2,
               delayBetweenRetries: TimeSpan.FromSeconds(2),
               cancellationToken: cancellationToken);

            result.Success = report!.Status != OperationStatus.Failed;
            result.Report = report;
            result.Errors = report.Entries
                .Where(e => !e.Success)
                .Select(e => $"{e.Category}/{e.Item}: {e.Message}")
                .ToList();
            result.Duration = DateTime.UtcNow - startTime;

            await _reportGenerator.GenerateHtmlReportAsync(report);
            await _reportGenerator.GenerateJsonReportAsync(report);

            _logger.OperationComplete("ImportOrchestration", result.Duration,
                "Import completed: {Success}", result.Success);
            _soundService.PlaySuccess();
        }
        catch (OperationCanceledException)
        {
            result.Errors.Add("Import was cancelled.");
            _logger.Warning("Import cancelled by user.");
        }
        catch (Exception ex)
        {
            var msg = $"[{ex.GetType().Name}] {ex.Message}";
            if (ex.InnerException is not null)
                msg += $" | Inner [{ex.InnerException.GetType().Name}]: {ex.InnerException.Message}";
            result.Errors.Add(msg);
            result.Success = false;
            _soundService.PlayFailure();
            _logger.Error(ex, "Import failed: {Message}", ex.Message);
        }

        result.Duration = DateTime.UtcNow - startTime;
        return result;
    }

    public async Task<ValidationResult> ValidateImportPrerequisitesAsync(
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        var result = new ValidationResult();

        var envResult = await _validationService.ValidateEnvironmentAsync();
        result.Issues.AddRange(envResult.Issues);

        var pkgResult = await _validationService.ValidatePackageAsync(packagePath);
        result.Issues.AddRange(pkgResult.Issues);

        if (!envResult.IsValid || !pkgResult.IsValid)
        {
            result.IsValid = false;
            return result;
        }

        try
        {
            var manifest = await _packageBuilder.ReadManifestAsync(packagePath);

            if (manifest.ExportedSiteNames.Count == 0)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Code = "IMP-WARN-001",
                    Message = "Package contains no sites to import."
                });
            }

            result.Issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Information,
                Code = "IMP-INFO-001",
                Message = $"Package contains {manifest.ExportedSiteNames.Count} site(s), " +
                    $"{manifest.ExportedAppPoolNames.Count} app pool(s).",
                Detail = $"Exported from {manifest.SourceMachine.MachineName} at {manifest.ExportTimestamp:yyyy-MM-dd HH:mm}"
            });
        }
        catch (Exception ex)
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Critical,
                Code = "PKG-ERR-004",
                Message = "Failed to read package manifest.",
                Detail = ex.Message
            });
        }

        var isValid = !result.Issues.Any(i =>
            i.Severity == ValidationSeverity.Error || i.Severity == ValidationSeverity.Critical);

        result.IsValid = isValid;
        return result;
    }
}
