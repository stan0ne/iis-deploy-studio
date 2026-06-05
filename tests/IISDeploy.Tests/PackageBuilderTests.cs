using IISDeploy.Core.Interfaces;
using IISDeploy.Core.Models;
using IISDeploy.Infrastructure.Packaging;

namespace IISDeploy.Tests;

public class PackageBuilderTests
{
    [Fact]
    public async Task BuildPackage_Should_Create_Valid_Zip()
    {
        var service = new ZipPackageBuilderService();

        var stagingPath = Path.Combine(Path.GetTempPath(), $"test_staging_{Guid.NewGuid():N}");
        var outputPath = Path.Combine(Path.GetTempPath(), $"test_package_{Guid.NewGuid():N}.iispackage");

        try
        {
            Directory.CreateDirectory(stagingPath);
            Directory.CreateDirectory(Path.Combine(stagingPath, "sites"));
            Directory.CreateDirectory(Path.Combine(stagingPath, "config"));

            await File.WriteAllTextAsync(
                Path.Combine(stagingPath, "sites", "test_site.json"),
                @"{ ""name"": ""TestSite"", ""state"": ""Started"" }");

            var manifest = new PackageManifest
            {
                PackageVersion = "1.0.0",
                ExportTimestamp = DateTime.UtcNow,
                ExportedBy = "test",
                ExportedSiteNames = ["TestSite"],
                SourceMachine = new MachineInfo
                {
                    MachineName = "TESTPC",
                    OsVersion = "Windows Test",
                    OsArchitecture = "x64"
                },
                SourceIisInfo = new IisServerInfo
                {
                    IisVersion = "10.0",
                    SiteCount = 1,
                    AppPoolCount = 1
                }
            };

            await service.BuildPackageAsync(manifest, stagingPath, outputPath);

            Assert.True(File.Exists(outputPath));
            var fileInfo = new FileInfo(outputPath);
            Assert.True(fileInfo.Length > 0);

            var isValid = await service.ValidatePackageAsync(outputPath);
            Assert.True(isValid);

            var readManifest = await service.ReadManifestAsync(outputPath);
            Assert.NotNull(readManifest);
            Assert.Equal("TestSite", readManifest.ExportedSiteNames[0]);
            Assert.Equal("TESTPC", readManifest.SourceMachine.MachineName);
            Assert.NotNull(readManifest.Checksum);
        }
        finally
        {
            if (Directory.Exists(stagingPath))
                Directory.Delete(stagingPath, recursive: true);
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task ValidatePackage_Should_Reject_Invalid_File()
    {
        var service = new ZipPackageBuilderService();

        var fakePath = Path.Combine(Path.GetTempPath(), $"fake_{Guid.NewGuid():N}.iispackage");
        await File.WriteAllTextAsync(fakePath, "not a zip file");

        try
        {
            var isValid = await service.ValidatePackageAsync(fakePath);
            Assert.False(isValid);
        }
        finally
        {
            if (File.Exists(fakePath))
                File.Delete(fakePath);
        }
    }

    [Fact]
    public async Task ValidatePackage_Should_Reject_Nonexistent_File()
    {
        var service = new ZipPackageBuilderService();
        var isValid = await service.ValidatePackageAsync(@"Z:\nonexistent\fake.iispackage");
        Assert.False(isValid);
    }

    [Fact]
    public async Task BuildPackage_Should_Support_Cancellation()
    {
        var service = new ZipPackageBuilderService();

        var stagingPath = Path.Combine(Path.GetTempPath(), $"test_staging_cancel_{Guid.NewGuid():N}");
        var outputPath = Path.Combine(Path.GetTempPath(), $"test_package_cancel_{Guid.NewGuid():N}.iispackage");

        try
        {
            Directory.CreateDirectory(stagingPath);

            // Create many files to test cancellation
            for (int i = 0; i < 100; i++)
            {
                await File.WriteAllTextAsync(
                    Path.Combine(stagingPath, $"file_{i:d5}.txt"),
                    new string('x', 10000));
            }

            var manifest = new PackageManifest
            {
                PackageVersion = "1.0.0",
                ExportTimestamp = DateTime.UtcNow,
                ExportedBy = "test",
                SourceMachine = new MachineInfo { MachineName = "TEST" },
                SourceIisInfo = new IisServerInfo()
            };

            var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                service.BuildPackageAsync(manifest, stagingPath, outputPath, cts.Token));
        }
        finally
        {
            if (Directory.Exists(stagingPath))
                Directory.Delete(stagingPath, recursive: true);
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task ReadManifest_Should_Throw_On_Nonexistent_File()
    {
        var service = new ZipPackageBuilderService();
        await Assert.ThrowsAnyAsync<Exception>(() =>
            service.ReadManifestAsync(@"Z:\nonexistent.iispackage"));
    }
}
