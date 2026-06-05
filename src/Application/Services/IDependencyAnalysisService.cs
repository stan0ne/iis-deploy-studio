using IISDeploy.Application.DTOs;

namespace IISDeploy.Application.Services;

public interface IDependencyAnalysisService
{
    Task<DependencyScanResult> ScanAsync(DependencyScanRequest request, CancellationToken cancellationToken = default, IProgress<Core.Models.OperationProgress>? progress = null);
    Task<List<Core.Models.DependencyInfo>> GetInstallationSuggestionsAsync(List<Core.Models.DependencyInfo> missingDependencies);
}
