using IISDeploy.Core.Models;

namespace IISDeploy.Core.Interfaces;

public interface IIisDiscoveryService
{
    Task<IisServerInfo> GetServerInfoAsync();
    Task<List<IisSite>> GetAllSitesAsync();
    Task<IisSite?> GetSiteAsync(string name);
    Task<List<IisApplicationPool>> GetAllAppPoolsAsync();
    Task<IisApplicationPool?> GetAppPoolAsync(string name);
    Task<List<BindingInfo>> GetSiteBindingsAsync(string siteName);
    Task<List<IisApplication>> GetApplicationsAsync(string siteName);
    Task<List<IisVirtualDirectory>> GetVirtualDirectoriesAsync(string siteName, string? applicationPath = null);
}
