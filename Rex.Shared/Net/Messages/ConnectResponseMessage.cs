using LiteNetLib;
using LiteNetLib.Utils;

namespace Rex.Shared.Net.Messages;

/// <summary>Server reply to <see cref="ConnectRequestMessage"/>.</summary>
public sealed class ConnectResponseMessage : INetMessage
{
    public const ushort Id = 2;

    /// <inheritdoc />
    public ushort MessageId => Id;

    /// <inheritdoc />
    public MessageGroup Group => MessageGroup.Core;

    /// <summary>True when the server accepted the client.</summary>
    public bool Accepted { get; }

    /// <summary>Server-assigned client id when <see cref="Accepted"/> is true.</summary>
    public int ClientId { get; }

    /// <summary>Simulation rate the client should use.</summary>
    public int TickRate { get; }

    /// <summary>Human-readable failure text when <see cref="Accepted"/> is false.</summary>
    public string? RejectReason { get; }

    /// <summary>Builds a connection response payload.</summary>
    public ConnectResponseMessage(bool accepted, int clientId, int tickRate, string? rejectReason = null)
    {
        Accepted = accepted;
        ClientId = clientId;
        TickRate = tickRate;
        RejectReason = rejectReason;
    }

    /// <inheritdoc />
    public void Serialize(NetDataWriter writer)
    {
        NetMessageRegistry.WriteHeader(writer, Id);
        writer.Put(Accepted);
        writer.Put(ClientId);
        writer.Put(TickRate);
        writer.Put(RejectReason ?? string.Empty);
    }

    public static ConnectResponseMessage Deserialize(NetPacketReader reader)
    {
        var accepted = reader.GetBool();
        var clientId = reader.GetInt();
        var tickRate = reader.GetInt();
        var rejectReason = reader.GetString();
        return new ConnectResponseMessage(accepted, clientId, tickRate,
            string.IsNullOrEmpty(rejectReason) ? null : rejectReason);
    }
}
