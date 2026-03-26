using LiteNetLib;
using LiteNetLib.Utils;

namespace Rex.Shared.Net.Messages;

/// <summary>
/// Reliable notice that an entity left the replicated world.
/// </summary>
public sealed class EntityDestroyMessage : INetMessage
{
    public const ushort Id = 7;

    /// <inheritdoc />
    public ushort MessageId => Id;

    /// <inheritdoc />
    public MessageGroup Group => MessageGroup.EntityEvent;

    /// <summary>
    /// Gets the entity ID to remove.
    /// </summary>
    public int EntityId { get; }

    /// <summary>
    /// Creates an entity destroy payload.
    /// </summary>
    public EntityDestroyMessage(int entityId)
    {
        EntityId = entityId;
    }

    /// <inheritdoc />
    public void Serialize(NetDataWriter writer)
    {
        NetMessageRegistry.WriteHeader(writer, Id);
        writer.Put(EntityId);
    }

    public static EntityDestroyMessage Deserialize(NetPacketReader reader)
    {
        var entityId = reader.GetInt();
        return new EntityDestroyMessage(entityId);
    }
}