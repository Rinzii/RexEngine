using Microsoft.Extensions.Logging;
using Rex.Shared.Logging;
using Rex.Shared.Net.Transfer;

namespace Rex.Client.Net;

public sealed partial class GameClient
{
    [LoggerMessage(EventId = LogEventIds.GameClient.ConnectedToServer, Level = LogLevel.Information,
        Message = "Connected to server")]
    private partial void LogConnectedToServer();

    [LoggerMessage(EventId = LogEventIds.GameClient.Disconnected, Level = LogLevel.Information,
        Message = "Disconnected: {Reason}")]
    private partial void LogDisconnected(string reason);

    [LoggerMessage(EventId = LogEventIds.GameClient.EntitySpawned, Level = LogLevel.Debug,
        Message = "Entity spawned: {EntityId} ({EntityType})")]
    private partial void LogEntitySpawned(int entityId, string entityType);

    [LoggerMessage(EventId = LogEventIds.GameClient.EntityDestroyed, Level = LogLevel.Debug,
        Message = "Entity destroyed: {EntityId}")]
    private partial void LogEntityDestroyed(int entityId);

    [LoggerMessage(EventId = LogEventIds.GameClient.ConnectionRejected, Level = LogLevel.Warning,
        Message = "Connection rejected: {Reason}")]
    private partial void LogConnectionRejected(string? reason);

    [LoggerMessage(EventId = LogEventIds.GameClient.ConnectionAccepted, Level = LogLevel.Information,
        Message = "Accepted. ClientId: {ClientId}, TickRate: {TickRate}")]
    private partial void LogConnectionAccepted(int clientId, int tickRate);

    [LoggerMessage(EventId = LogEventIds.GameClient.ClientBulkTransferComplete, Level = LogLevel.Information,
        Message = "Bulk transfer {TransferId} complete: {DataType} ({Size} bytes)")]
    private partial void LogClientBulkTransferComplete(int transferId, BulkDataType dataType, int size);

    [LoggerMessage(EventId = LogEventIds.GameClient.UnhandledNetMessage, Level = LogLevel.Debug,
        Message = "Unhandled inbound message: Id {MessageId} ({MessageType})")]
    private partial void LogUnhandledNetMessage(ushort messageId, string messageType);
}
