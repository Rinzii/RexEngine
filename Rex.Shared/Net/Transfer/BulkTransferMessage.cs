using LiteNetLib.Utils;

namespace Rex.Shared.Net.Transfer;

/// <summary>Starts a bulk transfer and describes the following payload.</summary>
public sealed class BulkTransferInitMessage : INetMessage
{
    /// <summary>Wire id for <see cref="BulkTransferInitMessage"/>.</summary>
    public const ushort Id = 100;

    /// <summary>Builds init metadata for a bulk transfer.</summary>
    public BulkTransferInitMessage(Guid transferId, byte dataType, int totalSize, int originalSize,
        bool isCompressed, int chunkCount)
    {
        TransferId = transferId;
        DataType = dataType;
        TotalSize = totalSize;
        OriginalSize = originalSize;
        IsCompressed = isCompressed;
        ChunkCount = chunkCount;
    }

    /// <summary>Transfer id shared by every chunk in this transfer.</summary>
    public Guid TransferId { get; }

    /// <summary>Bulk payload kind from the consumer.</summary>
    public byte DataType { get; }

    /// <summary>Payload byte length on the wire after optional compression.</summary>
    public int TotalSize { get; }

    /// <summary>Uncompressed payload length before compression.</summary>
    public int OriginalSize { get; }

    /// <summary>True when the wire payload is compressed.</summary>
    public bool IsCompressed { get; }

    /// <summary>Expected chunk message count.</summary>
    public int ChunkCount { get; }

    /// <inheritdoc />
    public ushort MessageId => Id;

    /// <inheritdoc />
    public MessageGroup Group => MessageGroup.Transfer;

    /// <inheritdoc />
    public void Serialize(NetDataWriter writer)
    {
        NetMessageRegistry.WriteHeader(writer, Id);
        writer.PutGuid(TransferId);
        writer.Put(DataType);
        writer.Put(TotalSize);
        writer.Put(OriginalSize);
        writer.Put(IsCompressed);
        writer.Put(ChunkCount);
    }

    /// <summary>Rebuilds this message from the reader.</summary>
    /// <param name="reader">Source buffer positioned after the message header.</param>
    /// <returns>Parsed init payload.</returns>
    public static BulkTransferInitMessage Deserialize(NetDataReader reader)
    {
        Guid transferId = reader.ReadGuid();
        byte dataType = reader.GetByte();
        int totalSize = reader.GetInt();
        int originalSize = reader.GetInt();
        bool isCompressed = reader.GetBool();
        int chunkCount = reader.GetInt();
        return new BulkTransferInitMessage(transferId, dataType, totalSize, originalSize, isCompressed, chunkCount);
    }
}

/// <summary>One chunk of payload bytes during a bulk transfer.</summary>
public sealed class BulkTransferChunkMessage : INetMessage
{
    /// <summary>Wire id for <see cref="BulkTransferChunkMessage"/>.</summary>
    public const ushort Id = 101;

    /// <summary>Builds one chunk for an in-flight bulk transfer.</summary>
    public BulkTransferChunkMessage(Guid transferId, int chunkIndex, byte[] data)
    {
        TransferId = transferId;
        ChunkIndex = chunkIndex;
        Data = data;
    }

    /// <summary>Transfer id shared by every chunk in this transfer.</summary>
    public Guid TransferId { get; }

    /// <summary>Chunk index. The first chunk uses zero.</summary>
    public int ChunkIndex { get; }

    /// <summary>Raw bytes for this chunk.</summary>
    public byte[] Data { get; }

    /// <inheritdoc />
    public ushort MessageId => Id;

    /// <inheritdoc />
    public MessageGroup Group => MessageGroup.Transfer;

    /// <inheritdoc />
    public void Serialize(NetDataWriter writer)
    {
        NetMessageRegistry.WriteHeader(writer, Id);
        writer.PutGuid(TransferId);
        writer.Put(ChunkIndex);
        writer.PutBytesWithLength(Data);
    }

    /// <summary>Rebuilds this message from the reader.</summary>
    /// <param name="reader">Source buffer positioned after the message header.</param>
    /// <returns>Parsed chunk payload.</returns>
    public static BulkTransferChunkMessage Deserialize(NetDataReader reader)
    {
        Guid transferId = reader.ReadGuid();
        int chunkIndex = reader.GetInt();
        byte[] data = reader.GetBytesWithLength();
        return new BulkTransferChunkMessage(transferId, chunkIndex, data);
    }
}

/// <summary>Acknowledges a completed bulk transfer.</summary>
public sealed class BulkTransferAckMessage : INetMessage
{
    /// <summary>Wire id for <see cref="BulkTransferAckMessage"/>.</summary>
    public const ushort Id = 102;

    /// <summary>Builds an ack after the bulk transfer finishes.</summary>
    public BulkTransferAckMessage(Guid transferId, bool success)
    {
        TransferId = transferId;
        Success = success;
    }

    /// <summary>Completed transfer id.</summary>
    public Guid TransferId { get; }

    /// <summary>True when the receiver accepted the transfer.</summary>
    public bool Success { get; }

    /// <inheritdoc />
    public ushort MessageId => Id;

    /// <inheritdoc />
    public MessageGroup Group => MessageGroup.Transfer;

    /// <inheritdoc />
    public void Serialize(NetDataWriter writer)
    {
        NetMessageRegistry.WriteHeader(writer, Id);
        writer.PutGuid(TransferId);
        writer.Put(Success);
    }

    /// <summary>Rebuilds this message from the reader.</summary>
    /// <param name="reader">Source buffer positioned after the message header.</param>
    /// <returns>Parsed ack payload.</returns>
    public static BulkTransferAckMessage Deserialize(NetDataReader reader)
    {
        Guid transferId = reader.ReadGuid();
        bool success = reader.GetBool();
        return new BulkTransferAckMessage(transferId, success);
    }
}
