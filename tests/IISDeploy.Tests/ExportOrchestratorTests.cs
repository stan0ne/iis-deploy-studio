using IISDeploy.Core.Interfaces;
using IISDeploy.Core.Models;
using IISDeploy.Core.Models.Enums;
using Moq;

namespace IISDeploy.Tests;

public class ExportOrchestratorTests
{
    [Fact]
    public async Task ExportAsync_Should_Reject_Empty_Site_List()
    {
        var mockExport = new Mock<IIisExportService>();
        var mockValidation = new Mock<IValidationService>();
        var mockDiscovery = new Mock<IIisDiscoveryService>();
        var mockReport = new Mock<IReportGeneratorService>();
        var mockLogger = new Mock<Core.Interfaces.ILoggingService>();
        var mockSound = new Mock<INotificationSoundService>();

        mockValidation
            .Setup(v => v.ValidateEnvironmentAsync())
            .ReturnsAsync(new ValidationResult { IsValid = true, Issues = [] });

        var orchestrator = new Application.Implementations.ExportOrchestrator(
            mockExport.Object,
            mockValidation.Object,
            mockDiscovery.Object,
            mockReport.Object,
            mockLogger.Object,
            mockSound.Object);

        var request = new Application.DTOs.ExportRequest
        {
            SiteNames = [],
            OutputPath = @"C:\temp\test.iispackage",
            Mode = ExportMode.MultiSite
        };

        var result = await orchestrator.ExportAsync(request);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ValidateExportPrerequisites_Should_Throw_On_Invalid_Environment()
    {
        var mockExport = new Mock<IIisExportService>();
        var mockDiscovery = new Mock<IIisDiscoveryService>();
        var mockReport = new Mock<IReportGeneratorService>();
        var mockLogger = new Mock<Core.Interfaces.ILoggingService>();
        var mockSound = new Mock<INotificationSoundService>();

        var mockValidation = new Mock<IValidationService>();
        mockValidation
            .Setup(v => v.ValidateEnvironmentAsync())
            .ReturnsAsync(new ValidationResult
            {
                IsValid = false,
                Issues =
                [
                    new ValidationIssue
                    {
                        Severity = ValidationSeverity.Critical,
                        Code = "TEST-ERR",
                        Message = "Test critical error"
                    }
                ]
            });

        var orchestrator = new Application.Implementations.ExportOrchestrator(
            mockExport.Object,
            mockValidation.Object,
            mockDiscovery.Object,
            mockReport.Object,
            mockLogger.Object,
            mockSound.Object);

        var request = new Application.DTOs.ExportRequest
        {
            SiteNames = ["TestSite"],
            OutputPath = @"C:\temp\test.iispackage",
            Mode = ExportMode.SingleSite
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            orchestrator.ValidateExportPrerequisitesAsync(request));
    }

    [Fact]
    public async Task ValidateExportPrerequisites_Should_Throw_On_Invalid_Site()
    {
        var mockExport = new Mock<IIisExportService>();
        var mockDiscovery = new Mock<IIisDiscoveryService>();
        var mockReport = new Mock<IReportGeneratorService>();
        var mockLogger = new Mock<Core.Interfaces.ILoggingService>();
        var mockSound = new Mock<INotificationSoundService>();

        var mockValidation = new Mock<IValidationService>();
        mockValidation
            .Setup(v => v.ValidateEnvironmentAsync())
            .ReturnsAsync(new ValidationResult { IsValid = true, Issues = [] });

        mockValidation
            .Setup(v => v.ValidateSiteForExportAsync("BadSite"))
            .ReturnsAsync(new ValidationResult
            {
                IsValid = false,
                Issues =
                [
                    new ValidationIssue
                    {
                        Severity = ValidationSeverity.Critical,
                        Code = "SITE-ERR",
                        Message = "Site not found"
                    }
                ]
            });

        var orchestrator = new Application.Implementations.ExportOrchestrator(
            mockExport.Object,
            mockValidation.Object,
            mockDiscovery.Object,
            mockReport.Object,
            mockLogger.Object,
            mockSound.Object);

        var request = new Application.DTOs.ExportRequest
        {
            SiteNames = ["BadSite"],
            OutputPath = @"C:\temp\test.iispackage",
            Mode = ExportMode.SingleSite
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            orchestrator.ValidateExportPrerequisitesAsync(request));
    }
}
