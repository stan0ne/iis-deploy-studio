using IISDeploy.Core.Models;

namespace IISDeploy.Core.Interfaces;

public interface IPowerShellExecutionService
{
    Task<string> ExecuteScriptAsync(string script, Dictionary<string,object>? parameters = null, CancellationToken cancellationToken = default);
    Task<bool> ExecuteCommandAsync(string command, CancellationToken cancellationToken = default);
    Task<string> GetExecutionPolicyAsync(CancellationToken cancellationToken = default);
}
