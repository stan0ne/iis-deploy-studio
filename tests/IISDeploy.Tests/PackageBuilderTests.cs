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

    [Fact]
    public async Task BuildPackage_Should_Produce_Stable_Checksum_For_Identical_Content()
    {
        var service = new ZipPackageBuilderService();
        var stagingPath = Path.Combine(Path.GetTempPath(), $"iisdeploy-stable-{Guid.NewGuid():N}");
        var output1 = Path.Combine(Path.GetTempPath(), $"stable-1-{Guid.NewGuid():N}.iispackage");
        var output2 = Path.Combine(Path.GetTempPath(), $"stable-2-{Guid.NewGuid():N}.iispackage");

        try
        {
            Directory.CreateDirectory(stagingPath);
            Directory.CreateDirectory(Path.Combine(stagingPath, "sites"));
            await File.WriteAllTextAsync(
                Path.Combine(stagingPath, "sites", "stable_site.json"),
                @"{ ""name"": ""StableSite"", ""state"": ""Started"" }");

            var fixedTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var manifest = new PackageManifest
            {
                PackageVersion = "1.0.0",
                ExportTimestamp = fixedTime,
                ExportedBy = "stable-test",
                ExportedSiteNames = ["StableSite"],
                SourceMachine = new MachineInfo
                {
                    MachineName = "STABLEPC",
                    OsVersion = "Windows Test",
                    OsArchitecture = "x64"
                },
                SourceIisInfo = new IisServerInfo { IisVersion = "10.0", SiteCount = 1 }
            };

            await service.BuildPackageAsync(manifest, stagingPath, output1);
            await service.BuildPackageAsync(manifest, stagingPath, output2);

            var manifest1 = await service.ReadManifestAsync(output1);
            var manifest2 = await service.ReadManifestAsync(output2);

            Assert.NotNull(manifest1.Checksum);
            Assert.NotNull(manifest2.Checksum);
            Assert.Equal(manifest1.Checksum, manifest2.Checksum);
        }
        finally
        {
            if (Directory.Exists(stagingPath)) Directory.Delete(stagingPath, recursive: true);
            if (File.Exists(output1)) File.Delete(output1);
            if (File.Exists(output2)) File.Delete(output2);
        }
    }

    [Fact]
    public async Task BuildPackage_Should_Produce_Different_Checksum_When_Content_Changes()
    {
        var service = new ZipPackageBuilderService();
        var stagingA = Path.Combine(Path.GetTempPath(), $"iisdeploy-a-{Guid.NewGuid():N}");
        var stagingB = Path.Combine(Path.GetTempPath(), $"iisdeploy-b-{Guid.NewGuid():N}");
        var outputA = Path.Combine(Path.GetTempPath(), $"diff-a-{Guid.NewGuid():N}.iispackage");
        var outputB = Path.Combine(Path.GetTempPath(), $"diff-b-{Guid.NewGuid():N}.iispackage");

        try
        {
            Directory.CreateDirectory(stagingA);
            Directory.CreateDirectory(stagingB);
            await File.WriteAllTextAsync(
                Path.Combine(stagingA, "config.json"),
                @"{ ""version"": ""1.0"" }");
            await File.WriteAllTextAsync(
                Path.Combine(stagingB, "config.json"),
                @"{ ""version"": ""2.0"" }");

            var manifest = new PackageManifest
            {
                PackageVersion = "1.0.0",
                ExportTimestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ExportedBy = "diff-test",
                SourceMachine = new MachineInfo { MachineName = "DIFFPC" },
                SourceIisInfo = new IisServerInfo()
            };

            await service.BuildPackageAsync(manifest, stagingA, outputA);
            await service.BuildPackageAsync(manifest, stagingB, outputB);

            var manifestA = await service.ReadManifestAsync(outputA);
            var manifestB = await service.ReadManifestAsync(outputB);

            Assert.NotNull(manifestA.Checksum);
            Assert.NotNull(manifestB.Checksum);
            Assert.NotEqual(manifestA.Checksum, manifestB.Checksum);
        }
        finally
        {
            if (Directory.Exists(stagingA)) Directory.Delete(stagingA, recursive: true);
            if (Directory.Exists(stagingB)) Directory.Delete(stagingB, recursive: true);
            if (File.Exists(outputA)) File.Delete(outputA);
            if (File.Exists(outputB)) File.Delete(outputB);
        }
    }
}
