using LiteNetLib.Utils;

namespace Rex.Shared.Net;

public interface INetMessage
{
    ushort MessageId { get; }

    /// <summary>
    /// Determines the default delivery method and channel for this message.
    /// </summary>
    MessageGroup Group { get; }

    void Serialize(NetDataWriter writer);
}
