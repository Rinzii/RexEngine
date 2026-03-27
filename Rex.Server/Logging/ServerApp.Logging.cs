using Microsoft.Extensions.Logging;
using Rex.Shared.Logging;

// ReSharper disable once CheckNamespace
namespace Rex.Server;

public sealed partial class ServerApp
{
    [LoggerMessage(EventId = LogEventIds.ServerApp.DedicatedServerRunning, Level = LogLevel.Information,
        Message = "Dedicated server running. Press Ctrl+C to stop.")]
    private partial void LogDedicatedServerRunning();

    [LoggerMessage(EventId = LogEventIds.ServerApp.ShutdownSignal, Level = LogLevel.Information,
        Message = "Shutdown signal received.")]
    private partial void LogShutdownSignalReceived();

    [LoggerMessage(EventId = LogEventIds.ServerApp.OnUpdateFailed, Level = LogLevel.Error,
        Message = "OnUpdate threw an exception.")]
    private partial void LogOnUpdateFailed(Exception ex);

    [LoggerMessage(EventId = LogEventIds.ServerApp.OnLateUpdateFailed, Level = LogLevel.Error,
        Message = "OnLateUpdate threw an exception.")]
    private partial void LogOnLateUpdateFailed(Exception ex);
}
