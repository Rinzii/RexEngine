using Microsoft.Extensions.Logging;
using Rex.Shared.Logging;

namespace Rex.Client.Net;

public sealed partial class RemoteClientNetChannel
{
    [LoggerMessage(EventId = LogEventIds.ClientTransport.TransportStartFailed, Level = LogLevel.Error,
        Message = "Client transport failed to start (LiteNetLib NetManager.Start returned false).")]
    private partial void LogTransportStartFailed();

    [LoggerMessage(EventId = LogEventIds.ClientTransport.DeserializeMessageFailed, Level = LogLevel.Warning,
        Message = "Failed to deserialize inbound network message.")]
    private partial void LogDeserializeMessageFailed(Exception ex);
}
