using LiteNetLib.Utils;

namespace Rex.Shared.Net.Messages;

/// <summary>Client ack for the highest snapshot tick applied locally.</summary>
public sealed class StateAckMessage : INetMessage
{
    /// <summary>Wire id for <see cref="StateAckMessage"/>.</summary>
    public const ushort Id = 8;

    /// <summary>Builds a snapshot ack payload.</summary>
    public StateAckMessage(uint acknowledgedTick)
    {
        AcknowledgedTick = acknowledgedTick;
    }

    /// <summary>Highest server tick the client has applied locally.</summary>
    public uint AcknowledgedTick { get; }

    /// <inheritdoc />
    public ushort MessageId => Id;

    /// <inheritdoc />
    public MessageGroup Group => MessageGroup.Core;

    /// <inheritdoc />
    public void Serialize(NetDataWriter writer)
    {
        NetMessageRegistry.WriteHeader(writer, Id);
        writer.Put(AcknowledgedTick);
    }

    /// <summary>Parses the body after the header.</summary>
    public static StateAckMessage Deserialize(NetDataReader reader)
    {
        uint acknowledgedTick = reader.GetUInt();
        return new StateAckMessage(acknowledgedTick);
    }
}
