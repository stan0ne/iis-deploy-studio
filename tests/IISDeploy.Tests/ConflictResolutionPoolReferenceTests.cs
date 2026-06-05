using IISDeploy.Core.Interfaces;
using IISDeploy.Core.Models;
using IISDeploy.Infrastructure.Iis;
using Moq;

namespace IISDeploy.Tests;

public class ConflictResolutionPoolReferenceTests
{
    [Fact]
    public async Task AnalyzeConflicts_Should_Detect_Existing_Pool_References_From_Sites_Even_When_Pool_List_Is_Empty()
    {
        var mockDiscovery = new Mock<IIisDiscoveryService>();
        var mockBinding = new Mock<IBindingManagerService>();

        mockDiscovery.Setup(d => d.GetAllSitesAsync()).ReturnsAsync([]);
        mockDiscovery.Setup(d => d.GetAllAppPoolsAsync()).ReturnsAsync([
            new IisApplicationPool { Name = "PoolA" },
            new IisApplicationPool { Name = "PoolB" }
        ]);
        mockBinding.Setup(b => b.DetectConflictsAsync(It.IsAny<List<BindingInfo>>())).ReturnsAsync([]);

        var service = new ConflictResolutionService(mockDiscovery.Object, mockBinding.Object);

        var sites = new List<IisSite>
        {
            new() { Name = "SiteA", AppPoolName = "PoolA" },
            new() { Name = "SiteB", AppPoolName = "PoolB" }
        };

        var result = await service.AnalyzeImportConflictsAsync(sites, []);

        Assert.Equal(2, result.PoolConflicts.Count);
        Assert.Contains(result.PoolConflicts, c => c.ObjectName == "PoolA");
        Assert.Contains(result.PoolConflicts, c => c.ObjectName == "PoolB");
    }
}
