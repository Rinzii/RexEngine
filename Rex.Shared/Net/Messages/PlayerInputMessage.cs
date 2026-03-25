using LiteNetLib;
using LiteNetLib.Utils;

namespace Rex.Shared.Net.Messages;

/// <summary>
/// One sampled input frame from a client.
/// </summary>
public sealed class PlayerInputMessage : INetMessage
{
    public const ushort Id = 4;

    /// <inheritdoc />
    public ushort MessageId => Id;

    /// <inheritdoc />
    public MessageGroup Group => MessageGroup.Input;

    /// <summary>
    /// Gets the client tick that produced this input.
    /// </summary>
    public uint Tick { get; }

    /// <summary>
    /// Gets movement input on the X axis.
    /// </summary>
    public float MoveX { get; }

    /// <summary>
    /// Gets movement input on the Y axis.
    /// </summary>
    public float MoveY { get; }

    /// <summary>
    /// Gets look input on the X axis.
    /// </summary>
    public float LookX { get; }

    /// <summary>
    /// Gets look input on the Y axis.
    /// </summary>
    public float LookY { get; }

    /// <summary>
    /// Gets packed action bits for jump, fire, and similar actions.
    /// </summary>
    public uint ActionFlags { get; }

    /// <summary>
    /// Creates a player input payload.
    /// </summary>
    public PlayerInputMessage(uint tick, float moveX, float moveY, float lookX, float lookY, uint actionFlags)
    {
        Tick = tick;
        MoveX = moveX;
        MoveY = moveY;
        LookX = lookX;
        LookY = lookY;
        ActionFlags = actionFlags;
    }

    /// <inheritdoc />
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

    public static PlayerInputMessage Deserialize(NetPacketReader reader)
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
