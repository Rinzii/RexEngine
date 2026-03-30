using Microsoft.Extensions.Logging;
using Rex.Shared.Logging;

// ReSharper disable once CheckNamespace
namespace Rex.Server.Simulation;

public sealed partial class GameServerHost
{
    [LoggerMessage(EventId = LogEventIds.GameServerHost.HostStarted, Level = LogLevel.Information,
        Message = "Server host started (tick rate: {TickRate}, max players: {MaxPlayers})")]
    private partial void LogServerHostStarted(int tickRate, int maxPlayers);

    [LoggerMessage(EventId = LogEventIds.GameServerHost.SessionAdded, Level = LogLevel.Information,
        Message = "Session added: ClientId {ClientId}")]
    private partial void LogSessionAdded(Guid clientId);

    [LoggerMessage(EventId = LogEventIds.GameServerHost.SessionRemoved, Level = LogLevel.Information,
        Message = "Session removed: ClientId {ClientId}")]
    private partial void LogSessionRemoved(Guid clientId);

    [LoggerMessage(EventId = LogEventIds.GameServerHost.BulkTransferAcked, Level = LogLevel.Debug,
        Message = "Client {ClientId} acked transfer {TransferId}: {Success}")]
    private partial void LogBulkTransferAcked(Guid clientId, Guid transferId, bool success);

    [LoggerMessage(EventId = LogEventIds.GameServerHost.ClientDisconnecting, Level = LogLevel.Information,
        Message = "Client {ClientId} disconnecting: {Reason}")]
    private partial void LogClientDisconnecting(Guid clientId, string reason);

    [LoggerMessage(EventId = LogEventIds.GameServerHost.HostShuttingDown, Level = LogLevel.Information,
        Message = "Server host shutting down...")]
    private partial void LogServerHostShuttingDown();

    [LoggerMessage(EventId = LogEventIds.GameServerHost.HostStopped, Level = LogLevel.Information,
        Message = "Server host stopped.")]
    private partial void LogServerHostStopped();

    [LoggerMessage(EventId = LogEventIds.GameServerHost.ClientAuthenticated, Level = LogLevel.Information,
        Message = "Client {ClientId} authenticated as '{PlayerName}'")]
    private partial void LogClientAuthenticated(Guid clientId, string playerName);

    [LoggerMessage(EventId = LogEventIds.GameServerHost.UnhandledNetMessage, Level = LogLevel.Debug,
        Message = "Unhandled message from ClientId {ClientId}: Id {MessageId} ({MessageType})")]
    private partial void LogUnhandledNetMessage(Guid clientId, ushort messageId, string messageType);

    [LoggerMessage(EventId = LogEventIds.GameServerHost.HostAlreadyRunning, Level = LogLevel.Error,
        Message = "Start called while the server host is already running.")]
    private partial void LogHostAlreadyRunning();
}
