using LiteNetLib;
using LiteNetLib.Utils;
using Rex.Shared.Net;

namespace Rex.Sandbox.Shared.Net.Messages;

/// <summary>
/// One sampled input frame from the Sandbox client.
/// </summary>
public sealed class PlayerInputMessage : INetMessage
{
    public const ushort Id = 4;

    public ushort MessageId => Id;
    public MessageGroup Group => MessageGroup.Input;
    public uint Tick { get; }
    public float MoveX { get; }
    public float MoveY { get; }
    public float LookX { get; }
    public float LookY { get; }
    public uint ActionFlags { get; }

    public PlayerInputMessage(uint tick, float moveX, float moveY, float lookX, float lookY, uint actionFlags)
    {
        Tick = tick;
        MoveX = moveX;
        MoveY = moveY;
        LookX = lookX;
        LookY = lookY;
        ActionFlags = actionFlags;
    }

    public void Serialize(NetDataWriter writer)
    {
        NetMessageRegistry.WriteHeader(writer, Id);
        writer.Put(Tick);
        writer.Put(MoveX);
        writer.Put(MoveY);
        writer.Put(LookX);
        writer.Put(LookY);
        writer.Put(ActionFlags);
    }

    public static PlayerInputMessage Deserialize(NetDataReader reader)
    {
        var tick = reader.GetUInt();
        var moveX = reader.GetFloat();
        var moveY = reader.GetFloat();
        var lookX = reader.GetFloat();
        var lookY = reader.GetFloat();
        var actionFlags = reader.GetUInt();
        return new PlayerInputMessage(tick, moveX, moveY, lookX, lookY, actionFlags);
    }
}
