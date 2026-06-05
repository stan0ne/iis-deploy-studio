using IISDeploy.Application.DTOs;

namespace IISDeploy.Application.Services;

public interface IDashboardService
{
    Task<ServerSummary> GetServerSummaryAsync();
    Task<List<SiteTreeNode>> GetSiteTreeAsync(string? searchFilter = null);
    Task<List<AppPoolSummary>> GetAppPoolSummariesAsync();
    Task<string> GetSiteDetailAsync(string siteName);
}
