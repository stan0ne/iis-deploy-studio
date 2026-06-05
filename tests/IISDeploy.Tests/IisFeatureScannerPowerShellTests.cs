using IISDeploy.Core.Interfaces;
using IISDeploy.Infrastructure.DependencyScanning;
using Moq;

namespace IISDeploy.Tests;

public class IisFeatureScannerPowerShellTests
{
    [Fact]
    public async Task ScanIisFeaturesAsync_Should_Invoke_PowerShell_And_Report_OK_When_Output_NonEmpty()
    {
        var mockDiscovery = new Mock<IIisDiscoveryService>();
        var mockPowerShell = new Mock<IPowerShellExecutionService>();
        var mockLogger = new Mock<ILoggingService>();

        mockPowerShell
            .Setup(p => p.ExecuteScriptAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Web-Server Installed\nWeb-Common-Http Installed\n");

        var scanner = new IisFeatureScanner(
            mockDiscovery.Object, mockPowerShell.Object, mockLogger.Object);

        var result = await scanner.ScanIisFeaturesAsync();

        var psEntry = result.SingleOrDefault(d => d.Type == "PowerShellFeatureScan");
        Assert.NotNull(psEntry);
        Assert.Equal("OK", psEntry.Version);
        Assert.True(psEntry.Required);

        mockPowerShell.Verify(
            p => p.ExecuteScriptAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ScanIisFeaturesAsync_Should_Report_Empty_When_PowerShell_Returns_Blank()
    {
        var mockDiscovery = new Mock<IIisDiscoveryService>();
        var mockPowerShell = new Mock<IPowerShellExecutionService>();
        var mockLogger = new Mock<ILoggingService>();

        mockPowerShell
            .Setup(p => p.ExecuteScriptAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);

        var scanner = new IisFeatureScanner(
            mockDiscovery.Object, mockPowerShell.Object, mockLogger.Object);

        var result = await scanner.ScanIisFeaturesAsync();

        var psEntry = result.SingleOrDefault(d => d.Type == "PowerShellFeatureScan");
        Assert.NotNull(psEntry);
        Assert.Equal("Empty", psEntry.Version);
    }

    [Fact]
    public async Task ScanIisFeaturesAsync_Should_Report_Failed_And_Log_Warning_When_PowerShell_Throws()
    {
        var mockDiscovery = new Mock<IIisDiscoveryService>();
        var mockPowerShell = new Mock<IPowerShellExecutionService>();
        var mockLogger = new Mock<ILoggingService>();

        mockPowerShell
            .Setup(p => p.ExecuteScriptAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Security.SecurityException("cmdlet not in allowlist"));

        var scanner = new IisFeatureScanner(
            mockDiscovery.Object, mockPowerShell.Object, mockLogger.Object);

        var result = await scanner.ScanIisFeaturesAsync();

        var psEntry = result.SingleOrDefault(d => d.Type == "PowerShellFeatureScan");
        Assert.NotNull(psEntry);
        Assert.StartsWith("Failed:", psEntry.Version);
        Assert.False(psEntry.Required);
    }

    [Fact]
    public async Task ScanIisFeaturesAsync_Should_Report_Execution_Policy_When_PowerShell_Returns_It()
    {
        var mockDiscovery = new Mock<IIisDiscoveryService>();
        var mockPowerShell = new Mock<IPowerShellExecutionService>();
        var mockLogger = new Mock<ILoggingService>();

        mockPowerShell
            .Setup(p => p.GetExecutionPolicyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("RemoteSigned");
        mockPowerShell
            .Setup(p => p.ExecuteScriptAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Web-Server Installed");

        var scanner = new IisFeatureScanner(
            mockDiscovery.Object, mockPowerShell.Object, mockLogger.Object);

        var result = await scanner.ScanIisFeaturesAsync();

        var policyEntry = result.SingleOrDefault(d => d.Type == "PowerShellPolicy");
        Assert.NotNull(policyEntry);
        Assert.Equal("RemoteSigned", policyEntry.Version);
        Assert.True(policyEntry.Required);

        mockPowerShell.Verify(
            p => p.GetExecutionPolicyAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("Restricted")]
    [InlineData("AllSigned")]
    [InlineData("restricted")]
    public async Task ScanIisFeaturesAsync_Should_Mark_Restrictive_Policy_As_Not_Required(string policy)
    {
        var mockDiscovery = new Mock<IIisDiscoveryService>();
        var mockPowerShell = new Mock<IPowerShellExecutionService>();
        var mockLogger = new Mock<ILoggingService>();

        mockPowerShell
            .Setup(p => p.GetExecutionPolicyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);

        var scanner = new IisFeatureScanner(
            mockDiscovery.Object, mockPowerShell.Object, mockLogger.Object);

        var result = await scanner.ScanIisFeaturesAsync();

        var policyEntry = result.SingleOrDefault(d => d.Type == "PowerShellPolicy");
        Assert.NotNull(policyEntry);
        Assert.Equal(policy, policyEntry.Version);
        Assert.False(policyEntry.Required);
    }

    [Fact]
    public async Task ScanIisFeaturesAsync_Should_Report_Policy_Check_Failed_When_PowerShell_Throws()
    {
        var mockDiscovery = new Mock<IIisDiscoveryService>();
        var mockPowerShell = new Mock<IPowerShellExecutionService>();
        var mockLogger = new Mock<ILoggingService>();

        mockPowerShell
            .Setup(p => p.GetExecutionPolicyAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("PS host unavailable"));

        var scanner = new IisFeatureScanner(
            mockDiscovery.Object, mockPowerShell.Object, mockLogger.Object);

        var result = await scanner.ScanIisFeaturesAsync();

        var policyEntry = result.SingleOrDefault(d => d.Type == "PowerShellPolicy");
        Assert.NotNull(policyEntry);
        Assert.StartsWith("Check failed:", policyEntry.Version);
        Assert.False(policyEntry.Required);
    }

    [Fact]
    public async Task RemediateIisFeaturesAsync_Should_Install_Requested_Features_Via_PowerShell()
    {
        var mockDiscovery = new Mock<IIisDiscoveryService>();
        var mockPowerShell = new Mock<IPowerShellExecutionService>();
        var mockLogger = new Mock<ILoggingService>();

        mockPowerShell
            .Setup(p => p.ExecuteScriptAsync(
                It.Is<string>(s => s.Contains("Install-WindowsFeature")),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Success");

        var scanner = new IisFeatureScanner(
            mockDiscovery.Object, mockPowerShell.Object, mockLogger.Object);

        var result = await scanner.RemediateIisFeaturesAsync(
            new List<string> { "Web-Server", "Web-Common-Http" });

        Assert.Equal(2, result.Count);
        Assert.True(result["Web-Server"]);
        Assert.True(result["Web-Common-Http"]);

        mockPowerShell.Verify(
            p => p.ExecuteScriptAsync(
                It.Is<string>(s => s.Contains("Install-WindowsFeature -Name 'Web-Server'")),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        mockPowerShell.Verify(
            p => p.ExecuteScriptAsync(
                It.Is<string>(s => s.Contains("Install-WindowsFeature -Name 'Web-Common-Http'")),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RemediateIisFeaturesAsync_Should_Mark_Feature_Failed_When_PowerShell_Throws()
    {
        var mockDiscovery = new Mock<IIisDiscoveryService>();
        var mockPowerShell = new Mock<IPowerShellExecutionService>();
        var mockLogger = new Mock<ILoggingService>();

        mockPowerShell
            .Setup(p => p.ExecuteScriptAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("access denied"));

        var scanner = new IisFeatureScanner(
            mockDiscovery.Object, mockPowerShell.Object, mockLogger.Object);

        var result = await scanner.RemediateIisFeaturesAsync(
            new List<string> { "Web-Server" });

        Assert.Single(result);
        Assert.False(result["Web-Server"]);
    }

    [Fact]
    public async Task RemediateIisFeaturesAsync_Should_Return_Empty_Dictionary_For_Empty_Input()
    {
        var mockDiscovery = new Mock<IIisDiscoveryService>();
        var mockPowerShell = new Mock<IPowerShellExecutionService>();
        var mockLogger = new Mock<ILoggingService>();

        var scanner = new IisFeatureScanner(
            mockDiscovery.Object, mockPowerShell.Object, mockLogger.Object);

        var emptyResult = await scanner.RemediateIisFeaturesAsync(new List<string>());
        var nullResult = await scanner.RemediateIisFeaturesAsync(null!);

        Assert.Empty(emptyResult);
        Assert.Empty(nullResult);

        mockPowerShell.Verify(
            p => p.ExecuteScriptAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
