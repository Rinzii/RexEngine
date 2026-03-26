using Microsoft.Extensions.Logging;
using Rex.Shared.Logging;

namespace Rex.Server.Logging;

internal static partial class ServerProgramLog
{
    [LoggerMessage(EventId = LogEventIds.ServerHost.CliParseFailed, Level = LogLevel.Error,
        Message = "Command-line parse failed: {Reason}")]
    public static partial void CliParseFailed(this ILogger logger, string reason);

    [LoggerMessage(EventId = LogEventIds.ServerHost.UnrecognizedCliArgument, Level = LogLevel.Warning,
        Message = "Ignoring unrecognized command-line argument: {Argument}")]
    public static partial void UnrecognizedCliArgument(this ILogger logger, string argument);

    [LoggerMessage(EventId = LogEventIds.ServerHost.PortAlreadyInUse, Level = LogLevel.Error,
        Message = "Dedicated server could not start: {Detail}")]
    public static partial void PortAlreadyInUse(this ILogger logger, string detail);
}
