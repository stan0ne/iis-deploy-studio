using System.Text.Json;
using IISDeploy.Core.Models;
using IISDeploy.Core.Models.Enums;
using IISDeploy.Infrastructure.Reporting;
using Moq;

namespace IISDeploy.Tests;

public class IisImportIdempotencyTests : IDisposable
{
    private readonly string _tempReportsDir;
    private readonly ReportGeneratorService _service;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public IisImportIdempotencyTests()
    {
        _tempReportsDir = Path.Combine(
            Path.GetTempPath(),
            $"iisdeploy_idem_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempReportsDir);
        _service = new ReportGeneratorService();
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempReportsDir)) Directory.Delete(_tempReportsDir, recursive: true); }
        catch { /* best-effort */ }
    }

    private void WriteReport(string filename, MigrationReport report)
    {
        var path = Path.Combine(_tempReportsDir, filename);
        File.WriteAllText(path, JsonSerializer.Serialize(report, _jsonOptions));
    }

    private static MigrationReport MakeReport(
        string checksum, string machine, OperationStatus status, DateTime generatedAt, string reportId)
    {
        return new MigrationReport
        {
            ReportId = reportId,
            GeneratedAt = generatedAt,
            ReportType = "Import",
            Status = status,
            TargetMachine = new MachineInfo { MachineName = machine },
            SourceManifest = new PackageManifest
            {
                Checksum = checksum,
                SourceMachine = new MachineInfo { MachineName = "Source" }
            }
        };
    }

    [Fact]
    public async Task FindPreviousImport_Returns_Null_When_Checksum_Empty()
    {
        var result = await _service.FindPreviousImportAsync(
            "", "MACHINE-A", _tempReportsDir);
        Assert.Null(result);
    }

    [Fact]
    public async Task FindPreviousImport_Returns_Null_When_Directory_Missing()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid():N}");
        var result = await _service.FindPreviousImportAsync(
            "abc123", "MACHINE-A", missing);
        Assert.Null(result);
    }

    [Fact]
    public async Task FindPreviousImport_Returns_Null_When_No_Matching_Checksum()
    {
        WriteReport("report_Import_r1_20240101_000000.json",
            MakeReport("different-checksum", "MACHINE-A", OperationStatus.Completed,
                DateTime.UtcNow, "r1"));

        var result = await _service.FindPreviousImportAsync(
            "target-checksum", "MACHINE-A", _tempReportsDir);
        Assert.Null(result);
    }

    [Fact]
    public async Task FindPreviousImport_Returns_Report_For_Matching_Checksum_And_Machine()
    {
        var earlier = DateTime.UtcNow.AddMinutes(-10);
        var later = DateTime.UtcNow;

        WriteReport("report_Import_r1_20240101_000000.json",
            MakeReport("target-checksum", "MACHINE-A", OperationStatus.Completed,
                earlier, "r1"));
        WriteReport("report_Import_r2_20240101_000100.json",
            MakeReport("target-checksum", "MACHINE-A", OperationStatus.Completed,
                later, "r2"));

        var result = await _service.FindPreviousImportAsync(
            "target-checksum", "MACHINE-A", _tempReportsDir);

        Assert.NotNull(result);
        Assert.Equal("r2", result!.ReportId);
    }

    [Fact]
    public async Task FindPreviousImport_Ignores_Failed_Imports()
    {
        WriteReport("report_Import_r1_20240101_000000.json",
            MakeReport("target-checksum", "MACHINE-A", OperationStatus.Failed,
                DateTime.UtcNow, "r1"));

        var result = await _service.FindPreviousImportAsync(
            "target-checksum", "MACHINE-A", _tempReportsDir);
        Assert.Null(result);
    }

    [Fact]
    public async Task FindPreviousImport_Matches_Different_Machine_As_Null()
    {
        WriteReport("report_Import_r1_20240101_000000.json",
            MakeReport("target-checksum", "MACHINE-B", OperationStatus.Completed,
                DateTime.UtcNow, "r1"));

        var result = await _service.FindPreviousImportAsync(
            "target-checksum", "MACHINE-A", _tempReportsDir);
        Assert.Null(result);
    }

    [Fact]
    public void IisImportService_Constructor_Accepts_ReportGenerator()
    {
        var mockLogger = new Mock<Core.Interfaces.ILoggingService>();
        var service = new IISDeploy.Infrastructure.Iis.IisImportService(
            new Mock<Core.Interfaces.IPackageBuilderService>().Object,
            new Mock<Core.Interfaces.IIisDiscoveryService>().Object,
            new Mock<IISDeploy.Application.Services.IConflictResolutionService>().Object,
            new Mock<Core.Interfaces.IBindingManagerService>().Object,
            mockLogger.Object,
            new IISDeploy.Infrastructure.Transactions.TransactionManager(mockLogger.Object),
            new ReportGeneratorService());

        Assert.NotNull(service);
    }
}
