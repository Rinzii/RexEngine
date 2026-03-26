using Microsoft.Extensions.Logging;

namespace Rex.Server;

public sealed partial class ServerApp
{
    [LoggerMessage(EventId = 1080, Level = LogLevel.Information, Message = "Dedicated server running. Press Ctrl+C to stop.")]
    private partial void LogDedicatedServerRunning();

    [LoggerMessage(EventId = 1081, Level = LogLevel.Information, Message = "Shutdown signal received.")]
    private partial void LogShutdownSignalReceived();
}
