using Microsoft.Extensions.Logging;
using Rex.Shared.Logging;
using Rex.Shared.Net;

namespace Rex.Client;

public sealed partial class ClientApp
{
    [LoggerMessage(EventId = LogEventIds.ClientApp.InvalidNetMode, Level = LogLevel.Error,
        Message = "Net mode {Mode} is not valid for the client.")]
    private partial void LogInvalidClientNetMode(NetMode mode);

    [LoggerMessage(EventId = LogEventIds.ClientApp.ClientRunning, Level = LogLevel.Information,
        Message = "Client running in {Mode} mode.")]
    private partial void LogClientRunning(NetMode mode);

    [LoggerMessage(EventId = LogEventIds.ClientApp.StandaloneWorldInitialized, Level = LogLevel.Information,
        Message = "Standalone world initialized.")]
    private partial void LogStandaloneWorldInitialized();

    [LoggerMessage(EventId = LogEventIds.ClientApp.OnUpdateFailed, Level = LogLevel.Error,
        Message = "OnUpdate threw an exception.")]
    private partial void LogOnUpdateFailed(Exception ex);

    [LoggerMessage(EventId = LogEventIds.ClientApp.OnLateUpdateFailed, Level = LogLevel.Error,
        Message = "OnLateUpdate threw an exception.")]
    private partial void LogOnLateUpdateFailed(Exception ex);

    [LoggerMessage(EventId = LogEventIds.ClientApp.MainLoopCancellationRequested, Level = LogLevel.Debug,
        Message = "Main loop exiting due to cancellation.")]
    private partial void LogMainLoopCancellationRequested();
}
