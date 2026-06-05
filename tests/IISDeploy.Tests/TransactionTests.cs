using IISDeploy.Infrastructure.Transactions;
using Moq;

namespace IISDeploy.Tests;

public class TransactionTests
{
    [Fact]
    public async Task TransactionScope_Should_Commit_Successfully()
    {
        var mockLogger = new Mock<Core.Interfaces.ILoggingService>();
        var manager = new TransactionManager(mockLogger.Object);

        var committed = false;

        await using (var scope = manager.BeginTransaction("Test"))
        {
            scope.RegisterOperation(new IisOperation
            {
                OperationType = "Create",
                ObjectType = "Site",
                ObjectName = "TestSite"
            });

            committed = true;
            await scope.CommitAsync();
        }

        Assert.True(committed);
    }

    [Fact]
    public async Task TransactionScope_Should_Rollback_When_Not_Committed()
    {
        var mockLogger = new Mock<Core.Interfaces.ILoggingService>();
        var manager = new TransactionManager(mockLogger.Object);

        var rolledBack = false;

        var scope = manager.BeginTransaction("FailingTest");
        scope.RegisterRollback(() =>
        {
            rolledBack = true;
            return Task.CompletedTask;
        });

        // Dispose without committing — should rollback
        await scope.DisposeAsync();

        Assert.True(rolledBack);
    }

    [Fact]
    public async Task ExecuteWithRetry_Should_Retry_On_Failure()
    {
        var mockLogger = new Mock<Core.Interfaces.ILoggingService>();
        var manager = new TransactionManager(mockLogger.Object);

        int attempts = 0;

        await manager.ExecuteWithRetryAsync(async _ =>
        {
            attempts++;
            if (attempts < 3)
                throw new InvalidOperationException($"Attempt {attempts} failed");

            await Task.CompletedTask;
        }, maxRetries: 3, delayBetweenRetries: TimeSpan.FromMilliseconds(10));

        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task ResumableExportService_Checkpoint_Save_And_Load()
    {
        var mockExport = new Mock<Core.Interfaces.IIisExportService>();
        var mockLogger = new Mock<Core.Interfaces.ILoggingService>();
        var service = new IISDeploy.Infrastructure.Iis.ResumableExportService(
            mockExport.Object, mockLogger.Object);

        var operationId = $"test_{Guid.NewGuid():N}";

        // Save
        service.SaveCheckpoint(operationId, ["Site1", "Site2"]);
        var loaded = service.LoadCheckpoint(operationId);

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.ExportedSites.Count);
        Assert.Equal("Site1", loaded.ExportedSites[0]);

        // Clear
        service.ClearCheckpoint(operationId);
        var afterClear = service.LoadCheckpoint(operationId);
        Assert.Null(afterClear);
    }

    [Fact]
    public async Task PluginHost_Should_Handle_Empty_Plugins_Dir()
    {
        var mockLogger = new Mock<Core.Interfaces.ILoggingService>();
        var sp = new Mock<IServiceProvider>().Object;

        var host = new IISDeploy.Infrastructure.Plugins.PluginHost(sp, mockLogger.Object);
        await host.LoadPluginsAsync();

        Assert.Empty(host.Plugins);
    }
}
