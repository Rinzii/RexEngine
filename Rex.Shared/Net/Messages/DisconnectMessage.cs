using LiteNetLib;
using LiteNetLib.Utils;

namespace Rex.Shared.Net.Messages;

/// <summary>
/// Explicit disconnect notice sent before a channel closes.
/// </summary>
public sealed class DisconnectMessage : INetMessage
{
    public const ushort Id = 3;

    /// <inheritdoc />
    public ushort MessageId => Id;

    /// <inheritdoc />
    public MessageGroup Group => MessageGroup.Core;

    /// <summary>
    /// Gets the disconnect reason.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Creates a disconnect payload.
    /// </summary>
    public DisconnectMessage(string reason)
    {
        Reason = reason;
    }

    /// <inheritdoc />
    public void Serialize(NetDataWriter writer)
    {
        NetMessageRegistry.WriteHeader(writer, Id);
        writer.Put(Reason);
    }

    public static DisconnectMessage Deserialize(NetPacketReader reader)
    {
        var reason = reader.GetString();
        return new DisconnectMessage(reason);
    }
}
