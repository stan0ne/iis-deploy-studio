using IISDeploy.Application.Services;
using IISDeploy.Infrastructure.Transactions;
using Moq;

namespace IISDeploy.Tests;

public class IisImportRollbackTests
{
    [Fact]
    public async Task TransactionScope_Rollback_Fires_Actions_In_LIFO_Order()
    {
        var mockLogger = new Mock<Core.Interfaces.ILoggingService>();
        var manager = new TransactionManager(mockLogger.Object);
        var executionOrder = new List<string>();

        var scope = manager.BeginTransaction("LifoTest");
        scope.RegisterRollback(() => { executionOrder.Add("a"); return Task.CompletedTask; });
        scope.RegisterRollback(() => { executionOrder.Add("b"); return Task.CompletedTask; });
        scope.RegisterRollback(() => { executionOrder.Add("c"); return Task.CompletedTask; });

        await scope.DisposeAsync();

        Assert.Equal(new[] { "c", "b", "a" }, executionOrder);
    }

    [Fact]
    public async Task TransactionScope_Rollback_Continues_After_Action_Throws()
    {
        var mockLogger = new Mock<Core.Interfaces.ILoggingService>();
        var manager = new TransactionManager(mockLogger.Object);
        var fired = new List<string>();

        var scope = manager.BeginTransaction("ResilienceTest");
        scope.RegisterRollback(() => { fired.Add("first"); return Task.CompletedTask; });
        scope.RegisterRollback(() => { fired.Add("second-throws"); throw new InvalidOperationException("boom"); });
        scope.RegisterRollback(() => { fired.Add("third"); return Task.CompletedTask; });

        try { await scope.DisposeAsync(); }
        catch (AggregateException) { /* expected: aggregate of action errors */ }

        Assert.Contains("first", fired);
        Assert.Contains("second-throws", fired);
        Assert.Contains("third", fired);
        Assert.Equal(3, fired.Count);
    }

    [Fact]
    public async Task TransactionScope_Commit_Prevents_Rollback_On_Dispose()
    {
        var mockLogger = new Mock<Core.Interfaces.ILoggingService>();
        var manager = new TransactionManager(mockLogger.Object);
        var fired = false;

        await using (var scope = manager.BeginTransaction("CommitTest"))
        {
            scope.RegisterRollback(() => { fired = true; return Task.CompletedTask; });
            await scope.CommitAsync();
        }

        Assert.False(fired);
    }

    [Fact]
    public void IisImportService_Constructor_Accepts_TransactionManager()
    {
        var mockLogger = new Mock<Core.Interfaces.ILoggingService>();
        var manager = new TransactionManager(mockLogger.Object);

        var service = new IISDeploy.Infrastructure.Iis.IisImportService(
            new Mock<Core.Interfaces.IPackageBuilderService>().Object,
            new Mock<Core.Interfaces.IIisDiscoveryService>().Object,
            new Mock<IConflictResolutionService>().Object,
            new Mock<Core.Interfaces.IBindingManagerService>().Object,
            mockLogger.Object,
            manager,
            new Mock<Core.Interfaces.IReportGeneratorService>().Object);

        Assert.NotNull(service);
    }
}
