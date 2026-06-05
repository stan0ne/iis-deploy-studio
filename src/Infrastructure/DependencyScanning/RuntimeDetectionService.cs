using IISDeploy.Core.Models;
using Microsoft.Win32;

namespace IISDeploy.Infrastructure.DependencyScanning;

public class RuntimeDetectionService
{
    public Task<List<DependencyInfo>> ScanRuntimesAsync()
    {
        var dependencies = new List<DependencyInfo>();

        // .NET runtimes
        AddDotNetRuntimes(dependencies);

        // .NET Core / ASP.NET Core runtimes
        AddAspNetCoreRuntimes(dependencies);

        // VC++ Redistributables
        AddVcRedists(dependencies);

        // Windows version info
        dependencies.Add(new DependencyInfo
        {
            Name = "Windows",
            Type = "OperatingSystem",
            Version = Environment.OSVersion.VersionString,
            Required = true,
            InstallPath = Environment.SystemDirectory
        });

        return Task.FromResult(dependencies);
    }

    private static void AddDotNetRuntimes(List<DependencyInfo> deps)
    {
        var dotnetPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "dotnet");

        if (Directory.Exists(dotnetPath))
        {
            var sharedPath = Path.Combine(dotnetPath, "shared", "Microsoft.NETCore.App");
            if (Directory.Exists(sharedPath))
            {
                var versions = Directory.GetDirectories(sharedPath)
                    .Select(Path.GetFileName)
                    .OrderDescending()
                    .ToList();

                foreach (var version in versions)
                {
                    deps.Add(new DependencyInfo
                    {
                        Name = ".NET Runtime",
                        Type = "Runtime",
                        Version = version,
                        Required = true,
                        InstallPath = Path.Combine(sharedPath, version!)
                    });
                }
            }
        }

        // Classic .NET Framework
        var fxPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "Microsoft.NET", "Framework");

        if (Directory.Exists(fxPath))
        {
            foreach (var dir in Directory.GetDirectories(fxPath))
            {
                var dirName = Path.GetFileName(dir);
                if (dirName.StartsWith("v4.") && File.Exists(Path.Combine(dir, "clr.dll")))
                {
                    deps.Add(new DependencyInfo
                    {
                        Name = ".NET Framework",
                        Type = "Runtime",
                        Version = dirName,
                        Required = false,
                        InstallPath = dir
                    });
                }
            }
        }
    }

    private static void AddAspNetCoreRuntimes(List<DependencyInfo> deps)
    {
        var hostingBundles = new[]
        {
            @"SOFTWARE\Microsoft\ASP.NET Core\Shared Framework",
            @"SOFTWARE\WOW6432Node\Microsoft\ASP.NET Core\Shared Framework"
        };

        foreach (var keyPath in hostingBundles)
        {
            using var key = Registry.LocalMachine.OpenSubKey(keyPath);
            if (key is null) continue;

            foreach (var subKeyName in key.GetSubKeyNames())
            {
                using var subKey = key.OpenSubKey(subKeyName);
                var version = subKey?.GetValue("Version")?.ToString();
                if (!string.IsNullOrEmpty(version))
                {
                    deps.Add(new DependencyInfo
                    {
                        Name = "ASP.NET Core Runtime",
                        Type = "Runtime",
                        Version = version,
                        Required = true
                    });
                }
            }
        }

        // Check hosting bundle
        var hostingKey = Registry.LocalMachine.OpenSubKey(
            @"SOFTWARE\Microsoft\IIS Extensions\AspNetCoreModuleV2");

        if (hostingKey is not null)
        {
            var hostingVersion = hostingKey.GetValue("Version")?.ToString();
            deps.Add(new DependencyInfo
            {
                Name = "ASP.NET Core Hosting Bundle",
                Type = "HostingBundle",
                Version = hostingVersion ?? "installed",
                Required = true,
                InstallPath = hostingKey.GetValue("InstallPath")?.ToString()
            });
        }
    }

    private static void AddVcRedists(List<DependencyInfo> deps)
    {
        var vcKeyPath = @"SOFTWARE\Microsoft\VisualStudio";
        using var key = Registry.LocalMachine.OpenSubKey(vcKeyPath);
        if (key is null)
        {
            using var wowKey = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\WOW6432Node\Microsoft\VisualStudio");
            if (wowKey is null) return;

            foreach (var subKeyName in wowKey.GetSubKeyNames())
            {
                if (subKeyName.StartsWith("14.") || subKeyName.StartsWith("17."))
                {
                    var vcVersion = subKeyName;
                    using var vcKey = wowKey.OpenSubKey(vcVersion + @"\VC\Runtimes\x64");
                    var installed = vcKey?.GetValue("Installed")?.ToString();

                    if (installed == "1" || vcKey is not null)
                    {
                        deps.Add(new DependencyInfo
                        {
                            Name = "VC++ Redistributable",
                            Type = "Runtime",
                            Version = vcVersion,
                            Required = true
                        });
                    }
                }
            }
        }
    }
}
