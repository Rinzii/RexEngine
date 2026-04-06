using LiteNetLib;
using LiteNetLib.Utils;
using Rex.Shared.Net;

namespace Rex.Shared.Net.Transfer;

/// <summary>Starts a bulk transfer and describes the following payload.</summary>
public sealed class BulkTransferInitMessage : INetMessage
{
    /// <summary>Wire id for <see cref="BulkTransferInitMessage"/>.</summary>
    public const ushort Id = 100;

    /// <inheritdoc />
    public ushort MessageId => Id;

    /// <inheritdoc />
    public MessageGroup Group => MessageGroup.Transfer;

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

    /// <inheritdoc />
    public void Serialize(NetDataWriter writer)
    {
        NetMessageRegistry.WriteHeader(writer, Id);
        writer.PutGuid(TransferId);
        writer.Put((byte)DataType);
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
        var transferId = reader.ReadGuid();
        var dataType = reader.GetByte();
        var totalSize = reader.GetInt();
        var originalSize = reader.GetInt();
        var isCompressed = reader.GetBool();
        var chunkCount = reader.GetInt();
        return new BulkTransferInitMessage(transferId, dataType, totalSize, originalSize, isCompressed, chunkCount);
    }
}

/// <summary>One chunk of payload bytes during a bulk transfer.</summary>
public sealed class BulkTransferChunkMessage : INetMessage
{
    /// <summary>Wire id for <see cref="BulkTransferChunkMessage"/>.</summary>
    public const ushort Id = 101;

    /// <inheritdoc />
    public ushort MessageId => Id;

    /// <inheritdoc />
    public MessageGroup Group => MessageGroup.Transfer;

    /// <summary>Transfer id shared by every chunk in this transfer.</summary>
    public Guid TransferId { get; }

    /// <summary>Chunk index. The first chunk uses zero.</summary>
    public int ChunkIndex { get; }

    /// <summary>Raw bytes for this chunk.</summary>
    public byte[] Data { get; }

    /// <summary>Builds one chunk for an in-flight bulk transfer.</summary>
    public BulkTransferChunkMessage(Guid transferId, int chunkIndex, byte[] data)
    {
        TransferId = transferId;
        ChunkIndex = chunkIndex;
        Data = data;
    }

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
        var transferId = reader.ReadGuid();
        var chunkIndex = reader.GetInt();
        var data = reader.GetBytesWithLength();
        return new BulkTransferChunkMessage(transferId, chunkIndex, data);
    }
}

/// <summary>Acknowledges a completed bulk transfer.</summary>
public sealed class BulkTransferAckMessage : INetMessage
{
    /// <summary>Wire id for <see cref="BulkTransferAckMessage"/>.</summary>
    public const ushort Id = 102;

    /// <inheritdoc />
    public ushort MessageId => Id;

    /// <inheritdoc />
    public MessageGroup Group => MessageGroup.Transfer;

    /// <summary>Completed transfer id.</summary>
    public Guid TransferId { get; }

    /// <summary>True when the receiver accepted the transfer.</summary>
    public bool Success { get; }

    /// <summary>Builds an ack after the bulk transfer finishes.</summary>
    public BulkTransferAckMessage(Guid transferId, bool success)
    {
        TransferId = transferId;
        Success = success;
    }

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
        var transferId = reader.ReadGuid();
        var success = reader.GetBool();
        return new BulkTransferAckMessage(transferId, success);
    }
}
