using Microsoft.Extensions.Logging;
using Rex.Shared.Net.Transfer;

namespace Rex.Client.Net;

public sealed partial class GameClient
{
    [LoggerMessage(EventId = 1010, Level = LogLevel.Information, Message = "Connected to server")]
    private partial void LogConnectedToServer();

    [LoggerMessage(EventId = 1011, Level = LogLevel.Information, Message = "Disconnected: {Reason}")]
    private partial void LogDisconnected(string reason);

    [LoggerMessage(EventId = 1012, Level = LogLevel.Debug, Message = "Entity spawned: {EntityId} ({EntityType})")]
    private partial void LogEntitySpawned(int entityId, string entityType);

    [LoggerMessage(EventId = 1013, Level = LogLevel.Debug, Message = "Entity destroyed: {EntityId}")]
    private partial void LogEntityDestroyed(int entityId);

    [LoggerMessage(EventId = 1014, Level = LogLevel.Warning, Message = "Connection rejected: {Reason}")]
    private partial void LogConnectionRejected(string? reason);

    [LoggerMessage(EventId = 1015, Level = LogLevel.Information, Message = "Accepted. ClientId: {ClientId}, TickRate: {TickRate}")]
    private partial void LogConnectionAccepted(int clientId, int tickRate);

    [LoggerMessage(EventId = 1016, Level = LogLevel.Information, Message = "Bulk transfer {TransferId} complete: {DataType} ({Size} bytes)")]
    private partial void LogClientBulkTransferComplete(int transferId, BulkDataType dataType, int size);
}
