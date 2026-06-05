using IISDeploy.Core.Models;

namespace IISDeploy.Core.Interfaces;

public interface IReportGeneratorService
{
    Task<string> GenerateHtmlReportAsync(MigrationReport report);
    Task<string> GenerateJsonReportAsync(MigrationReport report);
    Task<string> GeneratePdfReportAsync(MigrationReport report);
    Task<MigrationReport?> FindPreviousImportAsync(
        string packageChecksum,
        string targetMachine,
        string? reportsDirectory = null);
}
