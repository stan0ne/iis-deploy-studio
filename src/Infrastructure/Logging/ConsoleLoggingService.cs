using IISDeploy.Core.Interfaces;
using Serilog;

namespace IISDeploy.Infrastructure.Logging;

public class ConsoleLoggingService : ILoggingService
{
    public void Information(string message, params object?[] args)
    {
        Log.Information(message, args!);
        ConsoleLog("INF", message);
    }

    public void Warning(string message, params object?[] args)
    {
        Log.Warning(message, args!);
        ConsoleLog("WRN", message);
    }

    public void Error(string message, params object?[] args)
    {
        Log.Error(message, args!);
        ConsoleLog("ERR", message);
    }

    public void Error(Exception ex, string message, params object?[] args)
    {
        Log.Error(ex, message, args!);
        ConsoleLog("ERR", $"{message} | {ex.GetType().Name}: {ex.Message}");
    }

    public void Debug(string message, params object?[] args)
    {
        Log.Debug(message, args!);
        ConsoleLog("DBG", message);
    }

    public void Verbose(string message, params object?[] args)
    {
        Log.Verbose(message, args!);
        ConsoleLog("VRB", message);
    }

    public void OperationStart(string operation, params object?[] args)
    {
        Log.Information("Operation start: {Operation} | {Args}", operation, args);
        ConsoleLog("OPS", $"Starting: {operation}");
    }

    public void OperationComplete(string operation, TimeSpan duration, params object?[] args)
    {
        Log.Information("Operation complete: {Operation} in {Duration:F2}s | {Args}", operation, duration.TotalSeconds, args);
        ConsoleLog("OPC", $"Completed: {operation} in {duration.TotalSeconds:F2}s");
    }

    private static void ConsoleLog(string level, string message)
    {
        var formatted = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
        Console.WriteLine(formatted);
    }
}
