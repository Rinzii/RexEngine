using System.Net;
using LiteNetLib;
using Microsoft.Extensions.Logging;

namespace Rex.Server.Core;

public sealed partial class GameServer
{
    [LoggerMessage(EventId = 1060, Level = LogLevel.Error,
        Message =
            "Cannot listen on port {Port}. It is probably already in use. Stop the other process or use --port with a different value.")]
    private partial void LogCannotListenOnPort(int port);

    [LoggerMessage(EventId = 1061, Level = LogLevel.Information, Message = "Server listening on port {Port}")]
    private partial void LogServerListening(int port);

    [LoggerMessage(EventId = 1062, Level = LogLevel.Information, Message = "Server network layer stopped.")]
    private partial void LogServerNetworkStopped();

    [LoggerMessage(EventId = 1063, Level = LogLevel.Warning, Message = "Connection rejected: server full.")]
    private partial void LogConnectionRejectedServerFull();

    [LoggerMessage(EventId = 1064, Level = LogLevel.Information, Message = "Peer connected: {Address} -> ClientId {ClientId}")]
    private partial void LogPeerConnected(IPAddress address, int clientId);

    [LoggerMessage(EventId = 1065, Level = LogLevel.Information, Message = "Peer disconnected: ClientId {ClientId} ({Reason})")]
    private partial void LogPeerDisconnected(int clientId, DisconnectReason reason);
}
