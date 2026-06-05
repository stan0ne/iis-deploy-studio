using IISDeploy.Application.DTOs;
using IISDeploy.Application.Services;

namespace IISDeploy.Application.Implementations;

public class DashboardService : IDashboardService
{
    private readonly Core.Interfaces.IIisDiscoveryService _discoveryService;

    public DashboardService(Core.Interfaces.IIisDiscoveryService discoveryService)
    {
        _discoveryService = discoveryService;
    }

    public async Task<ServerSummary> GetServerSummaryAsync()
    {
        var serverInfo = await _discoveryService.GetServerInfoAsync();
        var sites = await _discoveryService.GetAllSitesAsync();
        var pools = await _discoveryService.GetAllAppPoolsAsync();

        return new ServerSummary
        {
            MachineName = Environment.MachineName,
            OsVersion = Environment.OSVersion.VersionString,
            IisVersion = serverInfo.IisVersion,
            TotalSites = sites.Count,
            RunningSites = sites.Count(s => s.State == "Started"),
            StoppedSites = sites.Count(s => s.State != "Started"),
            TotalAppPools = pools.Count,
            RunningAppPools = pools.Count(p => p.State == "Started"),
            PendingWarnings = 0,
            LastScan = DateTime.UtcNow
        };
    }

    public async Task<List<SiteTreeNode>> GetSiteTreeAsync(string? searchFilter = null)
    {
        var sites = await _discoveryService.GetAllSitesAsync();
        var filteredSites = string.IsNullOrWhiteSpace(searchFilter)
            ? sites
            : sites.Where(s => s.Name.Contains(searchFilter, StringComparison.OrdinalIgnoreCase)).ToList();

        var nodes = new List<SiteTreeNode>();
        foreach (var site in filteredSites)
        {
            var node = new SiteTreeNode
            {
                Id = site.Id.ToString(),
                Name = site.Name,
                Status = site.State,
                PhysicalPath = site.PhysicalPath,
                AppPoolName = site.AppPoolName,
                BindingCount = site.Bindings.Count,
                ChildCount = site.Applications.Count,
                Children = site.Applications.Select(app => new SiteTreeNode
                {
                    Id = $"{site.Id}_{app.Path}",
                    Name = app.Path,
                    Status = "Active",
                    AppPoolName = app.ApplicationPoolName,
                    PhysicalPath = app.PhysicalPath,
                    ChildCount = app.VirtualDirectories.Count,
                    Children = app.VirtualDirectories.Select(vd => new SiteTreeNode
                    {
                        Id = $"vdir_{site.Id}_{vd.Path}",
                        Name = vd.Path,
                        PhysicalPath = vd.PhysicalPath,
                        Status = "Active"
                    }).ToList()
                }).ToList()
            };

            nodes.Add(node);
        }

        return nodes;
    }

    public async Task<List<AppPoolSummary>> GetAppPoolSummariesAsync()
    {
        var pools = await _discoveryService.GetAllAppPoolsAsync();
        return pools.Select(p => new AppPoolSummary
        {
            Name = p.Name,
            State = p.State,
            PipelineMode = p.PipelineMode.ToString(),
            ManagedRuntimeVersion = p.ManagedRuntimeVersion,
            IdentityType = p.IdentityType.ToString(),
            ApplicationCount = 0
        }).ToList();
    }

    public async Task<string> GetSiteDetailAsync(string siteName)
    {
        var site = await _discoveryService.GetSiteAsync(siteName);
        return site is not null
            ? System.Text.Json.JsonSerializer.Serialize(site, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })
            : $"Site '{siteName}' not found.";
    }
}
