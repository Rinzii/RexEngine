using Microsoft.Extensions.Logging;

namespace Rex.Shared.Net.Transfer;

/// <summary>Splits large payloads into chunks on the transfer channel.</summary>
public sealed partial class BulkTransferManager
{
    /// <summary>Maximum payload bytes per chunk message.</summary>
    public const int MaxChunkSize = 4096;

    private readonly Dictionary<Guid, IncomingTransfer> _incomingTransfers = [];

    // TODO: Actually use this.
#pragma warning disable IDE0052
    private readonly ILogger _logger;
#pragma warning restore IDE0052

    /// <summary>Scopes logs from <paramref name="loggerFactory"/> to this manager.</summary>
    public BulkTransferManager(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<BulkTransferManager>();
    }

    /// <summary>Creates a bulk transfer manager with an existing logger.</summary>
    /// <param name="logger">Logger instance to use.</param>
    public BulkTransferManager(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>Raised after an inbound transfer reassembles successfully.</summary>
    public event Action<Guid, byte, byte[]>? TransferCompleted;

    /// <summary>Sends a bulk payload to one client.</summary>
    public void SendBulkData<T>(IServerNetChannel channel, byte dataType, T data)
    {
        byte[] raw = ProtoSerializer.Serialize(data);
        int originalSize = raw.Length;
        (byte[]? payload, bool isCompressed) = NetCompression.Compress(raw);
        var transferId = Guid.CreateVersion7();

        List<byte[]> chunks = ChunkData(payload);
        var init = new BulkTransferInitMessage(transferId, dataType, payload.Length, originalSize, isCompressed,
            chunks.Count);
        channel.Send(init);

        for (int i = 0; i < chunks.Count; i++)
        {
            var chunk = new BulkTransferChunkMessage(transferId, i, chunks[i]);
            channel.Send(chunk);
        }

        LogStartedBulkTransfer(transferId, dataType, originalSize, isCompressed, payload.Length, chunks.Count);
    }

    /// <summary>Sends a bulk payload to the server.</summary>
    public void SendBulkData<T>(IClientNetChannel channel, byte dataType, T data)
    {
        byte[] raw = ProtoSerializer.Serialize(data);
        int originalSize = raw.Length;
        (byte[]? payload, bool isCompressed) = NetCompression.Compress(raw);
        var transferId = Guid.CreateVersion7();

        List<byte[]> chunks = ChunkData(payload);
        var init = new BulkTransferInitMessage(transferId, dataType, payload.Length, originalSize, isCompressed,
            chunks.Count);
        channel.Send(init);

        for (int i = 0; i < chunks.Count; i++)
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
        if (!_incomingTransfers.TryGetValue(chunk.TransferId, out IncomingTransfer? transfer))
        {
            LogUnknownTransferChunk(chunk.TransferId);
            return;
        }

        // Chunks can arrive out of order. Index picks the slot to fill.
        transfer.ReceivedChunks[chunk.ChunkIndex] = chunk.Data;
        transfer.ChunksReceived++;

        if (transfer.ChunksReceived >= transfer.ChunkCount)
        {
            byte[] assembled = Reassemble(transfer);
            byte[] finalData = transfer.IsCompressed
                ? NetCompression.Decompress(assembled, transfer.OriginalSize)
                : assembled;

            _ = _incomingTransfers.Remove(chunk.TransferId);

            LogBulkTransferComplete(transfer.TransferId, transfer.DataType, finalData.Length);

            TransferCompleted?.Invoke(transfer.TransferId, transfer.DataType, finalData);
        }
    }

    private static List<byte[]> ChunkData(byte[] data)
    {
        var chunks = new List<byte[]>();
        int offset = 0;

        while (offset < data.Length)
        {
            int remaining = data.Length - offset;
            int chunkSize = Math.Min(remaining, MaxChunkSize);
            byte[] chunk = new byte[chunkSize];
            Buffer.BlockCopy(data, offset, chunk, 0, chunkSize);
            chunks.Add(chunk);
            offset += chunkSize;
        }

        return chunks;
    }

    private static byte[] Reassemble(IncomingTransfer transfer)
    {
        int totalSize = 0;
        foreach (byte[] chunk in transfer.ReceivedChunks)
        {
            totalSize += chunk.Length;
        }

        byte[] result = new byte[totalSize];
        int offset = 0;

        foreach (byte[] chunk in transfer.ReceivedChunks)
        {
            Buffer.BlockCopy(chunk, 0, result, offset, chunk.Length);
            offset += chunk.Length;
        }

        return result;
    }

    private sealed class IncomingTransfer
    {
        public int ChunkCount;
        public int ChunksReceived;
        public byte DataType;
        public bool IsCompressed;
        public int OriginalSize;
        public byte[][] ReceivedChunks = [];
        public int TotalSize;
        public Guid TransferId;
    }
}
