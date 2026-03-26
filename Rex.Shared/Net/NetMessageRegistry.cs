using LiteNetLib;
using LiteNetLib.Utils;
using Rex.Shared.Analyzers;

namespace Rex.Shared.Net;

/// <summary>
/// Maps message IDs to deserializer delegates.
/// All message types must be registered before packets are read.
/// </summary>
public static class NetMessageRegistry
{
    private static readonly Dictionary<ushort, Func<NetPacketReader, INetMessage>> Deserializers = new();

    /// <summary>
    /// Registers a deserializer for one message type.
    /// </summary>
    /// <param name="messageId">The ID written in the packet header.</param>
    /// <param name="deserializer">Factory that reads the remaining packet bytes and returns the message.</param>
    public static void Register<T>([ForbidLiteral] ushort messageId, Func<NetPacketReader, T> deserializer)
        where T : INetMessage
    {
        // Same id twice replaces the previous entry. Avoid duplicate ids in RegisterAll.
        Deserializers[messageId] = reader => deserializer(reader);
    }

    /// <summary>
    /// Reads the message ID from the front of the reader and dispatches to the matching deserializer.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no deserializer is registered for the incoming ID.</exception>
    public static INetMessage Deserialize(NetPacketReader reader)
    {
        // Reader position is immediately after LiteNetLib peeled the packet. First field is our message id.
        var messageId = reader.GetUShort();

        if (!Deserializers.TryGetValue(messageId, out var deserializer))
        {
            throw new InvalidOperationException($"No deserializer registered for message ID {messageId}");
        }

        return deserializer(reader);
    }

    /// <summary>
    /// Writes the message ID prefix expected by <see cref="Deserialize"/>.
    /// </summary>
    public static void WriteHeader(NetDataWriter writer, [ForbidLiteral] ushort messageId)
    {
        writer.Put(messageId);
    }
}
