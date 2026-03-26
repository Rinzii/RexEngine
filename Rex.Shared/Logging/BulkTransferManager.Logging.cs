using Microsoft.Extensions.Logging;
using Rex.Shared.Logging;

namespace Rex.Shared.Net.Transfer;

public sealed partial class BulkTransferManager
{
    [LoggerMessage(EventId = LogEventIds.BulkTransfer.TransferStarted, Level = LogLevel.Debug,
        Message =
            "Started bulk transfer {TransferId}: {DataType}, {OriginalSize} bytes (compressed: {IsCompressed}, {PayloadSize} bytes, {ChunkCount} chunks)")]
    private partial void LogStartedBulkTransfer(Guid transferId, BulkDataType dataType, int originalSize, bool isCompressed,
        int payloadSize, int chunkCount);

    [LoggerMessage(EventId = LogEventIds.BulkTransfer.TransferReceiving, Level = LogLevel.Debug,
        Message = "Receiving bulk transfer {TransferId}: {DataType}, expecting {ChunkCount} chunks ({TotalSize} bytes)")]
    private partial void LogReceivingBulkTransfer(Guid transferId, BulkDataType dataType, int chunkCount, int totalSize);

    [LoggerMessage(EventId = LogEventIds.BulkTransfer.UnknownTransferChunk, Level = LogLevel.Warning,
        Message = "Received chunk for unknown transfer {TransferId}")]
    private partial void LogUnknownTransferChunk(Guid transferId);

    [LoggerMessage(EventId = LogEventIds.BulkTransfer.TransferComplete, Level = LogLevel.Debug,
        Message = "Bulk transfer {TransferId} complete: {DataType}, {Size} bytes")]
    private partial void LogBulkTransferComplete(Guid transferId, BulkDataType dataType, int size);
}
