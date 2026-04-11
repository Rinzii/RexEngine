using LiteNetLib.Utils;
using Rex.Shared.Analyzers;

namespace Rex.Shared.Net;

/// <summary>
/// Maps message IDs to deserializer delegates.
/// All message types must be registered before packets are read.
/// </summary>
public static class NetMessageRegistry
{
    private static readonly Dictionary<ushort, Func<NetDataReader, INetMessage>> s_deserializers = [];

    /// <summary>
    /// Registers a deserializer for one message type.
    /// </summary>
    /// <param name="messageId">The ID written in the packet header.</param>
    /// <param name="deserializer">Factory that reads the remaining packet bytes and returns the message.</param>
    public static void Register<T>([ForbidLiteral] ushort messageId, Func<NetDataReader, T> deserializer)
        where T : INetMessage
    {
        if (s_deserializers.ContainsKey(messageId))
        {
            throw new InvalidOperationException($"Deserializer already registered for message ID {messageId}");
        }

        s_deserializers[messageId] = reader => deserializer(reader);
    }

    /// <summary>
    /// Reads the message ID from the front of the reader and dispatches to the matching deserializer.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no deserializer is registered for the incoming ID.</exception>
    public static INetMessage Deserialize(NetDataReader reader)
    {
        // Reader position is immediately after LiteNetLib peeled the packet. First field is our message id.
        ushort messageId = reader.GetUShort();

        if (!s_deserializers.TryGetValue(messageId, out Func<NetDataReader, INetMessage>? deserializer))
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
