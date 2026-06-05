using IISDeploy.Application.Services;
using IISDeploy.Core.Interfaces;
using IISDeploy.Infrastructure.Iis;
using IISDeploy.Infrastructure.Transactions;
using Moq;

namespace IISDeploy.Tests;

public class IisImportChangeBindingTests
{
    [Fact]
    public void TryExtractPort_Parses_Star_Colon_Port_Format()
    {
        Assert.True(IisImportService.TryExtractPort("*:80:", out var port));
        Assert.Equal(80, port);
    }

    [Fact]
    public void TryExtractPort_Parses_With_Hostname()
    {
        Assert.True(IisImportService.TryExtractPort("*:443:example.com", out var port));
        Assert.Equal(443, port);
    }

    [Fact]
    public void TryExtractPort_Returns_False_For_Empty_String()
    {
        Assert.False(IisImportService.TryExtractPort("", out var port));
        Assert.Equal(0, port);
    }

    [Fact]
    public void TryExtractPort_Returns_False_For_Invalid_Format()
    {
        Assert.False(IisImportService.TryExtractPort("no_colon_here", out _));
    }

    [Fact]
    public async Task FindAlternativePort_Returns_First_Available_Port()
    {
        var mockLogger = new Mock<ILoggingService>();
        var mockBindingManager = new Mock<IBindingManagerService>();
        mockBindingManager
            .Setup(b => b.IsPortAvailableAsync("*", It.IsInRange(81, 200, Moq.Range.Inclusive)))
            .ReturnsAsync(false);
        mockBindingManager
            .Setup(b => b.IsPortAvailableAsync("*", 201))
            .ReturnsAsync(true);

        var service = BuildService(mockLogger, mockBindingManager);
        var result = await service.FindAlternativePort(80);

        Assert.Equal(201, result);
    }

    [Fact]
    public async Task FindAlternativePort_Returns_BasePlusOffset_When_None_Available()
    {
        var mockLogger = new Mock<ILoggingService>();
        var mockBindingManager = new Mock<IBindingManagerService>();
        mockBindingManager
            .Setup(b => b.IsPortAvailableAsync("*", It.IsAny<int>()))
            .ReturnsAsync(false);

        var service = BuildService(mockLogger, mockBindingManager);
        var result = await service.FindAlternativePort(80);

        Assert.Equal(10080, result);
    }

    private static IisImportService BuildService(
        Mock<ILoggingService> mockLogger, Mock<IBindingManagerService> mockBindingManager)
    {
        return new IisImportService(
            new Mock<IPackageBuilderService>().Object,
            new Mock<IIisDiscoveryService>().Object,
            new Mock<IConflictResolutionService>().Object,
            mockBindingManager.Object,
            mockLogger.Object,
            new TransactionManager(mockLogger.Object),
            new Mock<IReportGeneratorService>().Object);
    }
}
