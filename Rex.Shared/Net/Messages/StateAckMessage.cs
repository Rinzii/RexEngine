using LiteNetLib;
using LiteNetLib.Utils;

namespace Rex.Shared.Net.Messages;

/// <summary>
/// Client ack that tells the server which snapshot tick it has applied.
/// </summary>
public sealed class StateAckMessage : INetMessage
{
    public const ushort Id = 8;

    /// <inheritdoc />
    public ushort MessageId => Id;

    /// <inheritdoc />
    public MessageGroup Group => MessageGroup.Core;

    /// <summary>
    /// Gets the latest server tick known by the client.
    /// </summary>
    public uint AcknowledgedTick { get; }

    /// <summary>
    /// Creates a snapshot ack payload.
    /// </summary>
    public StateAckMessage(uint acknowledgedTick)
    {
        AcknowledgedTick = acknowledgedTick;
    }

    /// <inheritdoc />
    public void Serialize(NetDataWriter writer)
    {
        NetMessageRegistry.WriteHeader(writer, Id);
        writer.Put(AcknowledgedTick);
    }

    public static StateAckMessage Deserialize(NetDataReader reader)
    {
        var acknowledgedTick = reader.GetUInt();
        return new StateAckMessage(acknowledgedTick);
    }
}
