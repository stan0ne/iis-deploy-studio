using IISDeploy.Application.Services;
using IISDeploy.Core.Interfaces;

namespace IISDeploy.Infrastructure.Transactions;

public class TransactionManagerAdapter : ITransactionManager
{
    private readonly TransactionManager _manager;

    public TransactionManagerAdapter(ILoggingService logger)
    {
        _manager = new TransactionManager(logger);
    }

    public Task ExecuteWithRetryAsync(
        Func<CancellationToken, Task> action,
        int maxRetries = 3,
        TimeSpan? delayBetweenRetries = null,
        CancellationToken cancellationToken = default)
    {
        return _manager.ExecuteWithRetryAsync(action, maxRetries, delayBetweenRetries, cancellationToken);
    }
}
