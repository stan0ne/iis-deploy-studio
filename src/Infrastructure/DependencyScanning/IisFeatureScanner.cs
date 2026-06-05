using System.ServiceProcess;
using IISDeploy.Core.Interfaces;
using IISDeploy.Core.Models;
using Microsoft.Web.Administration;

namespace IISDeploy.Infrastructure.DependencyScanning;

public class IisFeatureScanner
{
    private readonly IIisDiscoveryService _discovery;

    public IisFeatureScanner(IIisDiscoveryService discovery)
    {
        _discovery = discovery;
    }

    public async Task<List<DependencyInfo>> ScanIisFeaturesAsync()
    {
        var dependencies = new List<DependencyInfo>();

        try
        {
            using var manager = new ServerManager();

            // Check IIS modules
            AddModuleStatus(manager, dependencies, "RewriteModule", "URL Rewrite Module",
                "https://www.iis.net/downloads/microsoft/url-rewrite");
            AddModuleStatus(manager, dependencies, "ApplicationRequestRouting", "ARR Module",
                "https://www.iis.net/downloads/microsoft/application-request-routing");
            AddModuleStatus(manager, dependencies, "FastCgiModule", "FastCGI Module",
                null);
            AddModuleStatus(manager, dependencies, "WebSocketModule", "WebSocket Module",
                null);
            AddModuleStatus(manager, dependencies, "AspNetCoreModuleV2", "ASP.NET Core Module V2",
                "https://dotnet.microsoft.com/download/dotnet");
            AddModuleStatus(manager, dependencies, "DynamicCompressionModule", "Dynamic Compression",
                null);
            AddModuleStatus(manager, dependencies, "StaticCompressionModule", "Static Compression",
                null);
        }
        catch (UnauthorizedAccessException)
        {
            dependencies.Add(new DependencyInfo
            {
                Name = "IIS Module Scan",
                Type = "Scanner",
                Version = "Administrator privileges required for full IIS module scan.",
                Required = false
            });
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            dependencies.Add(new DependencyInfo
            {
                Name = "IIS Module Scan",
                Type = "Scanner",
                Version = $"IIS COM error: {ex.Message}",
                Required = false
            });
        }
        catch (Exception ex)
        {
            dependencies.Add(new DependencyInfo
            {
                Name = "IIS Module Scan",
                Type = "Scanner",
                Version = $"ServerManager access failed: {ex.Message}",
                Required = false
            });
        }

        // Check IIS services (doesn't require ServerManager)
        await CheckIisFeaturesAsync(dependencies);

        return dependencies;
    }

    private static void AddModuleStatus(
        ServerManager manager,
        List<DependencyInfo> dependencies,
        string moduleName,
        string displayName,
        string? downloadUrl)
    {
        try
        {
            bool found = false;

            try
            {
                var globalModulesProp = manager.GetType().GetProperty("GlobalModules");
                var globalModules = globalModulesProp?.GetValue(manager);

                if (globalModules is System.Collections.IEnumerable enumerable)
                {
                    foreach (var m in enumerable)
                    {
                        try
                        {
                            var name = m?.GetType().GetProperty("Name")?.GetValue(m)?.ToString();
                            if (string.Equals(name, moduleName, StringComparison.OrdinalIgnoreCase))
                            {
                                found = true;
                                break;
                            }
                        }
                        catch
                        {
                            // Skip inaccessible module entries
                        }
                    }
                }
            }
            catch
            {
                // Reflection-based module detection failed
            }

            dependencies.Add(new DependencyInfo
            {
                Name = displayName,
                Type = "IISModule",
                Version = found ? "Installed" : null,
                Required = moduleName != "WebSocketModule",
                DownloadUrl = downloadUrl
            });
        }
        catch (Exception ex)
        {
            dependencies.Add(new DependencyInfo
            {
                Name = displayName,
                Type = "IISModule",
                Version = $"Detection failed: {ex.Message}",
                Required = moduleName != "WebSocketModule",
                DownloadUrl = downloadUrl
            });
        }
    }

    private static async Task CheckIisFeaturesAsync(List<DependencyInfo> dependencies)
    {
        // Check IIS services
        await Task.Run(() =>
        {
            try
            {
                var services = new[] { "W3SVC", "WAS" };
                foreach (var svcName in services)
                {
                    using var sc = new ServiceController(svcName);
                    dependencies.Add(new DependencyInfo
                    {
                        Name = $"IIS Service: {svcName}",
                        Type = "WindowsService",
                        Version = sc.Status.ToString(),
                        Required = true
                    });
                }
            }
            catch
            {
                dependencies.Add(new DependencyInfo
                {
                    Name = "IIS Services",
                    Type = "WindowsService",
                    Version = "Not Found",
                    Required = true
                });
            }
        });

        await Task.CompletedTask;
    }
}
