using System.Text;
using System.Text.Json;
using IISDeploy.Core.Interfaces;
using IISDeploy.Core.Models;

namespace IISDeploy.Infrastructure.Reporting;

public class ReportGeneratorService : IReportGeneratorService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<string> GenerateHtmlReportAsync(MigrationReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"UTF-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"<title>IISDeploy Studio — {report.ReportType} Report</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:'Segoe UI',sans-serif;margin:40px;background:#f5f5f5;color:#1a1a1a}");
        sb.AppendLine(".container{max-width:900px;margin:0 auto;background:#fff;padding:32px;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.1)}");
        sb.AppendLine("h1{color:#0078D4;margin:0 0 4px}");
        sb.AppendLine(".meta{color:#666;font-size:13px;margin-bottom:24px}");
        sb.AppendLine(".section{margin-bottom:24px}");
        sb.AppendLine(".section h2{border-bottom:2px solid #0078D4;padding-bottom:4px;font-size:16px}");
        sb.AppendLine(".success{color:#107C10}.failed{color:#D13438}.warn{color:#FF8C00}");
        sb.AppendLine("table{width:100%;border-collapse:collapse;margin-top:8px}");
        sb.AppendLine("th,td{padding:8px 12px;text-align:left;border-bottom:1px solid #e1e1e1;font-size:13px}");
        sb.AppendLine("th{background:#f0f0f0;font-weight:600}");
        sb.AppendLine(".summary{background:#f8f8f8;padding:12px;border-radius:4px;margin:8px 0}");
        sb.AppendLine(".summary p{margin:4px 0;font-size:13px}");
        sb.AppendLine(".status-badge{display:inline-block;padding:2px 8px;border-radius:3px;font-size:12px;font-weight:600}");
        sb.AppendLine(".status-success{background:#DFF6DD;color:#107C10}");
        sb.AppendLine(".status-failed{background:#FED9D9;color:#D13438}");
        sb.AppendLine(".status-warning{background:#FFF4CE;color:#FF8C00}");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<div class=\"container\">");

        // Header
        var statusClass = report.Status.ToString().Contains("Failed") ? "status-failed"
            : report.Status.ToString().Contains("Warning") ? "status-warning"
            : "status-success";

        sb.AppendLine($"<h1>IISDeploy Studio — {report.ReportType} Report</h1>");
        sb.AppendLine($"<div class=\"meta\">ID: {report.ReportId} | " +
            $"Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC | " +
            $"Status: <span class=\"status-badge {statusClass}\">{report.Status}</span></div>");

        // Machine Info
        sb.AppendLine("<div class=\"section\">");
        sb.AppendLine("<h2>Target Machine</h2>");
        sb.AppendLine($"<p>Machine: {Escape(report.TargetMachine.MachineName)} | " +
            $"OS: {Escape(report.TargetMachine.OsVersion)}</p>");
        sb.AppendLine("</div>");

        // Source Manifest
        if (report.SourceManifest is not null)
        {
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("<h2>Source Environment</h2>");
            sb.AppendLine($"<p>Machine: {Escape(report.SourceManifest.SourceMachine.MachineName)} | " +
                $"OS: {Escape(report.SourceManifest.SourceMachine.OsVersion)} | " +
                $"IIS: {Escape(report.SourceManifest.SourceIisInfo.IisVersion)}</p>");
            sb.AppendLine($"<p>Exported: {report.SourceManifest.ExportTimestamp:yyyy-MM-dd HH:mm} | " +
                $"Sites: {report.SourceManifest.ExportedSiteNames.Count} | " +
                $"App Pools: {report.SourceManifest.ExportedAppPoolNames.Count}</p>");
            sb.AppendLine("</div>");
        }

        // Summary
        sb.AppendLine("<div class=\"section\">");
        sb.AppendLine("<h2>Summary</h2>");
        sb.AppendLine("<div class=\"summary\">");
        foreach (var line in report.Summary)
            sb.AppendLine($"<p>{Escape(line)}</p>");
        sb.AppendLine("</div>");
        sb.AppendLine("</div>");

        // Entries table
        if (report.Entries.Count > 0)
        {
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("<h2>Details</h2>");
            sb.AppendLine("<table>");
            sb.AppendLine("<tr><th>Category</th><th>Item</th><th>Result</th><th>Message</th><th>Duration</th></tr>");

            foreach (var entry in report.Entries)
            {
                var rowClass = entry.Success ? "success" : "failed";
                sb.AppendLine($"<tr class=\"{rowClass}\">" +
                    $"<td>{Escape(entry.Category)}</td>" +
                    $"<td>{Escape(entry.Item)}</td>" +
                    $"<td>{(entry.Success ? "✓ Success" : "✗ Failed")}</td>" +
                    $"<td>{Escape(entry.Message ?? "-")}</td>" +
                    $"<td>{entry.Duration.TotalSeconds:F1}s</td></tr>");
            }

            sb.AppendLine("</table>");
            sb.AppendLine("</div>");
        }

        sb.AppendLine($"<p style=\"color:#999;font-size:11px\">Generated by IISDeploy Studio</p>");
        sb.AppendLine("</div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        var html = sb.ToString();
        var outputPath = GetReportPath(report, "html");
        await File.WriteAllTextAsync(outputPath, html);
        return outputPath;
    }

    public async Task<string> GenerateJsonReportAsync(MigrationReport report)
    {
        var json = JsonSerializer.Serialize(report, _jsonOptions);
        var outputPath = GetReportPath(report, "json");
        await File.WriteAllTextAsync(outputPath, json);
        return outputPath;
    }

    public async Task<string> GeneratePdfReportAsync(MigrationReport report)
    {
        // Generate HTML first, then save as PDF placeholder
        var htmlPath = await GenerateHtmlReportAsync(report);
        var pdfPath = Path.ChangeExtension(htmlPath, ".pdf.html");

        // PDF generation requires external library (QuestPDF, iTextSharp, etc.)
        // For now, save the HTML as a self-contained report with PDF note
        var note = "<!-- PDF generation requires optional library: dotnet add package QuestPDF -->\n";
        var html = await File.ReadAllTextAsync(htmlPath);
        await File.WriteAllTextAsync(pdfPath, note + html);

        return pdfPath;
    }

    private static string GetReportPath(MigrationReport report, string extension)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IISDeployStudio", "reports");

        Directory.CreateDirectory(dir);

        var filename = $"report_{report.ReportType}_{report.ReportId}_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}";
        return Path.Combine(dir, filename);
    }

    private static string Escape(string? text)
    {
        return System.Net.WebUtility.HtmlEncode(text ?? "");
    }
}
