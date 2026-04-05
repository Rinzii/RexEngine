using LiteNetLib.Utils;

namespace Rex.Shared.Net;

/// <summary>Wire contract for messages handled by the networking layer.</summary>
public interface INetMessage
{
    /// <summary>Message type id written in the packet header.</summary>
    ushort MessageId { get; }

    /// <summary>Routing group that picks the default channel and delivery mode.</summary>
    MessageGroup Group { get; }

    /// <summary>Writes the message body after the header. Rex writes the id first using <see cref="NetMessageRegistry.WriteHeader"/>.</summary>
    /// <param name="writer">Target buffer. Do not reset it unless this type owns the whole packet.</param>
    void Serialize(NetDataWriter writer);
}
