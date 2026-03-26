using LiteNetLib.Utils;

namespace Rex.Shared.Net;

/// <summary>
/// Base contract for all messages that move across the networking layer.
/// </summary>
public interface INetMessage
{
    /// <summary>
    /// Gets the message type ID written into the packet header.
    /// </summary>
    ushort MessageId { get; }

    /// <summary>
    /// Gets the routing group that chooses the default channel and delivery mode.
    /// </summary>
    MessageGroup Group { get; }

    /// <summary>Writes the message body after the header. Rex writes the id first using <see cref="NetMessageRegistry.WriteHeader"/>.</summary>
    /// <param name="writer">Target buffer. Do not reset it unless this type owns the whole packet.</param>
    void Serialize(NetDataWriter writer);
}