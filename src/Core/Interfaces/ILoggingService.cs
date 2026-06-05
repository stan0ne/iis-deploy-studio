namespace IISDeploy.Core.Interfaces;

public interface ILoggingService
{
    void Information(string message, params object?[] args);
    void Warning(string message, params object?[] args);
    void Error(string message, params object?[] args);
    void Error(Exception ex, string message, params object?[] args);
    void Debug(string message, params object?[] args);
    void Verbose(string message, params object?[] args);
    void OperationStart(string operation, params object?[] args);
    void OperationComplete(string operation, TimeSpan duration, params object?[] args);
}
