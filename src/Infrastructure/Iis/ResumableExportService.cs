using System.Text.Json;
using IISDeploy.Core.Interfaces;
using IISDeploy.Core.Models;

namespace IISDeploy.Infrastructure.Iis;

public class ResumableExportService
{
    private readonly IIisExportService _exportService;
    private readonly ILoggingService _logger;
    private readonly string _checkpointDir;

    public ResumableExportService(IIisExportService exportService, ILoggingService logger)
    {
        _exportService = exportService;
        _logger = logger;

        _checkpointDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IISDeployStudio", "checkpoints");
        Directory.CreateDirectory(_checkpointDir);
    }

    public async Task<string> ExportWithResumeAsync(
        IEnumerable<string> siteNames,
        string outputPath,
        string operationId,
        CancellationToken cancellationToken = default,
        IProgress<OperationProgress>? progress = null)
    {
        _logger.Information("Starting resumable export: {OperationId}", operationId);

        var checkpoint = LoadCheckpoint(operationId);
        var remainingSites = siteNames.ToList();

        if (checkpoint is not null)
        {
            remainingSites = remainingSites
                .Where(s => !checkpoint.ExportedSites.Contains(s, StringComparer.OrdinalIgnoreCase))
                .ToList();

            _logger.Information("Resuming export: {Exported}/{Total} sites already done, {Remaining} remaining",
                checkpoint.ExportedSites.Count, siteNames.Count(), remainingSites.Count);
        }

        try
        {
            var result = await _exportService.ExportSitesAsync(
                remainingSites, outputPath, Core.Models.Enums.ExportMode.MultiSite,
                false, null, false, cancellationToken, progress);

            SaveCheckpoint(operationId, siteNames.ToList());
            _logger.Information("Export complete: {OperationId}", operationId);

            return result;
        }
        catch (Exception ex)
        {
            _logger.Warning("Export interrupted at {Remaining} sites: {Message}",
                remainingSites.Count, ex.Message);

            SaveCheckpoint(operationId, siteNames.Except(remainingSites).ToList());
            throw;
        }
    }

    public ExportCheckpoint? LoadCheckpoint(string operationId)
    {
        var path = GetCheckpointPath(operationId);
        if (!File.Exists(path)) return null;

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ExportCheckpoint>(json);
        }
        catch
        {
            return null;
        }
    }

    public void SaveCheckpoint(string operationId, List<string> exportedSites)
    {
        var checkpoint = new ExportCheckpoint
        {
            OperationId = operationId,
            LastUpdated = DateTime.UtcNow,
            ExportedSites = exportedSites
        };

        var json = JsonSerializer.Serialize(checkpoint, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(GetCheckpointPath(operationId), json);
    }

    public void ClearCheckpoint(string operationId)
    {
        var path = GetCheckpointPath(operationId);
        if (File.Exists(path)) File.Delete(path);
    }

    public List<ExportCheckpoint> ListCheckpoints()
    {
        var checkpoints = new List<ExportCheckpoint>();
        foreach (var file in Directory.GetFiles(_checkpointDir, "*.checkpoint.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var checkpoint = JsonSerializer.Deserialize<ExportCheckpoint>(json);
                if (checkpoint is not null) checkpoints.Add(checkpoint);
            }
            catch { /* skip corrupt checkpoints */ }
        }

        return checkpoints.OrderByDescending(c => c.LastUpdated).ToList();
    }

    private string GetCheckpointPath(string operationId) =>
        Path.Combine(_checkpointDir, $"{Sanitize(operationId)}.checkpoint.json");

    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }
}

public class ExportCheckpoint
{
    public string OperationId { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
    public List<string> ExportedSites { get; set; } = [];
    public int TotalExported => ExportedSites.Count;
}
