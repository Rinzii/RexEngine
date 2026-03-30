using LiteNetLib;
using LiteNetLib.Utils;
using Rex.Shared.Analyzers;
using Rex.Shared.Net;

namespace Rex.Shared.Net.Messages;

/// <summary>
/// Reliable notice that an entity entered the replicated world.
/// </summary>
public sealed class EntitySpawnMessage : INetMessage
{
    public const ushort Id = 6;

    /// <inheritdoc />
    public ushort MessageId => Id;

    /// <inheritdoc />
    public MessageGroup Group => MessageGroup.EntityEvent;

    /// <summary>
    /// Gets the spawned entity ID.
    /// </summary>
    public int EntityId { get; }

    /// <summary>
    /// Gets the owning client session id for the entity.
    /// </summary>
    public Guid OwnerClientId { get; }

    /// <summary>
    /// Gets the entity type name.
    /// </summary>
    public string EntityType { get; }

    /// <summary>
    /// Gets the spawn X position.
    /// </summary>
    public float X { get; }

    /// <summary>
    /// Gets the spawn Y position.
    /// </summary>
    public float Y { get; }

    /// <summary>
    /// Gets the spawn Z position.
    /// </summary>
    public float Z { get; }

    /// <summary>
    /// Creates an entity spawn payload.
    /// </summary>
    public EntitySpawnMessage(int entityId, Guid ownerClientId, [ForbidLiteral] string entityType, float x, float y,
        float z)
    {
        EntityId = entityId;
        OwnerClientId = ownerClientId;
        EntityType = entityType;
        X = x;
        Y = y;
        Z = z;
    }

    /// <inheritdoc />
    public void Serialize(NetDataWriter writer)
    {
        NetMessageRegistry.WriteHeader(writer, Id);
        writer.Put(EntityId);
        writer.PutGuid(OwnerClientId);
        writer.Put(EntityType);
        writer.Put(X);
        writer.Put(Y);
        writer.Put(Z);
    }

    public static EntitySpawnMessage Deserialize(NetDataReader reader)
    {
        var entityId = reader.GetInt();
        var ownerClientId = reader.ReadGuid();
        var entityType = reader.GetString();
        var x = reader.GetFloat();
        var y = reader.GetFloat();
        var z = reader.GetFloat();
        return new EntitySpawnMessage(entityId, ownerClientId, entityType, x, y, z);
    }
}
