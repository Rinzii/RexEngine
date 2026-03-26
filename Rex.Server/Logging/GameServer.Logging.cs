using System.Net;
using LiteNetLib;
using Microsoft.Extensions.Logging;
using Rex.Shared.Logging;

namespace Rex.Server.Core;

public sealed partial class GameServer
{
    [LoggerMessage(EventId = LogEventIds.GameServerNet.CannotListenOnPort, Level = LogLevel.Error,
        Message =
            "Cannot listen on port {Port}. It is probably already in use. Stop the other process or use --port with a different value.")]
    private partial void LogCannotListenOnPort(int port);

    [LoggerMessage(EventId = LogEventIds.GameServerNet.ServerListening, Level = LogLevel.Information,
        Message = "Server listening on port {Port}")]
    private partial void LogServerListening(int port);

    [LoggerMessage(EventId = LogEventIds.GameServerNet.ServerNetworkStopped, Level = LogLevel.Information,
        Message = "Server network layer stopped.")]
    private partial void LogServerNetworkStopped();

    [LoggerMessage(EventId = LogEventIds.GameServerNet.ConnectionRejectedServerFull, Level = LogLevel.Warning,
        Message = "Connection rejected: server full.")]
    private partial void LogConnectionRejectedServerFull();

    [LoggerMessage(EventId = LogEventIds.GameServerNet.PeerConnected, Level = LogLevel.Information,
        Message = "Peer connected: {Address} -> ClientId {ClientId}")]
    private partial void LogPeerConnected(IPAddress address, int clientId);

    [LoggerMessage(EventId = LogEventIds.GameServerNet.PeerDisconnected, Level = LogLevel.Information,
        Message = "Peer disconnected: ClientId {ClientId} ({Reason})")]
    private partial void LogPeerDisconnected(int clientId, DisconnectReason reason);

    [LoggerMessage(EventId = LogEventIds.GameServerNet.DeserializeMessageFailed, Level = LogLevel.Warning,
        Message = "Failed to deserialize inbound message for ClientId {ClientId}.")]
    private partial void LogDeserializeMessageFailed(int clientId, Exception ex);
}
