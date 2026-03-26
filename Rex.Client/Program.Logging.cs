using Microsoft.Extensions.Logging;

namespace Rex.Client;

internal static partial class ClientProgramLog
{
    [LoggerMessage(EventId = 1040, Level = LogLevel.Information, Message = "Shutdown signal received.")]
    public static partial void ShutdownSignalReceived(this ILogger logger);

    [LoggerMessage(EventId = 1041, Level = LogLevel.Information, Message = "[Server] {Message}")]
    public static partial void ListenServerOutput(this ILogger logger, string message);

    [LoggerMessage(EventId = 1042, Level = LogLevel.Error, Message = "[Server] {Message}")]
    public static partial void ListenServerError(this ILogger logger, string message);

    [LoggerMessage(EventId = 1043, Level = LogLevel.Error, Message = "Could not find Rex.Server assembly for listen server mode.")]
    public static partial void ListenServerAssemblyNotFound(this ILogger logger);

    [LoggerMessage(EventId = 1044, Level = LogLevel.Error, Message = "Failed to start Rex.Server process.")]
    public static partial void ListenServerStartFailed(this ILogger logger);

    [LoggerMessage(EventId = 1045, Level = LogLevel.Error, Message = "Listen server exited before startup completed.")]
    public static partial void ListenServerExitedEarly(this ILogger logger);

    [LoggerMessage(EventId = 1046, Level = LogLevel.Error, Message = "Timed out waiting for listen server startup.")]
    public static partial void ListenServerStartupTimeout(this ILogger logger);

    [LoggerMessage(EventId = 1047, Level = LogLevel.Information, Message = "Stopping listen server process.")]
    public static partial void StoppingListenServer(this ILogger logger);
}
