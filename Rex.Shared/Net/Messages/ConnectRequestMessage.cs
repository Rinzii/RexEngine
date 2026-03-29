using LiteNetLib;
using LiteNetLib.Utils;

namespace Rex.Shared.Net.Messages;

/// <summary>
/// First gameplay message sent by a client after the transport connects.
/// </summary>
public sealed class ConnectRequestMessage : INetMessage
{
    public const ushort Id = 1;

    /// <inheritdoc />
    public ushort MessageId => Id;

    /// <inheritdoc />
    public MessageGroup Group => MessageGroup.Core;

    /// <summary>
    /// Gets the protocol version expected by the client.
    /// </summary>
    public ushort ProtocolVersion { get; }

    /// <summary>
    /// Gets the player name requested by the client.
    /// </summary>
    public string PlayerName { get; }

    /// <summary>
    /// Creates a connection request payload.
    /// </summary>
    public ConnectRequestMessage(ushort protocolVersion, string playerName)
    {
        ProtocolVersion = protocolVersion;
        PlayerName = playerName;
    }

    /// <inheritdoc />
    public void Serialize(NetDataWriter writer)
    {
        NetMessageRegistry.WriteHeader(writer, Id);
        writer.Put(ProtocolVersion);
        writer.Put(PlayerName);
    }

    public static ConnectRequestMessage Deserialize(NetDataReader reader)
    {
        var protocolVersion = reader.GetUShort();
        var playerName = reader.GetString();
        return new ConnectRequestMessage(protocolVersion, playerName);
    }
}
