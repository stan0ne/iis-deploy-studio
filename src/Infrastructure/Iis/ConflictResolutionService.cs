using IISDeploy.Application.Services;
using IISDeploy.Core.Interfaces;
using IISDeploy.Core.Models;
using IISDeploy.Core.Models.Enums;

namespace IISDeploy.Infrastructure.Iis;

public class ConflictResolutionService : IConflictResolutionService
{
    private readonly IIisDiscoveryService _discovery;
    private readonly IBindingManagerService _bindingManager;

    public ConflictResolutionService(
        IIisDiscoveryService discovery,
        IBindingManagerService bindingManager)
    {
        _discovery = discovery;
        _bindingManager = bindingManager;
    }

    public async Task<ConflictReport> AnalyzeImportConflictsAsync(
        List<IisSite> sitesToImport,
        List<IisApplicationPool> poolsToImport)
    {
        var report = new ConflictReport();

        var existingSites = await _discovery.GetAllSitesAsync();
        var existingPools = await _discovery.GetAllAppPoolsAsync();
        var existingSiteNames = existingSites.Select(s => s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingPoolNames = existingPools.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var site in sitesToImport)
        {
            if (existingSiteNames.Contains(site.Name))
            {
                report.SiteConflicts.Add(new ConflictEntry
                {
                    ObjectType = "Site",
                    ObjectName = site.Name,
                    ConflictType = "NameCollision",
                    Message = $"Site '{site.Name}' already exists.",
                    SuggestedResolution = ConflictResolutionStrategy.Overwrite
                });
            }
        }

        foreach (var pool in poolsToImport)
        {
            if (existingPoolNames.Contains(pool.Name))
            {
                report.PoolConflicts.Add(new ConflictEntry
                {
                    ObjectType = "AppPool",
                    ObjectName = pool.Name,
                    ConflictType = "NameCollision",
                    Message = $"Application pool '{pool.Name}' already exists.",
                    SuggestedResolution = ConflictResolutionStrategy.Overwrite
                });
            }
        }

        var allImportedBindings = sitesToImport.SelectMany(s => s.Bindings).ToList();
        var bindingConflicts = await _bindingManager.DetectConflictsAsync(allImportedBindings);

        foreach (var binding in bindingConflicts)
        {
            report.BindingConflicts.Add(new ConflictEntry
            {
                ObjectType = "Binding",
                ObjectName = binding.BindingInformation,
                ConflictType = "PortCollision",
                Message = $"Binding '{binding.BindingInformation}' is already in use.",
                SuggestedResolution = ConflictResolutionStrategy.ChangeBinding
            });
        }

        return report;
    }

    public ConflictResolutionStrategy GetEffectiveStrategy(
        string objectKey,
        Dictionary<string, ConflictResolutionStrategy>? userStrategies,
        ConflictResolutionStrategy defaultStrategy)
    {
        if (userStrategies is not null && userStrategies.TryGetValue(objectKey, out var strategy))
            return strategy;

        return defaultStrategy;
    }
}
