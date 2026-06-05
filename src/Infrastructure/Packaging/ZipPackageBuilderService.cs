using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using IISDeploy.Core.Interfaces;
using IISDeploy.Core.Models;

namespace IISDeploy.Infrastructure.Packaging;

public class ZipPackageBuilderService : IPackageBuilderService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<string> BuildPackageAsync(
        PackageManifest manifest,
        string stagingPath,
        string outputPath,
        CancellationToken cancellationToken = default,
        IProgress<int>? progress = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        await Task.Run(() =>
        {
            using var archive = ZipFile.Open(outputPath, ZipArchiveMode.Create);
            var trackedProgress = new ProgressTracker(progress);
            int totalFiles = CountFiles(stagingPath);
            int processed = 0;

            foreach (var filePath in Directory.EnumerateFiles(stagingPath, "*.*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var relativePath = Path.GetRelativePath(stagingPath, filePath)
                    .Replace(Path.DirectorySeparatorChar, '/');

                if (relativePath.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
                    continue;

                archive.CreateEntryFromFile(filePath, relativePath, CompressionLevel.Optimal);
                processed++;
                trackedProgress.Report(processed, totalFiles);
            }

            var manifestJson = JsonSerializer.Serialize(manifest, _jsonOptions);
            var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
            using (var writer = new StreamWriter(manifestEntry.Open()))
            {
                writer.Write(manifestJson);
            }

            trackedProgress.Report(totalFiles + 1, totalFiles + 1);
        }, cancellationToken);

        // Compute checksum of non-manifest entries
        manifest.Checksum = await CalculateChecksumSkipManifestAsync(outputPath);
        await UpdateManifestChecksum(outputPath, manifest.Checksum);

        return outputPath;
    }

    public async Task<bool> ValidatePackageAsync(string packagePath)
    {
        if (!File.Exists(packagePath)) return false;

        try
        {
            using var archive = ZipFile.OpenRead(packagePath);
            if (archive.GetEntry("manifest.json") is null)
                return false;

            var manifest = await ReadManifestAsync(packagePath);
            if (manifest is null) return false;

            if (!string.IsNullOrEmpty(manifest.Checksum))
            {
                var actualChecksum = await CalculateChecksumSkipManifestAsync(packagePath);
                return string.Equals(manifest.Checksum, actualChecksum, StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<PackageManifest> ReadManifestAsync(string packagePath)
    {
        return await Task.Run(() =>
        {
            using var archive = ZipFile.OpenRead(packagePath);
            var entry = archive.GetEntry("manifest.json");
            if (entry is null) throw new InvalidOperationException("manifest.json not found in package.");

            using var stream = entry.Open();
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();

            try
            {
                return JsonSerializer.Deserialize<PackageManifest>(json, _jsonOptions)
                    ?? throw new InvalidOperationException("Failed to deserialize manifest: result was null.");
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"Failed to parse manifest.json: {ex.Message}. Path: {ex.Path ?? "(root)"}", ex);
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException(
                    $"Numeric format error in manifest.json: {ex.Message}", ex);
            }
        });
    }

    public async Task<string> CalculateChecksumAsync(string packagePath)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(packagePath);
        var hash = await sha.ComputeHashAsync(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    private async Task<string> CalculateChecksumSkipManifestAsync(string packagePath)
    {
        using var ms = new MemoryStream();
        using var archive = ZipFile.OpenRead(packagePath);

        foreach (var entry in archive.Entries.OrderBy(e => e.FullName))
        {
            if (entry.Name.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
                continue;

            var nameBytes = System.Text.Encoding.UTF8.GetBytes(entry.FullName + "\0");
            ms.Write(nameBytes);

            using var stream = entry.Open();
            stream.CopyTo(ms);
        }

        ms.Position = 0;
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(ms);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    private async Task UpdateManifestChecksum(string packagePath, string checksum)
    {
        var manifest = await ReadManifestAsync(packagePath);
        manifest.Checksum = checksum;

        var manifestJson = JsonSerializer.Serialize(manifest, _jsonOptions);

        await Task.Run(() =>
        {
            var tempPath = packagePath + ".tmp";
            using (var source = ZipFile.OpenRead(packagePath))
            using (var target = ZipFile.Open(tempPath, ZipArchiveMode.Create))
            {
                foreach (var entry in source.Entries)
                {
                    if (entry.Name.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
                        continue;

                    using var srcStream = entry.Open();
                    var dstEntry = target.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                    using var dstStream = dstEntry.Open();
                    srcStream.CopyTo(dstStream);
                }

                var manifestEntry = target.CreateEntry("manifest.json", CompressionLevel.Optimal);
                using var writer = new StreamWriter(manifestEntry.Open());
                writer.Write(manifestJson);
            }

            File.Move(tempPath, packagePath, overwrite: true);
        });
    }

    private static int CountFiles(string path)
    {
        if (!Directory.Exists(path)) return 0;
        return Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories).Count();
    }

    private sealed class ProgressTracker
    {
        private readonly IProgress<int>? _progress;
        private int _lastReported;

        public ProgressTracker(IProgress<int>? progress) => _progress = progress;

        public void Report(int current, int total)
        {
            if (_progress is null || total == 0) return;
            var pct = (int)((double)current / total * 100);
            if (pct > _lastReported)
            {
                _lastReported = pct;
                _progress.Report(pct);
            }
        }
    }
}
