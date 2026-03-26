using Microsoft.Extensions.Logging;

namespace Rex.Shared.Net.Transfer;

/// <summary>
/// Splits large payloads into chunks for transfer on a dedicated channel.
/// </summary>
public sealed class BulkTransferManager
{
    /// <summary>
    /// Chunk size used for transfer messages.
    /// </summary>
    public const int MaxChunkSize = 4096;

    private readonly ILogger _logger;
    private int _nextTransferId;
    private readonly Dictionary<int, IncomingTransfer> _incomingTransfers = new();

    /// <summary>
    /// Raised when a transfer has been fully reassembled.
    /// </summary>
    public event Action<int, BulkDataType, byte[]>? TransferCompleted;

    /// <summary>
    /// Creates a bulk transfer manager with its own logger.
    /// </summary>
    public BulkTransferManager(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<BulkTransferManager>();
    }

    /// <summary>
    /// Sends a bulk payload from the server to one client.
    /// </summary>
    public void SendBulkData<T>(IServerNetChannel channel, BulkDataType dataType, T data)
    {
        var raw = ProtoSerializer.Serialize(data);
        var originalSize = raw.Length;
        var (payload, isCompressed) = NetCompression.Compress(raw);
        var transferId = _nextTransferId++;

        var chunks = ChunkData(payload);
        var init = new BulkTransferInitMessage(transferId, dataType, payload.Length, originalSize, isCompressed, chunks.Count);
        channel.Send(init);

        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = new BulkTransferChunkMessage(transferId, i, chunks[i]);
            channel.Send(chunk);
        }

        _logger.LogDebug("Started bulk transfer {TransferId}: {DataType}, {OriginalSize} bytes (compressed: {IsCompressed}, {PayloadSize} bytes, {ChunkCount} chunks)",
            transferId, dataType, originalSize, isCompressed, payload.Length, chunks.Count);
    }

    /// <summary>
    /// Sends a bulk payload from the client to the server.
    /// </summary>
    public void SendBulkData<T>(IClientNetChannel channel, BulkDataType dataType, T data)
    {
        var raw = ProtoSerializer.Serialize(data);
        var originalSize = raw.Length;
        var (payload, isCompressed) = NetCompression.Compress(raw);
        var transferId = _nextTransferId++;

        var chunks = ChunkData(payload);
        var init = new BulkTransferInitMessage(transferId, dataType, payload.Length, originalSize, isCompressed, chunks.Count);
        channel.Send(init);

        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = new BulkTransferChunkMessage(transferId, i, chunks[i]);
            channel.Send(chunk);
        }

        _logger.LogDebug("Started bulk transfer {TransferId}: {DataType}, {OriginalSize} bytes (compressed: {IsCompressed}, {PayloadSize} bytes, {ChunkCount} chunks)",
            transferId, dataType, originalSize, isCompressed, payload.Length, chunks.Count);
    }

    /// <summary>
    /// Starts tracking an inbound transfer after its init message arrives.
    /// </summary>
    public void HandleTransferInit(BulkTransferInitMessage init)
    {
        _incomingTransfers[init.TransferId] = new IncomingTransfer
        {
            TransferId = init.TransferId,
            DataType = init.DataType,
            TotalSize = init.TotalSize,
            OriginalSize = init.OriginalSize,
            IsCompressed = init.IsCompressed,
            ChunkCount = init.ChunkCount,
            ReceivedChunks = new byte[init.ChunkCount][],
            ChunksReceived = 0
        };

        _logger.LogDebug("Receiving bulk transfer {TransferId}: {DataType}, expecting {ChunkCount} chunks ({TotalSize} bytes)",
            init.TransferId, init.DataType, init.ChunkCount, init.TotalSize);
    }

    /// <summary>
    /// Adds one chunk to an inbound transfer and finishes it when all chunks are present.
    /// </summary>
    public void HandleTransferChunk(BulkTransferChunkMessage chunk)
    {
        if (!_incomingTransfers.TryGetValue(chunk.TransferId, out var transfer))
        {
            _logger.LogWarning("Received chunk for unknown transfer {TransferId}", chunk.TransferId);
            return;
        }

        transfer.ReceivedChunks[chunk.ChunkIndex] = chunk.Data;
        transfer.ChunksReceived++;

        if (transfer.ChunksReceived >= transfer.ChunkCount)
        {
            var assembled = Reassemble(transfer);
            var finalData = transfer.IsCompressed
                ? NetCompression.Decompress(assembled, transfer.OriginalSize)
                : assembled;

            _incomingTransfers.Remove(chunk.TransferId);

            _logger.LogDebug("Bulk transfer {TransferId} complete: {DataType}, {Size} bytes",
                transfer.TransferId, transfer.DataType, finalData.Length);

            TransferCompleted?.Invoke(transfer.TransferId, transfer.DataType, finalData);
        }
    }

    private static List<byte[]> ChunkData(byte[] data)
    {
        var chunks = new List<byte[]>();
        var offset = 0;

        while (offset < data.Length)
        {
            var remaining = data.Length - offset;
            var chunkSize = Math.Min(remaining, MaxChunkSize);
            var chunk = new byte[chunkSize];
            Buffer.BlockCopy(data, offset, chunk, 0, chunkSize);
            chunks.Add(chunk);
            offset += chunkSize;
        }

        return chunks;
    }

    private static byte[] Reassemble(IncomingTransfer transfer)
    {
        var totalSize = 0;
        foreach (var chunk in transfer.ReceivedChunks)
        {
            totalSize += chunk.Length;
        }

        var result = new byte[totalSize];
        var offset = 0;

        foreach (var chunk in transfer.ReceivedChunks)
        {
            Buffer.BlockCopy(chunk, 0, result, offset, chunk.Length);
            offset += chunk.Length;
        }

        return result;
    }

    private sealed class IncomingTransfer
    {
        public int TransferId;
        public BulkDataType DataType;
        public int TotalSize;
        public int OriginalSize;
        public bool IsCompressed;
        public int ChunkCount;
        public byte[][] ReceivedChunks = Array.Empty<byte[]>();
        public int ChunksReceived;
    }
}
