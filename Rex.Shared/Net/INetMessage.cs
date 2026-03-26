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

    /// <summary>Writes payload bytes after any header your format requires (Rex puts message id first via <see cref="NetMessageRegistry.WriteHeader"/>).</summary>
    /// <param name="writer">Target buffer. Do not reset unless you own the whole packet.</param>
    void Serialize(NetDataWriter writer);
}
