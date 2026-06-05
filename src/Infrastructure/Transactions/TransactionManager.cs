using IISDeploy.Core.Interfaces;
using IISDeploy.Core.Models;
using Microsoft.Web.Administration;

namespace IISDeploy.Infrastructure.Transactions;

public class IisOperation
{
    public string OperationType { get; init; } = string.Empty;
    public string ObjectType { get; init; } = string.Empty;
    public string ObjectName { get; init; } = string.Empty;
    public string? Snapshot { get; init; }
    public Dictionary<string, object?> Properties { get; init; } = [];
}

public class TransactionScope : IAsyncDisposable
{
    private readonly ILoggingService _logger;
    private readonly List<IisOperation> _operations = [];
    private readonly List<Func<Task>> _rollbackActions = [];
    private bool _committed;
    private bool _disposed;

    public TransactionScope(ILoggingService logger)
    {
        _logger = logger;
    }

    public void RegisterOperation(IisOperation operation)
    {
        _operations.Add(operation);
    }

    public void RegisterRollback(Func<Task> rollbackAction)
    {
        _rollbackActions.Add(rollbackAction);
    }

    public async Task CommitAsync()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(TransactionScope));

        _committed = true;
        _logger.Information("Transaction committed: {Count} operations", _operations.Count);
        await Task.CompletedTask;
    }

    public async Task RollbackAsync()
    {
        if (_committed) return;

        _logger.Warning("Rolling back {Count} operations", _operations.Count);

        var errors = new List<Exception>();

        for (int i = _rollbackActions.Count - 1; i >= 0; i--)
        {
            try
            {
                await _rollbackActions[i]();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Rollback action {Index} failed", i);
                errors.Add(ex);
            }
        }

        if (errors.Count > 0)
            throw new AggregateException("Some rollback actions failed.", errors);

        _logger.Information("Rollback complete");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (!_committed)
        {
            await RollbackAsync();
        }
    }
}

public class TransactionManager
{
    private readonly ILoggingService _logger;

    public TransactionManager(ILoggingService logger)
    {
        _logger = logger;
    }

    public TransactionScope BeginTransaction(string name = "")
    {
        _logger.OperationStart("Transaction", string.IsNullOrEmpty(name) ? "Unnamed" : name);
        return new TransactionScope(_logger);
    }

    public async Task ExecuteWithRetryAsync(
        Func<CancellationToken, Task> action,
        int maxRetries = 3,
        TimeSpan? delayBetweenRetries = null,
        CancellationToken cancellationToken = default)
    {
        var delay = delayBetweenRetries ?? TimeSpan.FromSeconds(1);

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await action(cancellationToken);
                return;
            }
            catch (Exception ex) when (attempt < maxRetries && ex is not OperationCanceledException)
            {
                _logger.Warning("Attempt {Attempt}/{MaxRetries} failed: {Message}. Retrying in {Delay}s...",
                    attempt, maxRetries, ex.Message, delay.TotalSeconds);

                await Task.Delay(delay, cancellationToken);
            }
        }

        throw new InvalidOperationException($"Operation failed after {maxRetries} attempts.");
    }
}
