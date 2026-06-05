using IISDeploy.Core.Models;
using IISDeploy.Core.Models.Enums;

namespace IISDeploy.Application.Services;

public interface IConflictResolutionService
{
    Task<ConflictReport> AnalyzeImportConflictsAsync(
        List<IisSite> sitesToImport,
        List<IisApplicationPool> poolsToImport);

    ConflictResolutionStrategy GetEffectiveStrategy(
        string objectKey,
        Dictionary<string, ConflictResolutionStrategy>? userStrategies,
        ConflictResolutionStrategy defaultStrategy);
}
