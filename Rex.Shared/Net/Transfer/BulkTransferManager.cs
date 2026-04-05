using Microsoft.Extensions.Logging;

namespace Rex.Shared.Net.Transfer;

/// <summary>Splits large payloads into chunks on the transfer channel.</summary>
public sealed partial class BulkTransferManager
{
    /// <summary>Maximum payload bytes per chunk message.</summary>
    public const int MaxChunkSize = 4096;

    private readonly ILogger _logger;
    private readonly Dictionary<Guid, IncomingTransfer> _incomingTransfers = new();

    /// <summary>Raised after an inbound transfer reassembles successfully.</summary>
    public event Action<Guid, byte, byte[]>? TransferCompleted;

    /// <summary>Scopes logs from <paramref name="loggerFactory"/> to this manager.</summary>
    public BulkTransferManager(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<BulkTransferManager>();
    }

    /// <summary>Sends a bulk payload to one client.</summary>
    public void SendBulkData<T>(IServerNetChannel channel, byte dataType, T data)
    {
        var raw = ProtoSerializer.Serialize(data);
        var originalSize = raw.Length;
        var (payload, isCompressed) = NetCompression.Compress(raw);
        var transferId = Guid.CreateVersion7();

        var chunks = ChunkData(payload);
        var init = new BulkTransferInitMessage(transferId, dataType, payload.Length, originalSize, isCompressed,
            chunks.Count);
        channel.Send(init);

        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = new BulkTransferChunkMessage(transferId, i, chunks[i]);
            channel.Send(chunk);
        }

        LogStartedBulkTransfer(transferId, dataType, originalSize, isCompressed, payload.Length, chunks.Count);
    }

    /// <summary>Sends a bulk payload to the server.</summary>
    public void SendBulkData<T>(IClientNetChannel channel, byte dataType, T data)
    {
        var raw = ProtoSerializer.Serialize(data);
        var originalSize = raw.Length;
        var (payload, isCompressed) = NetCompression.Compress(raw);
        var transferId = Guid.CreateVersion7();

        var chunks = ChunkData(payload);
        var init = new BulkTransferInitMessage(transferId, dataType, payload.Length, originalSize, isCompressed,
            chunks.Count);
        channel.Send(init);

        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = new BulkTransferChunkMessage(transferId, i, chunks[i]);
            channel.Send(chunk);
        }

        LogStartedBulkTransfer(transferId, dataType, originalSize, isCompressed, payload.Length, chunks.Count);
    }

    /// <summary>Begins tracking an inbound transfer after the init message.</summary>
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

        LogReceivingBulkTransfer(init.TransferId, init.DataType, init.ChunkCount, init.TotalSize);
    }

    /// <summary>Records one chunk and completes the transfer when all chunks arrive.</summary>
    public void HandleTransferChunk(BulkTransferChunkMessage chunk)
    {
        if (!_incomingTransfers.TryGetValue(chunk.TransferId, out var transfer))
        {
            LogUnknownTransferChunk(chunk.TransferId);
            return;
        }

        // Chunks can arrive out of order. Index picks the slot to fill.
        transfer.ReceivedChunks[chunk.ChunkIndex] = chunk.Data;
        transfer.ChunksReceived++;

        if (transfer.ChunksReceived >= transfer.ChunkCount)
        {
            var assembled = Reassemble(transfer);
            var finalData = transfer.IsCompressed
                ? NetCompression.Decompress(assembled, transfer.OriginalSize)
                : assembled;

            _incomingTransfers.Remove(chunk.TransferId);

            LogBulkTransferComplete(transfer.TransferId, transfer.DataType, finalData.Length);

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
        public Guid TransferId;
        public byte DataType;
        public int TotalSize;
        public int OriginalSize;
        public bool IsCompressed;
        public int ChunkCount;
        public byte[][] ReceivedChunks = Array.Empty<byte[]>();
        public int ChunksReceived;
    }
}
