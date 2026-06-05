using IISDeploy.Core.Models;
using IISDeploy.Infrastructure.Iis;

namespace IISDeploy.Tests;

public class AppPoolNameResolverTests
{
    [Fact]
    public void ApplyResolvedPoolNames_Should_Replace_Original_Pool_Names_Throughout_Site()
    {
        var site = new IisSite
        {
            Name = "SiteA",
            AppPoolName = "OldPool",
            Applications =
            [
                new IisApplication
                {
                    Path = "/",
                    ApplicationPoolName = "OldPool"
                },
                new IisApplication
                {
                    Path = "/api",
                    ApplicationPoolName = "OtherPool"
                }
            ]
        };

        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["OldPool"] = "OldPool_Imported_20240101",
            ["OtherPool"] = "OtherPool_Imported_20240101"
        };

        AppPoolNameResolver.ApplyResolvedPoolNames(site, mappings);

        Assert.Equal("OldPool_Imported_20240101", site.AppPoolName);
        Assert.Equal("OldPool_Imported_20240101", site.Applications[0].ApplicationPoolName);
        Assert.Equal("OtherPool_Imported_20240101", site.Applications[1].ApplicationPoolName);
    }
}
