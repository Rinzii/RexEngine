using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Rex.Shared.Startup;

/// <summary>
/// Watches a child process until it prints a ready token.
/// </summary>
public sealed class ReadyChildProcess : IDisposable
{
    private readonly ILogger _logger;
    private readonly string _readyLine;
    private readonly ManualResetEventSlim _readySignal = new();
    private bool _disposed;

    /// <summary>
    /// Binds process output to readiness tracking.
    /// </summary>
    public ReadyChildProcess(Process process, ILogger logger, string readyLine)
    {
        Process = process;
        _logger = logger;
        _readyLine = readyLine;
        Process.OutputDataReceived += OnOutputDataReceived;
        Process.ErrorDataReceived += OnErrorDataReceived;
    }

    /// <summary>
    /// The child process that is being observed.
    /// </summary>
    public Process Process { get; }

    /// <summary>
    /// Waits until the process prints the ready token.
    /// </summary>
    public bool WaitUntilReady(TimeSpan timeout)
    {
        return _readySignal.Wait(timeout);
    }

    /// <summary>
    /// Releases process event subscriptions and wait handles.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Process.OutputDataReceived -= OnOutputDataReceived;
        Process.ErrorDataReceived -= OnErrorDataReceived;
        _readySignal.Dispose();
        Process.Dispose();
        _disposed = true;
    }

    private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data))
        {
            return;
        }

        ReadyChildProcessLog.ChildStdout(_logger, e.Data);
        if (e.Data.Contains(_readyLine, StringComparison.Ordinal))
        {
            _readySignal.Set();
        }
    }

    private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(e.Data))
        {
            ReadyChildProcessLog.ChildStderr(_logger, e.Data);
        }
    }
}

internal static partial class ReadyChildProcessLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "[Server] {Message}")]
    public static partial void ChildStdout(ILogger logger, string message);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "[Server] {Message}")]
    public static partial void ChildStderr(ILogger logger, string message);
}
