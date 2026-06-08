using System.ServiceProcess;
using IISDeploy.Core.Interfaces;
using IISDeploy.Core.Models;
using Microsoft.Web.Administration;

namespace IISDeploy.Infrastructure.DependencyScanning;

public class IisFeatureScanner
{
    private readonly IIisDiscoveryService _discovery;
    private readonly IPowerShellExecutionService _powerShell;
    private readonly ILoggingService _logger;

    public IisFeatureScanner(
        IIisDiscoveryService discovery,
        IPowerShellExecutionService powerShell,
        ILoggingService logger)
    {
        _discovery = discovery;
        _powerShell = powerShell;
        _logger = logger;
    }

    public async Task<List<DependencyInfo>> ScanIisFeaturesAsync()
    {
        var dependencies = new List<DependencyInfo>();

        await CheckPowerShellExecutionPolicyAsync(dependencies, CancellationToken.None);

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

        await CheckIisFeaturesViaPowerShellAsync(dependencies, CancellationToken.None);

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

    private async Task CheckIisFeaturesViaPowerShellAsync(
        List<DependencyInfo> dependencies,
        CancellationToken cancellationToken)
    {
        const string script = @"
Import-Module ServerManager -ErrorAction Stop
$features = @('Web-Server', 'Web-Common-Http', 'Web-Asp-Net45', 'Web-Scripting-Tools', 'Web-ISAPI-Ext', 'Web-ISAPI-Filter')
Get-WindowsFeature -Name $features -ErrorAction SilentlyContinue | Select-Object Name, InstallState
";

        try
        {
            var output = await _powerShell.ExecuteScriptAsync(script, parameters: null, cancellationToken);
            _logger.Information("PowerShell Get-WindowsFeature output: {Output}", output);

            dependencies.Add(new DependencyInfo
            {
                Name = "IIS OS Features (PowerShell)",
                Type = "PowerShellFeatureScan",
                Version = string.IsNullOrWhiteSpace(output) ? "Empty" : "OK",
                Required = true
            });
        }
        catch (Exception ex)
        {
            _logger.Warning("PowerShell Get-WindowsFeature failed: {Error}", ex.Message);
            dependencies.Add(new DependencyInfo
            {
                Name = "IIS OS Features (PowerShell)",
                Type = "PowerShellFeatureScan",
                Version = $"Failed: {ex.Message}",
                Required = false
            });
        }
    }

    private async Task CheckPowerShellExecutionPolicyAsync(
        List<DependencyInfo> dependencies,
        CancellationToken cancellationToken)
    {
        try
        {
            var policy = await _powerShell.GetExecutionPolicyAsync(cancellationToken);
            _logger.Information("PowerShell execution policy: {Policy}", policy);

            var restrictive = string.Equals(policy, "Restricted", StringComparison.OrdinalIgnoreCase)
                           || string.Equals(policy, "AllSigned", StringComparison.OrdinalIgnoreCase);

            dependencies.Add(new DependencyInfo
            {
                Name = "PowerShell Execution Policy",
                Type = "PowerShellPolicy",
                Version = policy,
                Required = !restrictive
            });
        }
        catch (Exception ex)
        {
            _logger.Warning("Failed to query PowerShell execution policy: {Error}", ex.Message);
            dependencies.Add(new DependencyInfo
            {
                Name = "PowerShell Execution Policy",
                Type = "PowerShellPolicy",
                Version = $"Check failed: {ex.Message}",
                Required = false
            });
        }
    }

    public async Task<Dictionary<string, bool>> RemediateIisFeaturesAsync(
        List<string> featureNames,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, bool>();
        if (featureNames is null || featureNames.Count == 0)
            return result;

        foreach (var name in featureNames)
        {
            var safeName = name.Replace("'", "''");
            var script = $"Install-WindowsFeature -Name '{safeName}' -ErrorAction Stop";

            try
            {
                var output = await _powerShell.ExecuteScriptAsync(script, parameters: null, cancellationToken);
                _logger.Information("Remediated feature {Feature}: {Output}", name, output.Trim());
                result[name] = true;
            }
            catch (Exception ex)
            {
                _logger.Warning("Failed to remediate feature {Feature}: {Error}", name, ex.Message);
                result[name] = false;
            }
        }

        return result;
    }
}
