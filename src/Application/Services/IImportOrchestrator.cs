using IISDeploy.Application.DTOs;

namespace IISDeploy.Application.Services;

public interface IImportOrchestrator
{
    Task<ImportResult> ImportAsync(ImportRequest request, CancellationToken cancellationToken = default, IProgress<Core.Models.OperationProgress>? progress = null);
    Task<Core.Models.ValidationResult> ValidateImportPrerequisitesAsync(string packagePath, CancellationToken cancellationToken = default);
}
