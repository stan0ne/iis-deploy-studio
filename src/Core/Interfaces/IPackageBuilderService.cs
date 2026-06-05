using IISDeploy.Core.Models;

namespace IISDeploy.Core.Interfaces;

public interface IPackageBuilderService
{
    Task<string> BuildPackageAsync(
        PackageManifest manifest,
        string stagingPath,
        string outputPath,
        CancellationToken cancellationToken = default,
        IProgress<int>? progress = null);

    Task<bool> ValidatePackageAsync(string packagePath);
    Task<PackageManifest> ReadManifestAsync(string packagePath);
    Task<string> CalculateChecksumAsync(string packagePath);
}
