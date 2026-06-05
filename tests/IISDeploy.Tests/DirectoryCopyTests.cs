using System.Reflection;
using IISDeploy.Infrastructure.Iis;

namespace IISDeploy.Tests;

public class DirectoryCopyTests
{
    [Fact]
    public async Task CopyDirectoryAsync_Should_Copy_All_Nested_Directories_When_More_Than_1000_Directories_Exist()
    {
        var sourceDir = Path.Combine(Path.GetTempPath(), $"iisdeploy-copy-test-dirs-{Guid.NewGuid():N}");
        var destDir = Path.Combine(Path.GetTempPath(), $"iisdeploy-copy-dest-dirs-{Guid.NewGuid():N}");

        Directory.CreateDirectory(sourceDir);
        try
        {
            for (int i = 0; i < 1001; i++)
            {
                var subDir = Path.Combine(sourceDir, $"dir_{i:D4}");
                Directory.CreateDirectory(subDir);
                await File.WriteAllTextAsync(Path.Combine(subDir, "marker.txt"), i.ToString());
            }

            var method = typeof(IisExportService).GetMethod("CopyDirectoryAsync", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            await (Task)method.Invoke(null, new object?[] { sourceDir, destDir, CancellationToken.None })!;

            var copiedDirs = Directory.GetDirectories(destDir, "dir_*", SearchOption.AllDirectories);

            Assert.Equal(1001, copiedDirs.Length);
        }
        finally
        {
            if (Directory.Exists(sourceDir)) Directory.Delete(sourceDir, recursive: true);
            if (Directory.Exists(destDir)) Directory.Delete(destDir, recursive: true);
        }
    }

    [Fact]
    public async Task CopyDirectoryAsync_Should_Copy_All_Files_When_More_Than_10000_Files_Exist()
    {
        var sourceDir = Path.Combine(Path.GetTempPath(), $"iisdeploy-copy-test-{Guid.NewGuid():N}");
        var destDir = Path.Combine(Path.GetTempPath(), $"iisdeploy-copy-dest-{Guid.NewGuid():N}");

        Directory.CreateDirectory(sourceDir);
        try
        {
            for (int i = 0; i < 10001; i++)
            {
                var filePath = Path.Combine(sourceDir, $"file_{i:D6}.txt");
                await File.WriteAllTextAsync(filePath, "x");
            }

            var method = typeof(IisExportService).GetMethod("CopyDirectoryAsync", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            await (Task)method.Invoke(null, new object?[] { sourceDir, destDir, CancellationToken.None })!;

            var copiedFiles = Directory.GetFiles(destDir, "*.txt", SearchOption.AllDirectories);

            Assert.Equal(10001, copiedFiles.Length);
        }
        finally
        {
            if (Directory.Exists(sourceDir)) Directory.Delete(sourceDir, recursive: true);
            if (Directory.Exists(destDir)) Directory.Delete(destDir, recursive: true);
        }
    }
}
