using IISDeploy.Application.DTOs;
using IISDeploy.Application.Services;
using IISDeploy.Core.Interfaces;
using IISDeploy.Core.Models;
using IISDeploy.Core.Models.Enums;

namespace IISDeploy.Application.Implementations;

public class ExportOrchestrator : IExportOrchestrator
{
    private readonly IIisExportService _exportService;
    private readonly IValidationService _validationService;
    private readonly IIisDiscoveryService _discoveryService;
    private readonly IReportGeneratorService _reportGenerator;
    private readonly ILoggingService _logger;
    private readonly INotificationSoundService _soundService;

    public ExportOrchestrator(
        IIisExportService exportService,
        IValidationService validationService,
        IIisDiscoveryService discoveryService,
        IReportGeneratorService reportGenerator,
        ILoggingService logger,
        INotificationSoundService soundService)
    {
        _exportService = exportService;
        _validationService = validationService;
        _discoveryService = discoveryService;
        _reportGenerator = reportGenerator;
        _logger = logger;
        _soundService = soundService;
    }

    public async Task<ExportResult> ExportAsync(
        ExportRequest request,
        CancellationToken cancellationToken = default,
        IProgress<Core.Models.OperationProgress>? progress = null)
    {
        var startTime = DateTime.UtcNow;
        var result = new ExportResult();

        try
        {
            _logger.OperationStart("ExportOrchestration",
                $"Exporting {request.SiteNames.Count} site(s) to {request.OutputPath}");

            await ValidateExportPrerequisitesAsync(request, cancellationToken);

            var packagePath = await _exportService.ExportSitesAsync(
                request.SiteNames,
                request.OutputPath,
                request.Mode,
                request.IncludeCertificates,
                request.CertificatePassword,
                request.IncludeNtfsPermissions,
                cancellationToken,
                progress);

            result.Success = true;
            result.PackagePath = packagePath;
            result.Duration = DateTime.UtcNow - startTime;

            var report = new MigrationReport
            {
                ReportId = Guid.NewGuid().ToString("N"),
                GeneratedAt = DateTime.UtcNow,
                ReportType = "Export",
                Status = OperationStatus.Completed,
                TargetMachine = new MachineInfo
                {
                    MachineName = Environment.MachineName,
                    OsVersion = Environment.OSVersion.VersionString
                },
                Summary = [$"Exported {request.SiteNames.Count} site(s) to {packagePath}"]
            };
            result.Report = report;

            await _reportGenerator.GenerateHtmlReportAsync(report);
            await _reportGenerator.GenerateJsonReportAsync(report);

            _logger.OperationComplete("ExportOrchestration", result.Duration,
                "Package created: {Path}", packagePath);
            _soundService.PlaySuccess();
        }
        catch (OperationCanceledException)
        {
            result.Errors.Add("Export was cancelled.");
            _logger.Warning("Export cancelled by user.");
        }
        catch (Exception ex)
        {
            result.Errors.Add(ex.Message);
            _soundService.PlayFailure();
            _logger.Error(ex, "Export failed");
        }

        result.Duration = DateTime.UtcNow - startTime;
        return result;
    }

    public async Task ValidateExportPrerequisitesAsync(
        ExportRequest request,
        CancellationToken cancellationToken = default)
    {
        var envResult = await _validationService.ValidateEnvironmentAsync();
        if (!envResult.IsValid)
        {
            var critical = envResult.Issues
                .FirstOrDefault(i => i.Severity == ValidationSeverity.Critical
                    || i.Severity == ValidationSeverity.Error);

            if (critical is not null)
                throw new InvalidOperationException($"Prerequisites not met: {critical.Message}");
        }

        foreach (var siteName in request.SiteNames)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var siteResult = await _validationService.ValidateSiteForExportAsync(siteName);
            if (!siteResult.IsValid)
            {
                var error = siteResult.Issues.FirstOrDefault(i =>
                    i.Severity == ValidationSeverity.Critical
                    || i.Severity == ValidationSeverity.Error);

                if (error is not null)
                    throw new InvalidOperationException(
                        $"Cannot export '{siteName}': {error.Message}");
            }
        }

        var outputDir = Path.GetDirectoryName(request.OutputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);
    }
}
