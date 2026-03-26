using LiteNetLib;
using LiteNetLib.Utils;

namespace Rex.Shared.Net.Transfer;

/// <summary>
/// Starts a bulk transfer and describes the payload that follows.
/// </summary>
public sealed class BulkTransferInitMessage : INetMessage
{
    public const ushort Id = 100;

    /// <inheritdoc />
    public ushort MessageId => Id;

    /// <inheritdoc />
    public MessageGroup Group => MessageGroup.Transfer;

    /// <summary>
    /// Gets the transfer ID shared by all chunks in this transfer.
    /// </summary>
    public int TransferId { get; }

    /// <summary>
    /// Gets the bulk payload kind.
    /// </summary>
    public BulkDataType DataType { get; }

    /// <summary>
    /// Gets the payload size after optional compression.
    /// </summary>
    public int TotalSize { get; }

    /// <summary>
    /// Gets the original uncompressed payload size.
    /// </summary>
    public int OriginalSize { get; }

    /// <summary>
    /// Gets a value that indicates whether the payload bytes are compressed.
    /// </summary>
    public bool IsCompressed { get; }

    /// <summary>
    /// Gets the total number of chunk messages expected for this transfer.
    /// </summary>
    public int ChunkCount { get; }

    /// <summary>
    /// Creates a bulk transfer init payload.
    /// </summary>
    public BulkTransferInitMessage(int transferId, BulkDataType dataType, int totalSize, int originalSize,
        bool isCompressed, int chunkCount)
    {
        TransferId = transferId;
        DataType = dataType;
        TotalSize = totalSize;
        OriginalSize = originalSize;
        IsCompressed = isCompressed;
        ChunkCount = chunkCount;
    }

    /// <inheritdoc />
    public void Serialize(NetDataWriter writer)
    {
        NetMessageRegistry.WriteHeader(writer, Id);
        writer.Put(TransferId);
        writer.Put((byte)DataType);
        writer.Put(TotalSize);
        writer.Put(OriginalSize);
        writer.Put(IsCompressed);
        writer.Put(ChunkCount);
    }

    public static BulkTransferInitMessage Deserialize(NetPacketReader reader)
    {
        var transferId = reader.GetInt();
        var dataType = (BulkDataType)reader.GetByte();
        var totalSize = reader.GetInt();
        var originalSize = reader.GetInt();
        var isCompressed = reader.GetBool();
        var chunkCount = reader.GetInt();
        return new BulkTransferInitMessage(transferId, dataType, totalSize, originalSize, isCompressed, chunkCount);
    }
}

/// <summary>
/// A single chunk of a bulk transfer.
/// </summary>
public sealed class BulkTransferChunkMessage : INetMessage
{
    public const ushort Id = 101;

    /// <inheritdoc />
    public ushort MessageId => Id;

    /// <inheritdoc />
    public MessageGroup Group => MessageGroup.Transfer;

    /// <summary>
    /// Gets the transfer ID shared by all chunks in this transfer.
    /// </summary>
    public int TransferId { get; }

    /// <summary>
    /// Gets the zero-based chunk index.
    /// </summary>
    public int ChunkIndex { get; }

    /// <summary>
    /// Gets the raw bytes carried by this chunk.
    /// </summary>
    public byte[] Data { get; }

    /// <summary>
    /// Creates a chunk payload for an active bulk transfer.
    /// </summary>
    public BulkTransferChunkMessage(int transferId, int chunkIndex, byte[] data)
    {
        TransferId = transferId;
        ChunkIndex = chunkIndex;
        Data = data;
    }

    /// <inheritdoc />
    public void Serialize(NetDataWriter writer)
    {
        NetMessageRegistry.WriteHeader(writer, Id);
        writer.Put(TransferId);
        writer.Put(ChunkIndex);
        writer.PutBytesWithLength(Data);
    }

    public static BulkTransferChunkMessage Deserialize(NetPacketReader reader)
    {
        var transferId = reader.GetInt();
        var chunkIndex = reader.GetInt();
        var data = reader.GetBytesWithLength();
        return new BulkTransferChunkMessage(transferId, chunkIndex, data);
    }
}

/// <summary>
/// Ack for a completed bulk transfer.
/// </summary>
public sealed class BulkTransferAckMessage : INetMessage
{
    public const ushort Id = 102;

    /// <inheritdoc />
    public ushort MessageId => Id;

    /// <inheritdoc />
    public MessageGroup Group => MessageGroup.Transfer;

    /// <summary>
    /// Gets the completed transfer ID.
    /// </summary>
    public int TransferId { get; }

    /// <summary>
    /// Gets a value that indicates whether the receiver accepted the transfer.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Creates a bulk transfer ack payload.
    /// </summary>
    public BulkTransferAckMessage(int transferId, bool success)
    {
        TransferId = transferId;
        Success = success;
    }

    /// <inheritdoc />
    public void Serialize(NetDataWriter writer)
    {
        NetMessageRegistry.WriteHeader(writer, Id);
        writer.Put(TransferId);
        writer.Put(Success);
    }

    public static BulkTransferAckMessage Deserialize(NetPacketReader reader)
    {
        var transferId = reader.GetInt();
        var success = reader.GetBool();
        return new BulkTransferAckMessage(transferId, success);
    }
}