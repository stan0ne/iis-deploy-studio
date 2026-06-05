using IISDeploy.Core.Models;

namespace IISDeploy.Infrastructure.Iis;

public static class AppPoolNameResolver
{
    public static void ApplyResolvedPoolNames(
        IisSite siteModel,
        IReadOnlyDictionary<string, string>? poolNameMap)
    {
        if (poolNameMap is null || poolNameMap.Count == 0)
            return;

        if (!string.IsNullOrWhiteSpace(siteModel.AppPoolName) &&
            poolNameMap.TryGetValue(siteModel.AppPoolName, out var resolvedRootPoolName))
        {
            siteModel.AppPoolName = resolvedRootPoolName;
        }

        foreach (var appModel in siteModel.Applications)
        {
            if (!string.IsNullOrWhiteSpace(appModel.ApplicationPoolName) &&
                poolNameMap.TryGetValue(appModel.ApplicationPoolName, out var resolvedAppPoolName))
            {
                appModel.ApplicationPoolName = resolvedAppPoolName;
            }
        }
    }
}
