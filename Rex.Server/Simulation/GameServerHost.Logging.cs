using Microsoft.Extensions.Logging;

namespace Rex.Server.Simulation;

public sealed partial class GameServerHost
{
    [LoggerMessage(EventId = 1090, Level = LogLevel.Information,
        Message = "Server host started (tick rate: {TickRate}, max players: {MaxPlayers})")]
    private partial void LogServerHostStarted(int tickRate, int maxPlayers);

    [LoggerMessage(EventId = 1091, Level = LogLevel.Information, Message = "Session added: ClientId {ClientId}")]
    private partial void LogSessionAdded(int clientId);

    [LoggerMessage(EventId = 1092, Level = LogLevel.Information, Message = "Session removed: ClientId {ClientId}")]
    private partial void LogSessionRemoved(int clientId);

    [LoggerMessage(EventId = 1093, Level = LogLevel.Debug, Message = "Client {ClientId} acked transfer {TransferId}: {Success}")]
    private partial void LogBulkTransferAcked(int clientId, int transferId, bool success);

    [LoggerMessage(EventId = 1094, Level = LogLevel.Information, Message = "Client {ClientId} disconnecting: {Reason}")]
    private partial void LogClientDisconnecting(int clientId, string reason);

    [LoggerMessage(EventId = 1095, Level = LogLevel.Information, Message = "Server host shutting down...")]
    private partial void LogServerHostShuttingDown();

    [LoggerMessage(EventId = 1096, Level = LogLevel.Information, Message = "Server host stopped.")]
    private partial void LogServerHostStopped();

    [LoggerMessage(EventId = 1097, Level = LogLevel.Information, Message = "Client {ClientId} authenticated as '{PlayerName}'")]
    private partial void LogClientAuthenticated(int clientId, string playerName);
}
