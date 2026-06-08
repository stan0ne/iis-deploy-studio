using System.Diagnostics;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Security;
using System.Text;
using IISDeploy.Core.Interfaces;

namespace IISDeploy.Infrastructure.PowerShell;

public class PowerShellExecutionService : IPowerShellExecutionService
{
    private readonly ILoggingService _logger;
    private static readonly HashSet<string> _allowedCommands =
    [
        "Get-Command", "Get-Module", "Get-WindowsFeature", "Install-WindowsFeature",
        "Get-IISAppPool", "Get-IISSite", "New-IISSite", "Remove-IISSite",
        "Start-IISSite", "Stop-IISSite", "Get-WebConfiguration", "Set-WebConfiguration",
        "Get-ChildItem", "Test-Path", "Get-Item", "Get-ItemProperty",
        "Get-Service", "Start-Service", "Stop-Service", "Restart-Service",
        "Write-Output", "Write-Host", "Out-String", "Select-Object", "Where-Object",
        "Get-WebBinding", "New-WebBinding", "Remove-WebBinding",
        "Import-Module", "Get-IISServerManager", "Get-IISConfigSection",
        "appcmd", "msdeploy", "Get-ExecutionPolicy"
    ];

    public PowerShellExecutionService(ILoggingService logger)
    {
        _logger = logger;
    }

    public async Task<string> ExecuteScriptAsync(
        string script,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ValidateScript(script);

        return await Task.Run(() =>
        {
            // Bypass system ExecutionPolicy for this app's PowerShell instance.
            // ServerManager (Get-WindowsFeature) and other system modules
            // require at least RemoteSigned to load.
            var iss = InitialSessionState.CreateDefault();
            iss.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.RemoteSigned;
            using var ps = System.Management.Automation.PowerShell.Create(iss);
            ps.AddScript(script);

            if (parameters is not null)
            {
                foreach (var (key, value) in parameters)
                {
                    var safeKey = SanitizeParameterName(key);
                    ps.AddParameter(safeKey, value);
                }
            }

            ps.Streams.Error.DataAdded += (sender, args) =>
            {
                if (sender is PSDataCollection<ErrorRecord> errors)
                {
                    foreach (var error in errors)
                    {
                        _logger.Error("PowerShell Error: {Message}", error.Exception?.Message ?? error.ToString());
                    }
                }
            };

            var output = new StringBuilder();
            try
            {
                var results = ps.Invoke();

                foreach (var result in results)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        ps.Stop();
                        throw new OperationCanceledException();
                    }
                    output.AppendLine(result?.ToString() ?? string.Empty);
                }

                if (ps.HadErrors)
                    _logger.Warning("PowerShell script completed with errors");

                return output.ToString().TrimEnd();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.Error(ex, "PowerShell execution failed");
                throw;
            }
        }, cancellationToken);
    }

    public async Task<bool> ExecuteCommandAsync(
        string command,
        CancellationToken cancellationToken = default)
    {
        _logger.Debug("Executing PowerShell command: {Command}", command);

        try
        {
            var result = await ExecuteScriptAsync(command, null, cancellationToken);
            return !string.IsNullOrWhiteSpace(result);
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> GetExecutionPolicyAsync(CancellationToken cancellationToken = default)
    {
        return (await ExecuteScriptAsync("Get-ExecutionPolicy", null, cancellationToken)).Trim();
    }

    private void ValidateScript(string script)
    {
        if (string.IsNullOrWhiteSpace(script))
            throw new ArgumentException("Script cannot be empty.");

        // Detect dangerous patterns
        var dangerousPatterns = new[]
        {
            "Invoke-Expression", "iex ", "Invoke-Command",
            "New-Object System.Net.WebClient", "DownloadFile",
            "Start-Process", "Invoke-Item",
            "[System.Reflection.Assembly]::Load",
            "IEX(", "$()", "`$()",
            "Remove-Item -Recurse", "rm -rf"
        };

        foreach (var pattern in dangerousPatterns)
        {
            if (script.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                _logger.Warning("Blocked potentially dangerous PowerShell pattern: {Pattern}", pattern);
                throw new SecurityException($"PowerShell script contains forbidden pattern: {pattern}");
            }
        }

        // Validate only allowed cmdlets are used
        foreach (var line in script.Split('\n'))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
                continue;

            var firstWord = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (firstWord is not null && !firstWord.StartsWith('$') && !firstWord.StartsWith('.') &&
                !_allowedCommands.Any(c => firstWord.Equals(c, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.Warning("PowerShell command not in allowlist: {Command}", firstWord);
                throw new SecurityException(
                    $"PowerShell command '{firstWord}' is not in the allowed command list.");
            }
        }
    }

    private static string SanitizeParameterName(string name)
    {
        return new string(name.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
    }
}
