using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Rex.Shared.Logging;

// ReSharper disable once CheckNamespace
namespace Rex.Client.Net;

public sealed partial class RemoteClientNetChannel
{
    [LoggerMessage(EventId = LogEventIds.ClientTransport.TransportStartFailed, Level = LogLevel.Error,
        Message = "Client transport failed to start (LiteNetLib NetManager.Start returned false).")]
    private partial void LogTransportStartFailed();

    [LoggerMessage(EventId = LogEventIds.ClientTransport.DeserializeMessageFailed, Level = LogLevel.Warning,
        Message = "Failed to deserialize inbound network message.")]
    private partial void LogDeserializeMessageFailed(Exception ex);

    [LoggerMessage(EventId = LogEventIds.ClientTransport.NetworkError, Level = LogLevel.Warning,
        Message = "LiteNetLib transport error from {EndPoint}: {SocketError}.")]
    private partial void LogNetworkError(IPEndPoint endPoint, SocketError socketError);

    [LoggerMessage(EventId = LogEventIds.ClientTransport.LatencyUpdated, Level = LogLevel.Debug,
        Message = "LiteNetLib latency updated for {EndPoint}: {LatencyMs} ms.")]
    private partial void LogLatencyUpdated(string endPoint, int latencyMs);
}
