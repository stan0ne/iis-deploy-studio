namespace IISDeploy.Application.Services;

public interface ITransactionManager
{
    Task ExecuteWithRetryAsync(
        Func<CancellationToken, Task> action,
        int maxRetries = 3,
        TimeSpan? delayBetweenRetries = null,
        CancellationToken cancellationToken = default);
}
