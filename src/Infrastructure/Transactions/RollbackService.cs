using System.Text.Json;
using IISDeploy.Core.Interfaces;
using IISDeploy.Core.Models;
using IISDeploy.Infrastructure.Transactions;

namespace IISDeploy.Infrastructure.Transactions;

public class RollbackService
{
    private readonly IIisDiscoveryService _discovery;
    private readonly ILoggingService _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public RollbackService(IIisDiscoveryService discovery, ILoggingService logger)
    {
        _discovery = discovery;
        _logger = logger;
    }

    public async Task<RollbackSnapshot> CreateSnapshotAsync()
    {
        var snapshot = new RollbackSnapshot
        {
            Timestamp = DateTime.UtcNow,
            SnapshotId = Guid.NewGuid().ToString("N")
        };

        try
        {
            var sites = await _discovery.GetAllSitesAsync();
            snapshot.SiteNames = sites.Select(s => s.Name).ToList();

            var pools = await _discovery.GetAllAppPoolsAsync();
            snapshot.PoolNames = pools.Select(p => p.Name).ToList();
        }
        catch (Exception ex)
        {
            _logger.Warning("Could not create full snapshot: {Message}", ex.Message);
        }

        return snapshot;
    }

    public async Task<RollbackResult> RollbackImportAsync(
        RollbackSnapshot snapshot,
        List<string> importedSiteNames,
        List<string> importedPoolNames)
    {
        var result = new RollbackResult();

        using var manager = new Microsoft.Web.Administration.ServerManager();

        // Remove imported sites
        foreach (var siteName in importedSiteNames)
        {
            try
            {
                var site = manager.Sites.FirstOrDefault(s =>
                    string.Equals(s.Name, siteName, StringComparison.OrdinalIgnoreCase));

                if (site is not null && !snapshot.SiteNames.Contains(siteName, StringComparer.OrdinalIgnoreCase))
                {
                    manager.Sites.Remove(site);
                    result.RolledBackSites.Add(siteName);
                    _logger.Information("Rolled back site: {Site}", siteName);
                }
            }
            catch (Exception ex)
            {
                result.FailedRollbacks.Add($"Site '{siteName}': {ex.Message}");
                _logger.Error(ex, "Failed to roll back site {Site}", siteName);
            }
        }

        // Remove imported pools
        foreach (var poolName in importedPoolNames)
        {
            try
            {
                var pool = manager.ApplicationPools.FirstOrDefault(p =>
                    string.Equals(p.Name, poolName, StringComparison.OrdinalIgnoreCase));

                if (pool is not null && !snapshot.PoolNames.Contains(poolName, StringComparer.OrdinalIgnoreCase))
                {
                    manager.ApplicationPools.Remove(pool);
                    result.RolledBackPools.Add(poolName);
                    _logger.Information("Rolled back app pool: {Pool}", poolName);
                }
            }
            catch (Exception ex)
            {
                result.FailedRollbacks.Add($"Pool '{poolName}': {ex.Message}");
                _logger.Error(ex, "Failed to roll back pool {Pool}", poolName);
            }
        }

        manager.CommitChanges();
        return result;
    }

    public async Task SaveSnapshotAsync(RollbackSnapshot snapshot, string path)
    {
        var json = JsonSerializer.Serialize(snapshot, _jsonOptions);
        await File.WriteAllTextAsync(path, json);
    }

    public async Task<RollbackSnapshot?> LoadSnapshotAsync(string path)
    {
        if (!File.Exists(path)) return null;

        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<RollbackSnapshot>(json, _jsonOptions);
    }
}

public class RollbackSnapshot
{
    public string SnapshotId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public List<string> SiteNames { get; set; } = [];
    public List<string> PoolNames { get; set; } = [];
}

public class RollbackResult
{
    public List<string> RolledBackSites { get; set; } = [];
    public List<string> RolledBackPools { get; set; } = [];
    public List<string> FailedRollbacks { get; set; } = [];
    public bool Success => FailedRollbacks.Count == 0;
}
