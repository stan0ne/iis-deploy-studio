using IISDeploy.Core.Models;
using Microsoft.Win32;

namespace IISDeploy.Infrastructure.DependencyScanning;

public class ThirdPartyScanner
{
    public Task<List<DependencyInfo>> ScanAsync()
    {
        var deps = new List<DependencyInfo>();

        ScanPhp(deps);
        ScanNodeJs(deps);
        ScanJava(deps);
        ScanOdbc(deps);

        return Task.FromResult(deps);
    }

    private static void ScanPhp(List<DependencyInfo> deps)
    {
        var phpPaths = new[]
        {
            @"C:\Program Files\PHP",
            @"C:\PHP",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PHP")
        };

        foreach (var path in phpPaths)
        {
            if (Directory.Exists(path))
            {
                var phpExe = Path.Combine(path, "php.exe");
                if (File.Exists(phpExe))
                {
                    deps.Add(new DependencyInfo
                    {
                        Name = "PHP",
                        Type = "Runtime",
                        Version = "Installed",
                        Required = false,
                        InstallPath = path
                    });
                    return;
                }
            }
        }

        // Check for PHP in PATH
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var segment in pathEnv.Split(';'))
        {
            var phpExe = Path.Combine(segment.Trim(), "php.exe");
            if (File.Exists(phpExe))
            {
                deps.Add(new DependencyInfo
                {
                    Name = "PHP",
                    Type = "Runtime",
                    Version = "Installed (PATH)",
                    Required = false,
                    InstallPath = Path.GetDirectoryName(phpExe)
                });
                return;
            }
        }
    }

    private static void ScanNodeJs(List<DependencyInfo> deps)
    {
        var nodePaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "nodejs")
        };

        foreach (var path in nodePaths)
        {
            var nodeExe = Path.Combine(path, "node.exe");
            if (File.Exists(nodeExe))
            {
                deps.Add(new DependencyInfo
                {
                    Name = "Node.js",
                    Type = "Runtime",
                    Version = "Installed",
                    Required = false,
                    InstallPath = path
                });
                return;
            }
        }

        using var key = Registry.LocalMachine.OpenSubKey(
            @"SOFTWARE\Node.js");

        if (key is not null)
        {
            var installPath = key.GetValue("InstallPath")?.ToString();
            deps.Add(new DependencyInfo
            {
                Name = "Node.js",
                Type = "Runtime",
                Version = "Installed",
                Required = false,
                InstallPath = installPath
            });
        }
    }

    private static void ScanJava(List<DependencyInfo> deps)
    {
        var javaPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Java"),
            @"C:\Program Files\Java"
        };

        foreach (var basePath in javaPaths)
        {
            if (!Directory.Exists(basePath)) continue;

            foreach (var dir in Directory.GetDirectories(basePath))
            {
                var javaExe = Path.Combine(dir, "bin", "java.exe");
                if (File.Exists(javaExe))
                {
                    var dirName = Path.GetFileName(dir);
                    deps.Add(new DependencyInfo
                    {
                        Name = "Java",
                        Type = "Runtime",
                        Version = dirName,
                        Required = false,
                        InstallPath = dir
                    });
                    return;
                }
            }
        }

        // Check JAVA_HOME
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrEmpty(javaHome) && Directory.Exists(javaHome))
        {
            deps.Add(new DependencyInfo
            {
                Name = "Java (JAVA_HOME)",
                Type = "Runtime",
                Version = "Installed",
                Required = false,
                InstallPath = javaHome
            });
        }
    }

    private static void ScanOdbc(List<DependencyInfo> deps)
    {
        var odbcKeys = new[]
        {
            @"SOFTWARE\ODBC\ODBC.INI\ODBC Data Sources",
            @"SOFTWARE\WOW6432Node\ODBC\ODBC.INI\ODBC Data Sources"
        };

        foreach (var keyPath in odbcKeys)
        {
            using var key = Registry.LocalMachine.OpenSubKey(keyPath);
            if (key is null) continue;

            foreach (var valueName in key.GetValueNames())
            {
                deps.Add(new DependencyInfo
                {
                    Name = $"ODBC DSN: {valueName}",
                    Type = "ODBC",
                    Version = key.GetValue(valueName)?.ToString(),
                    Required = false
                });
            }
        }
    }
}
