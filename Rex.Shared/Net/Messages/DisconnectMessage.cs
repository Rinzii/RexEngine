using LiteNetLib.Utils;

namespace Rex.Shared.Net.Messages;

/// <summary>Disconnect notice sent before the channel closes.</summary>
public sealed class DisconnectMessage : INetMessage
{
    /// <summary>Wire id for <see cref="DisconnectMessage"/>.</summary>
    public const ushort Id = 3;

    /// <summary>Builds a disconnect payload.</summary>
    public DisconnectMessage(string reason)
    {
        Reason = reason;
    }

    /// <summary>Opaque reason forwarded to the peer.</summary>
    public string Reason { get; }

    /// <inheritdoc />
    public ushort MessageId => Id;

    /// <inheritdoc />
    public MessageGroup Group => MessageGroup.Core;

    /// <inheritdoc />
    public void Serialize(NetDataWriter writer)
    {
        NetMessageRegistry.WriteHeader(writer, Id);
        writer.Put(Reason);
    }

    /// <summary>Parses the body after the header.</summary>
    public static DisconnectMessage Deserialize(NetDataReader reader)
    {
        string reason = reader.GetString();
        return new DisconnectMessage(reason);
    }
}
