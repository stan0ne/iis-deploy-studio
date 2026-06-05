using System.Net;
using IISDeploy.Core.Interfaces;
using IISDeploy.Core.Models;

namespace IISDeploy.Infrastructure.Iis;

public class BindingManagerService : IBindingManagerService
{
    private readonly IIisDiscoveryService _discovery;

    public BindingManagerService(IIisDiscoveryService discovery)
    {
        _discovery = discovery;
    }

    public async Task<List<BindingInfo>> DetectConflictsAsync(List<BindingInfo> importedBindings)
    {
        var conflicts = new List<BindingInfo>();

        var allSites = await _discovery.GetAllSitesAsync();
        var existingBindings = allSites.SelectMany(s => s.Bindings).ToList();

        foreach (var imported in importedBindings)
        {
            var conflict = existingBindings.Any(existing =>
                string.Equals(existing.BindingInformation, imported.BindingInformation,
                    StringComparison.OrdinalIgnoreCase));

            if (conflict)
                conflicts.Add(imported);
        }

        return conflicts;
    }

    public Task<bool> IsPortAvailableAsync(string ipAddress, int port)
    {
        return Task.Run(() =>
        {
            try
            {
                var addr = string.IsNullOrEmpty(ipAddress) || ipAddress == "*"
                    ? IPAddress.Any
                    : IPAddress.Parse(ipAddress);

                using var listener = new System.Net.Sockets.TcpListener(addr, port);
                listener.Start();
                listener.Stop();
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    public Task<string> SuggestAlternativeBindingAsync(BindingInfo binding, List<string> existingBindingStrings)
    {
        var basePort = int.TryParse(binding.Port, out var p) ? p : 80;

        for (int offset = 1; offset < 1000; offset++)
        {
            var candidate = $"{binding.Protocol}://{binding.Host ?? "*"}:{basePort + offset}";
            if (!existingBindingStrings.Contains(candidate))
                return Task.FromResult(candidate);
        }

        return Task.FromResult($"{binding.Protocol}://{binding.Host ?? "*"}:{basePort + 9999}");
    }
}
