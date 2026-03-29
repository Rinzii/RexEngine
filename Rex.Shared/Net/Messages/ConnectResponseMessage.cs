using LiteNetLib;
using LiteNetLib.Utils;
using Rex.Shared.Net;

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

    /// <summary>Server-assigned session id when <see cref="Accepted"/> is true.</summary>
    public Guid ClientId { get; }

    /// <summary>Simulation rate the client should use.</summary>
    public int TickRate { get; }

    /// <summary>Server entity id for the local player when <see cref="Accepted"/> is true.</summary>
    public int LocalPlayerEntityId { get; }

    /// <summary>Human-readable failure text when <see cref="Accepted"/> is false.</summary>
    public string? RejectReason { get; }

    /// <summary>Builds a connection response payload.</summary>
    public ConnectResponseMessage(bool accepted, Guid clientId, int tickRate, int localPlayerEntityId = 0,
        string? rejectReason = null)
    {
        Accepted = accepted;
        ClientId = clientId;
        TickRate = tickRate;
        LocalPlayerEntityId = localPlayerEntityId;
        RejectReason = rejectReason;
    }

    /// <inheritdoc />
    public void Serialize(NetDataWriter writer)
    {
        NetMessageRegistry.WriteHeader(writer, Id);
        writer.Put(Accepted);
        writer.PutGuid(ClientId);
        writer.Put(TickRate);
        writer.Put(LocalPlayerEntityId);
        writer.Put(RejectReason ?? string.Empty);
    }

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
