using Microsoft.Extensions.Logging;
using Rex.Shared.Logging;

namespace Rex.Client.Logging;

internal static partial class ClientProgramLog
{
    [LoggerMessage(EventId = LogEventIds.ClientHost.ShutdownSignal, Level = LogLevel.Information,
        Message = "Shutdown signal received.")]
    public static partial void ShutdownSignalReceived(this ILogger logger);

    [LoggerMessage(EventId = LogEventIds.ClientHost.ListenServerStdout, Level = LogLevel.Information,
        Message = "[Server] {Message}")]
    public static partial void ListenServerOutput(this ILogger logger, string message);

    [LoggerMessage(EventId = LogEventIds.ClientHost.ListenServerStderr, Level = LogLevel.Error,
        Message = "[Server] {Message}")]
    public static partial void ListenServerError(this ILogger logger, string message);

    [LoggerMessage(EventId = LogEventIds.ClientHost.ListenServerAssemblyNotFound, Level = LogLevel.Error,
        Message = "Could not find Rex.Server assembly for listen server mode.")]
    public static partial void ListenServerAssemblyNotFound(this ILogger logger);

    [LoggerMessage(EventId = LogEventIds.ClientHost.ListenServerStartFailed, Level = LogLevel.Error,
        Message = "Failed to start Rex.Server process.")]
    public static partial void ListenServerStartFailed(this ILogger logger);

    [LoggerMessage(EventId = LogEventIds.ClientHost.ListenServerExitedEarly, Level = LogLevel.Error,
        Message = "Listen server exited before startup completed.")]
    public static partial void ListenServerExitedEarly(this ILogger logger);

    [LoggerMessage(EventId = LogEventIds.ClientHost.ListenServerStartupTimeout, Level = LogLevel.Error,
        Message = "Timed out waiting for listen server startup.")]
    public static partial void ListenServerStartupTimeout(this ILogger logger);

    [LoggerMessage(EventId = LogEventIds.ClientHost.StoppingListenServer, Level = LogLevel.Information,
        Message = "Stopping listen server process.")]
    public static partial void StoppingListenServer(this ILogger logger);

    [LoggerMessage(EventId = LogEventIds.ClientHost.InvalidConnectAddress, Level = LogLevel.Error,
        Message =
            "Invalid connect address \"{ConnectAddress}\". Use host, host:port, or bracketed IPv6 such as [::1]:port.")]
    public static partial void InvalidConnectAddress(this ILogger logger, string connectAddress);

    [LoggerMessage(EventId = LogEventIds.ClientHost.CliParseFailed, Level = LogLevel.Error,
        Message = "Command-line parse failed: {Reason}")]
    public static partial void CliParseFailed(this ILogger logger, string reason);

    [LoggerMessage(EventId = LogEventIds.ClientHost.UnrecognizedCliArgument, Level = LogLevel.Warning,
        Message = "Ignoring unrecognized command-line argument: {Argument}")]
    public static partial void UnrecognizedCliArgument(this ILogger logger, string argument);
}
