using IISDeploy.Application.DTOs;
using IISDeploy.Application.Services;
using IISDeploy.Core.Interfaces;
using IISDeploy.Core.Models;
using IISDeploy.Core.Models.Enums;

namespace IISDeploy.Application.Implementations;

public class DependencyAnalysisService : IDependencyAnalysisService
{
    private readonly IDependencyScannerService _scanner;
    private readonly ILoggingService _logger;

    public DependencyAnalysisService(
        IDependencyScannerService scanner,
        ILoggingService logger)
    {
        _scanner = scanner;
        _logger = logger;
    }

    public async Task<DependencyScanResult> ScanAsync(
        DependencyScanRequest request,
        CancellationToken cancellationToken = default,
        IProgress<OperationProgress>? progress = null)
    {
        var startTime = DateTime.UtcNow;
        var result = new DependencyScanResult();

        try
        {
            List<DependencyInfo> allDeps;

            if (request.FullServerScan)
            {
                progress?.Report(new OperationProgress
                {
                    Status = OperationStatus.InProgress,
                    CurrentStep = "Scanning entire server for dependencies...",
                    Percentage = 10
                });

                allDeps = await _scanner.ScanServerDependenciesAsync();
            }
            else if (!string.IsNullOrEmpty(request.PackagePath))
            {
                progress?.Report(new OperationProgress
                {
                    Status = OperationStatus.InProgress,
                    CurrentStep = "Scanning package dependencies...",
                    Percentage = 10
                });

                allDeps = await _scanner.ScanPackageDependenciesAsync(request.PackagePath);
            }
            else if (request.SiteNames is not null && request.SiteNames.Count > 0)
            {
                allDeps = [];
                int i = 0;
                foreach (var siteName in request.SiteNames)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(new OperationProgress
                    {
                        Status = OperationStatus.InProgress,
                        CurrentStep = $"Scanning {siteName}...",
                        CompletedItems = i,
                        TotalItems = request.SiteNames.Count,
                        Percentage = (int)((double)i / request.SiteNames.Count * 100)
                    });

                    var siteDeps = await _scanner.ScanSiteDependenciesAsync(siteName);
                    allDeps.AddRange(siteDeps);
                    i++;
                }
            }
            else
            {
                allDeps = await _scanner.ScanServerDependenciesAsync();
            }

            result.FoundDependencies = allDeps
                .Where(d => !string.IsNullOrEmpty(d.Version))
                .ToList();

            result.MissingDependencies = allDeps
                .Where(d => d.Required && string.IsNullOrEmpty(d.Version))
                .ToList();

            result.VersionMismatches = [];
            result.ScanDuration = DateTime.UtcNow - startTime;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Dependency scan failed");
            var msg = ex.Message;
            if (ex.InnerException is not null)
                msg = $"{msg} | Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}";
            result.MissingDependencies.Add(new DependencyInfo
            {
                Name = ex.GetType().Name,
                Type = "ScanError",
                Version = msg,
                Required = true
            });
        }

        return result;
    }

    public async Task<List<DependencyInfo>> GetInstallationSuggestionsAsync(
        List<DependencyInfo> missingDependencies)
    {
        var suggestions = new List<DependencyInfo>();

        foreach (var missing in missingDependencies)
        {
            var suggestion = missing switch
            {
                { Name: "URL Rewrite Module" } => missing with
                {
                    DownloadUrl = "https://www.iis.net/downloads/microsoft/url-rewrite"
                },
                { Name: "ARR Module" } => missing with
                {
                    DownloadUrl = "https://www.iis.net/downloads/microsoft/application-request-routing"
                },
                { Name: "ASP.NET Core Hosting Bundle" } => missing with
                {
                    DownloadUrl = "https://dotnet.microsoft.com/download/dotnet"
                },
                { Name: var n } when n.Contains(".NET Runtime") => missing with
                {
                    DownloadUrl = "https://dotnet.microsoft.com/download/dotnet"
                },
                { Name: "VC++ Redistributable" } => missing with
                {
                    DownloadUrl = "https://aka.ms/vs/17/release/vc_redist.x64.exe"
                },
                _ => missing
            };

            suggestions.Add(suggestion);
        }

        await Task.CompletedTask;
        return suggestions;
    }
}
