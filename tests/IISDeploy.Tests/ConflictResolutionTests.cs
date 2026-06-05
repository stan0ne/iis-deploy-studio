using IISDeploy.Core.Interfaces;
using IISDeploy.Core.Models;
using IISDeploy.Core.Models.Enums;
using IISDeploy.Infrastructure.Iis;
using Moq;

namespace IISDeploy.Tests;

public class ConflictResolutionTests
{
    [Fact]
    public async Task AnalyzeConflicts_Should_Detect_Site_Name_Collision()
    {
        var mockDiscovery = new Mock<IIisDiscoveryService>();
        var mockBinding = new Mock<IBindingManagerService>();

        mockDiscovery.Setup(d => d.GetAllSitesAsync())
            .ReturnsAsync([
                new IisSite { Name = "ExistingSite", State = "Started" }
            ]);

        mockDiscovery.Setup(d => d.GetAllAppPoolsAsync())
            .ReturnsAsync([]);

        mockBinding.Setup(b => b.DetectConflictsAsync(It.IsAny<List<BindingInfo>>()))
            .ReturnsAsync([]);

        var service = new ConflictResolutionService(mockDiscovery.Object, mockBinding.Object);

        var sitesToImport = new List<IisSite>
        {
            new() { Name = "ExistingSite", State = "Started" }
        };

        var result = await service.AnalyzeImportConflictsAsync(sitesToImport, []);

        Assert.True(result.HasConflicts);
        Assert.Single(result.SiteConflicts);
        Assert.Equal("ExistingSite", result.SiteConflicts[0].ObjectName);
        Assert.Equal("NameCollision", result.SiteConflicts[0].ConflictType);
    }

    [Fact]
    public async Task AnalyzeConflicts_Should_Detect_AppPool_Collision()
    {
        var mockDiscovery = new Mock<IIisDiscoveryService>();
        var mockBinding = new Mock<IBindingManagerService>();

        mockDiscovery.Setup(d => d.GetAllSitesAsync())
            .ReturnsAsync([]);

        mockDiscovery.Setup(d => d.GetAllAppPoolsAsync())
            .ReturnsAsync([
                new IisApplicationPool { Name = "DefaultAppPool" }
            ]);

        mockBinding.Setup(b => b.DetectConflictsAsync(It.IsAny<List<BindingInfo>>()))
            .ReturnsAsync([]);

        var service = new ConflictResolutionService(mockDiscovery.Object, mockBinding.Object);

        var poolsToImport = new List<IisApplicationPool>
        {
            new() { Name = "DefaultAppPool" }
        };

        var result = await service.AnalyzeImportConflictsAsync([], poolsToImport);

        Assert.True(result.HasConflicts);
        Assert.Single(result.PoolConflicts);
        Assert.Equal("DefaultAppPool", result.PoolConflicts[0].ObjectName);
    }

    [Fact]
    public async Task AnalyzeConflicts_Should_Detect_Binding_Collision()
    {
        var mockDiscovery = new Mock<IIisDiscoveryService>();
        var mockBinding = new Mock<IBindingManagerService>();

        mockDiscovery.Setup(d => d.GetAllSitesAsync())
            .ReturnsAsync([]);
        mockDiscovery.Setup(d => d.GetAllAppPoolsAsync())
            .ReturnsAsync([]);

        mockBinding.Setup(b => b.DetectConflictsAsync(It.IsAny<List<BindingInfo>>()))
            .ReturnsAsync([
                new BindingInfo
                {
                    Protocol = "http",
                    BindingInformation = "*:80:",
                    Port = "80"
                }
            ]);

        var service = new ConflictResolutionService(mockDiscovery.Object, mockBinding.Object);

        var sitesToImport = new List<IisSite>
        {
            new()
            {
                Name = "NewSite",
                Bindings =
                [
                    new() { Protocol = "http", BindingInformation = "*:80:", Port = "80" }
                ]
            }
        };

        var result = await service.AnalyzeImportConflictsAsync(sitesToImport, []);

        Assert.True(result.HasConflicts);
        Assert.Single(result.BindingConflicts);
        Assert.Equal("PortCollision", result.BindingConflicts[0].ConflictType);
    }

    [Fact]
    public void GetEffectiveStrategy_Should_Use_User_Override()
    {
        var mockDiscovery = new Mock<IIisDiscoveryService>();
        var mockBinding = new Mock<IBindingManagerService>();
        var service = new ConflictResolutionService(mockDiscovery.Object, mockBinding.Object);

        var userStrategies = new Dictionary<string, ConflictResolutionStrategy>
        {
            ["site:TestSite"] = ConflictResolutionStrategy.Overwrite
        };

        var result = service.GetEffectiveStrategy("site:TestSite", userStrategies,
            ConflictResolutionStrategy.Skip);

        Assert.Equal(ConflictResolutionStrategy.Overwrite, result);
    }

    [Fact]
    public void GetEffectiveStrategy_Should_Use_Default_When_No_Override()
    {
        var mockDiscovery = new Mock<IIisDiscoveryService>();
        var mockBinding = new Mock<IBindingManagerService>();
        var service = new ConflictResolutionService(mockDiscovery.Object, mockBinding.Object);

        var result = service.GetEffectiveStrategy("site:Unknown", null,
            ConflictResolutionStrategy.Skip);

        Assert.Equal(ConflictResolutionStrategy.Skip, result);
    }
}
