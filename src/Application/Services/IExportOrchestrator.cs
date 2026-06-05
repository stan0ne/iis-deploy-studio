using IISDeploy.Application.DTOs;

namespace IISDeploy.Application.Services;

public interface IExportOrchestrator
{
    Task<ExportResult> ExportAsync(ExportRequest request, CancellationToken cancellationToken = default, IProgress<Core.Models.OperationProgress>? progress = null);
    Task ValidateExportPrerequisitesAsync(ExportRequest request, CancellationToken cancellationToken = default);
}
