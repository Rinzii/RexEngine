using Microsoft.Extensions.Logging;

namespace Rex.Shared.Startup;

/// <summary>
/// Builds console startup services that are shared by client and server entrypoints.
/// </summary>
public static class ConsoleStartupSupport
{
    /// <summary>
    /// Creates a console logger factory for startup code.
    /// </summary>
    public static ILoggerFactory CreateLoggerFactory(LogLevel minimumLevel = LogLevel.Information)
    {
        return LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(minimumLevel);
        });
    }
}

/// <summary>
/// Stops a running startup host when Ctrl plus C is pressed.
/// </summary>
public sealed class ConsoleShutdownHook : IDisposable
{
    private readonly CancellationTokenSource _cts;
    private readonly Action _stop;
    private bool _disposed;

    /// <summary>
    /// Hooks console shutdown to a cancellation source and stop callback.
    /// </summary>
    public ConsoleShutdownHook(CancellationTokenSource cts, Action stop)
    {
        _cts = cts;
        _stop = stop;
        Console.CancelKeyPress += OnCancelKeyPress;
    }

    /// <summary>
    /// Removes the console shutdown subscription.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Console.CancelKeyPress -= OnCancelKeyPress;
        _disposed = true;
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        if (!_cts.IsCancellationRequested)
        {
            _cts.Cancel();
        }

        _stop();
    }
}
