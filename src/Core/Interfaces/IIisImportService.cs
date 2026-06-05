using IISDeploy.Core.Models;
using IISDeploy.Core.Models.Enums;

namespace IISDeploy.Core.Interfaces;

public interface IIisImportService
{
    Task<MigrationReport> ImportPackageAsync(
        string packagePath,
        Dictionary<string, ConflictResolutionStrategy>? conflictStrategies = null,
        Dictionary<string, string>? credentialOverrides = null,
        Dictionary<string, string>? sitePathOverrides = null,
        Dictionary<string, string>? customNames = null,
        Dictionary<string, int>? customPorts = null,
        bool dryRun = false,
        bool autoRemediate = false,
        CancellationToken cancellationToken = default,
        IProgress<OperationProgress>? progress = null);
}
