using Microsoft.Extensions.Logging;
using Rex.Shared.Net;

namespace Rex.Client;

public sealed partial class ClientApp
{
    [LoggerMessage(EventId = 1030, Level = LogLevel.Error, Message = "Net mode {Mode} is not valid for the client.")]
    private partial void LogInvalidClientNetMode(NetMode mode);

    [LoggerMessage(EventId = 1031, Level = LogLevel.Information, Message = "Client running in {Mode} mode.")]
    private partial void LogClientRunning(NetMode mode);

    [LoggerMessage(EventId = 1032, Level = LogLevel.Information, Message = "Standalone world initialized.")]
    private partial void LogStandaloneWorldInitialized();
}
