using LiteNetLib;
using LiteNetLib.Utils;
using Rex.Shared.Net;

namespace Rex.Sandbox.Shared.Net.Messages;

/// <summary>
/// First Sandbox gameplay message sent by a client after the transport connects.
/// </summary>
public sealed class ConnectRequestMessage : INetMessage
{
    public const ushort Id = 1;

    public ushort MessageId => Id;
    public MessageGroup Group => MessageGroup.Core;
    public ushort ProtocolVersion { get; }
    public string PlayerName { get; }

    public ConnectRequestMessage(ushort protocolVersion, string playerName)
    {
        ProtocolVersion = protocolVersion;
        PlayerName = playerName;
    }

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

/// <summary>Sandbox reply to <see cref="ConnectRequestMessage"/>.</summary>
public sealed class ConnectResponseMessage : INetMessage
{
    public const ushort Id = 2;

    public ushort MessageId => Id;
    /// <summary>
    /// Writes this message to the network writer.
    /// </summary>
    public MessageGroup Group => MessageGroup.Core;
    public bool Accepted { get; }
    public Guid ClientId { get; }
    public int TickRate { get; }
    /// <summary>
    /// Reads this message from the network reader.
    /// </summary>
    public int LocalPlayerEntityId { get; }
    public string? RejectReason { get; }

    public ConnectResponseMessage(bool accepted, Guid clientId, int tickRate, int localPlayerEntityId = 0,
        string? rejectReason = null)
    {
        Accepted = accepted;
        ClientId = clientId;
        TickRate = tickRate;
        LocalPlayerEntityId = localPlayerEntityId;
        RejectReason = rejectReason;
    }

    public void Serialize(NetDataWriter writer)
    {
        NetMessageRegistry.WriteHeader(writer, Id);
        writer.Put(Accepted);
        writer.PutGuid(ClientId);
        writer.Put(TickRate);
        writer.Put(LocalPlayerEntityId);
        writer.Put(RejectReason ?? string.Empty);
    }

    /// <summary>
    /// Reads this message from the network reader.
    /// </summary>
    public static ConnectResponseMessage Deserialize(NetDataReader reader)
    {
        var accepted = reader.GetBool();
        var clientId = reader.ReadGuid();
        var tickRate = reader.GetInt();
        var localPlayerEntityId = reader.GetInt();
        var rejectReason = reader.GetString();
        return new ConnectResponseMessage(accepted, clientId, tickRate, localPlayerEntityId,
            string.IsNullOrEmpty(rejectReason) ? null : rejectReason);
    }
}
