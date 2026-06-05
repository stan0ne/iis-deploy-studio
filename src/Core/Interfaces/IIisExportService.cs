using IISDeploy.Core.Models;
using IISDeploy.Core.Models.Enums;

namespace IISDeploy.Core.Interfaces;

public interface IIisExportService
{
    Task<string> ExportSitesAsync(
        IEnumerable<string> siteNames,
        string outputPath,
        ExportMode mode,
        bool includeCertificates = false,
        string? certificatePassword = null,
        bool includeNtfsPermissions = false,
        CancellationToken cancellationToken = default,
        IProgress<OperationProgress>? progress = null);
}
