using IISDeploy.Core.Interfaces;
using IISDeploy.Core.Models;
using IISDeploy.Core.Models.Enums;
using Microsoft.Web.Administration;

namespace IISDeploy.Infrastructure.Iis;

public class IisDiscoveryService : IIisDiscoveryService
{
    public Task<IisServerInfo> GetServerInfoAsync()
    {
        using var manager = new ServerManager();
        var info = new IisServerInfo
        {
            IisVersion = manager.GetType().Assembly.GetName().Version?.ToString() ?? "Unknown",
            SiteCount = manager.Sites.Count,
            AppPoolCount = manager.ApplicationPools.Count,
            InstalledFeatures = []
        };

        return Task.FromResult(info);
    }

    public Task<List<IisSite>> GetAllSitesAsync()
    {
        using var manager = new ServerManager();
        var sites = manager.Sites.Select(MapSite).ToList();
        return Task.FromResult(sites);
    }

    public Task<IisSite?> GetSiteAsync(string name)
    {
        using var manager = new ServerManager();
        var site = manager.Sites.FirstOrDefault(s =>
            string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(site is not null ? MapSite(site) : null);
    }

    public Task<List<IisApplicationPool>> GetAllAppPoolsAsync()
    {
        using var manager = new ServerManager();
        var pools = manager.ApplicationPools.Select(MapAppPool).ToList();
        return Task.FromResult(pools);
    }

    public Task<IisApplicationPool?> GetAppPoolAsync(string name)
    {
        using var manager = new ServerManager();
        var pool = manager.ApplicationPools.FirstOrDefault(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(pool is not null ? MapAppPool(pool) : null);
    }

    public Task<List<BindingInfo>> GetSiteBindingsAsync(string siteName)
    {
        using var manager = new ServerManager();
        var site = manager.Sites.FirstOrDefault(s =>
            string.Equals(s.Name, siteName, StringComparison.OrdinalIgnoreCase));

        if (site is null) return Task.FromResult(new List<BindingInfo>());

        var bindings = site.Bindings.Select(MapBinding).ToList();
        return Task.FromResult(bindings);
    }

    public Task<List<IisApplication>> GetApplicationsAsync(string siteName)
    {
        using var manager = new ServerManager();
        var site = manager.Sites.FirstOrDefault(s =>
            string.Equals(s.Name, siteName, StringComparison.OrdinalIgnoreCase));

        if (site is null) return Task.FromResult(new List<IisApplication>());

        var apps = site.Applications
            .Where(a => a.Path != "/")
            .Select(MapApplication)
            .ToList();

        return Task.FromResult(apps);
    }

    public Task<List<IisVirtualDirectory>> GetVirtualDirectoriesAsync(string siteName, string? applicationPath = null)
    {
        using var manager = new ServerManager();
        var site = manager.Sites.FirstOrDefault(s =>
            string.Equals(s.Name, siteName, StringComparison.OrdinalIgnoreCase));

        if (site is null) return Task.FromResult(new List<IisVirtualDirectory>());

        var targetPath = string.IsNullOrEmpty(applicationPath) ? "/" : applicationPath;
        var targetApp = site.Applications.FirstOrDefault(a => a.Path == targetPath);

        if (targetApp is null) return Task.FromResult(new List<IisVirtualDirectory>());

        var vdirs = targetApp.VirtualDirectories
            .Where(v => v.Path != "/")
            .Select(v => new IisVirtualDirectory
            {
                Path = v.Path,
                PhysicalPath = v.PhysicalPath,
                UserName = v.UserName,
                LogonMethod = v.LogonMethod.ToString()
            }).ToList();

        return Task.FromResult(vdirs);
    }

    private static BindingInfo MapBinding(Microsoft.Web.Administration.Binding binding)
    {
        var sslFlags = TryGetAttribute(binding, "sslFlags", 0);
        var sniEnabled = (sslFlags & 1) == 1;

        return new BindingInfo
        {
            Protocol = binding.Protocol,
            BindingInformation = binding.BindingInformation,
            Host = binding.Host,
            IpAddress = binding.EndPoint?.Address?.ToString(),
            Port = binding.EndPoint?.Port.ToString() ?? "80",
            CertificateStoreName = binding.CertificateStoreName,
            CertificateHash = binding.CertificateHash is not null
                ? BitConverter.ToString(binding.CertificateHash).Replace("-", "")
                : null,
            RequireServerNameIndication = sniEnabled,
            SslFlags = sslFlags > 0 ? sslFlags.ToString() : null
        };
    }

    private static IisSite MapSite(Microsoft.Web.Administration.Site site)
    {
        var rootApp = site.Applications.FirstOrDefault(a => a.Path == "/");
        var rootVdir = rootApp?.VirtualDirectories.FirstOrDefault(v => v.Path == "/");

        return new IisSite
        {
            Id = site.Id,
            Name = site.Name,
            PhysicalPath = rootVdir?.PhysicalPath ?? string.Empty,
            State = site.State.ToString(),
            ServerAutoStart = site.ServerAutoStart,
            Bindings = site.Bindings.Select(MapBinding).ToList(),
            AppPoolName = rootApp?.ApplicationPoolName,
            Applications = [],
            VirtualDirectories = [],
            PreloadEnabled = TryGetAttribute(site, "preloadEnabled", false)
        };
    }

    private static IisApplication MapApplication(Microsoft.Web.Administration.Application app)
    {
        return new IisApplication
        {
            Path = app.Path,
            PhysicalPath = app.VirtualDirectories
                .FirstOrDefault(v => v.Path == "/")?.PhysicalPath ?? string.Empty,
            ApplicationPoolName = app.ApplicationPoolName,
            PreloadEnabled = TryGetAttribute(app, "preloadEnabled", false),
            ServiceAutoStartEnabled = TryGetAttribute(app, "serviceAutoStartEnabled", false)
        };
    }

    private static IisApplicationPool MapAppPool(Microsoft.Web.Administration.ApplicationPool pool)
    {
        return new IisApplicationPool
        {
            Name = pool.Name,
            State = pool.State.ToString(),
            PipelineMode = pool.ManagedPipelineMode == ManagedPipelineMode.Integrated
                ? PipelineMode.Integrated
                : PipelineMode.Classic,
            ManagedRuntimeVersion = pool.ManagedRuntimeVersion?.ToString() ?? string.Empty,
            Enable32BitAppOnWin64 = pool.Enable32BitAppOnWin64,
            IdentityType = MapIdentityType(pool.ProcessModel.IdentityType),
            CustomUserName = pool.ProcessModel.UserName,
            IdleTimeoutMinutes = SafeCastToInt(pool.ProcessModel.IdleTimeout.TotalMinutes),
            AutoStart = pool.AutoStart,
            MaxProcesses = SafeCastToInt(pool.ProcessModel.MaxProcesses),
            PrivateMemoryLimit = pool.Recycling.PeriodicRestart.PrivateMemory,
            VirtualMemoryLimit = pool.Recycling.PeriodicRestart.Memory,
            ProcessModelLoadUserProfile = pool.ProcessModel.LoadUserProfile.ToString(),
            RegularTimeInterval = SafeCastToInt(pool.Recycling.PeriodicRestart.Time.TotalMinutes)
        };
    }

    private static AppPoolIdentityType MapIdentityType(ProcessModelIdentityType identityType)
    {
        return identityType switch
        {
            ProcessModelIdentityType.LocalSystem => AppPoolIdentityType.LocalSystem,
            ProcessModelIdentityType.LocalService => AppPoolIdentityType.LocalService,
            ProcessModelIdentityType.NetworkService => AppPoolIdentityType.NetworkService,
            ProcessModelIdentityType.ApplicationPoolIdentity => AppPoolIdentityType.ApplicationPoolIdentity,
            ProcessModelIdentityType.SpecificUser => AppPoolIdentityType.SpecificUser,
            _ => AppPoolIdentityType.ApplicationPoolIdentity
        };
    }

    private static T TryGetAttribute<T>(Microsoft.Web.Administration.ConfigurationElement element, string attributeName, T defaultValue)
    {
        try
        {
            var attr = element.Attributes[attributeName];
            if (attr is not null)
            {
                var value = attr.Value;
                if (value is T typedValue)
                    return typedValue;
                if (value is not null)
                    return (T)Convert.ChangeType(value, typeof(T));
            }
        }
        catch
        {
            // Silently fall back to default
        }

        return defaultValue;
    }

    private static int SafeCastToInt(long value)
    {
        if (value > int.MaxValue) return int.MaxValue;
        if (value < 0) return 0;
        return (int)value;
    }

    private static int SafeCastToInt(double value)
    {
        if (value > int.MaxValue) return int.MaxValue;
        if (value < 0) return 0;
        return (int)value;
    }
}
