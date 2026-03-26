using Microsoft.Extensions.Logging;

namespace Rex.Shared.Net.Transfer;

public sealed partial class BulkTransferManager
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Debug,
        Message =
            "Started bulk transfer {TransferId}: {DataType}, {OriginalSize} bytes (compressed: {IsCompressed}, {PayloadSize} bytes, {ChunkCount} chunks)")]
    private partial void LogStartedBulkTransfer(int transferId, BulkDataType dataType, int originalSize, bool isCompressed,
        int payloadSize, int chunkCount);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Debug,
        Message = "Receiving bulk transfer {TransferId}: {DataType}, expecting {ChunkCount} chunks ({TotalSize} bytes)")]
    private partial void LogReceivingBulkTransfer(int transferId, BulkDataType dataType, int chunkCount, int totalSize);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Warning, Message = "Received chunk for unknown transfer {TransferId}")]
    private partial void LogUnknownTransferChunk(int transferId);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Debug, Message = "Bulk transfer {TransferId} complete: {DataType}, {Size} bytes")]
    private partial void LogBulkTransferComplete(int transferId, BulkDataType dataType, int size);
}
