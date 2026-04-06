using Microsoft.Extensions.Logging;

namespace Rex.Shared.Startup;

/// <summary>Console logging and shutdown wiring at process entry.</summary>
public static class ConsoleStartupSupport
{
    /// <summary>Builds an <see cref="ILoggerFactory"/> that writes to the console.</summary>
    /// <param name="minimumLevel">Lowest level emitted by console providers.</param>
    /// <returns>A factory the caller owns and should dispose.</returns>
    public static ILoggerFactory CreateLoggerFactory(LogLevel minimumLevel = LogLevel.Information)
    {
        return LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(minimumLevel);
        });
    }
}

/// <summary>Listens for console cancel and coordinates cancellation with host stop.</summary>
public sealed class ConsoleShutdownHook : IDisposable
{
    private readonly CancellationTokenSource _cts;
    private readonly Action _stop;
    private bool _disposed;

    /// <summary>Registers <see cref="Console.CancelKeyPress"/> to cancel <paramref name="cts"/> and invoke <paramref name="stop"/>.</summary>
    /// <param name="cts">Cancellation source the runtime loop observes.</param>
    /// <param name="stop">Callback that tears down the running host.</param>
    public ConsoleShutdownHook(CancellationTokenSource cts, Action stop)
    {
        _cts = cts;
        _stop = stop;
        Console.CancelKeyPress += OnCancelKeyPress;
    }

    /// <summary>Removes the <see cref="Console.CancelKeyPress"/> handler.</summary>
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
        // Keep the default handler from terminating immediately so Stop can run cleanly.
        e.Cancel = true;
        if (!_cts.IsCancellationRequested)
        {
            _cts.Cancel();
        }

        _stop();
    }
}
