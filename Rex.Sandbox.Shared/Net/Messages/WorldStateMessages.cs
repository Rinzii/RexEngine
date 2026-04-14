using LiteNetLib.Utils;
using Rex.Shared.Analyzers;
using Rex.Shared.Net;

namespace Rex.Sandbox.Shared.Net.Messages;

/// <summary>
/// Server snapshot for the current Sandbox world state.
/// </summary>
public sealed class WorldSnapshotMessage : INetMessage
{
    public const ushort Id = 5;

    public WorldSnapshotMessage(uint serverTick, uint lastProcessedInputTick, IReadOnlyList<EntityState> entities)
    {
        ServerTick = serverTick;
        LastProcessedInputTick = lastProcessedInputTick;
        Entities = entities;
    }

    public uint ServerTick { get; }
    public uint LastProcessedInputTick { get; }
    public IReadOnlyList<EntityState> Entities { get; }

    public ushort MessageId => Id;
    public MessageGroup Group => MessageGroup.Entity;

    public void Serialize(NetDataWriter writer)
    {
        NetMessageRegistry.WriteHeader(writer, Id);
        writer.Put(ServerTick);
        writer.Put(LastProcessedInputTick);
        writer.Put((ushort)Entities.Count);

        foreach (EntityState entity in Entities)
        {
            entity.Serialize(writer);
        }
    }

    public static WorldSnapshotMessage Deserialize(NetDataReader reader)
    {
        uint serverTick = reader.GetUInt();
        uint lastProcessedInputTick = reader.GetUInt();
        ushort entityCount = reader.GetUShort();
        var entities = new List<EntityState>(entityCount);

        for (int i = 0; i < entityCount; i++)
        {
            entities.Add(EntityState.Deserialize(reader));
        }

        return new WorldSnapshotMessage(serverTick, lastProcessedInputTick, entities);
    }
}

/// <summary>
/// Compact Sandbox world state for one entity inside a snapshot.
/// </summary>
public sealed class EntityState
{
    public EntityState(int entityId, float x, float y, float z, float rotationY)
    {
        EntityId = entityId;
        X = x;
        Y = y;
        Z = z;
        RotationY = rotationY;
    }

    public int EntityId { get; }
    public float X { get; }
    public float Y { get; }
    public float Z { get; }
    public float RotationY { get; }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(EntityId);
        writer.Put(X);
        writer.Put(Y);
        writer.Put(Z);
        writer.Put(RotationY);
    }

    /// <summary>
    /// Reads this message from the network reader.
    /// </summary>
    public static EntityState Deserialize(NetDataReader reader)
    {
        int entityId = reader.GetInt();
        float x = reader.GetFloat();
        float y = reader.GetFloat();
        float z = reader.GetFloat();
        float rotationY = reader.GetFloat();
        return new EntityState(entityId, x, y, z, rotationY);
    }
}

/// <summary>
/// Reliable notice that an entity entered the Sandbox replicated world.
/// </summary>
public sealed class EntitySpawnMessage : INetMessage
{
    public const ushort Id = 6;

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

    public int EntityId { get; }
    public Guid OwnerClientId { get; }
    public string EntityType { get; }
    public float X { get; }
    public float Y { get; }
    public float Z { get; }
    public ushort MessageId => Id;
    public MessageGroup Group => MessageGroup.EntityEvent;

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
        int entityId = reader.GetInt();
        Guid ownerClientId = reader.ReadGuid();
        string entityType = reader.GetString();
        float x = reader.GetFloat();
        float y = reader.GetFloat();
        float z = reader.GetFloat();
        return new EntitySpawnMessage(entityId, ownerClientId, entityType, x, y, z);
    }
}

/// <summary>
/// Reliable notice that an entity left the Sandbox replicated world.
/// </summary>
public sealed class EntityDestroyMessage : INetMessage
{
    public const ushort Id = 7;

    public EntityDestroyMessage(int entityId)
    {
        EntityId = entityId;
    }

    public int EntityId { get; }

    public ushort MessageId => Id;
    public MessageGroup Group => MessageGroup.EntityEvent;

    public void Serialize(NetDataWriter writer)
    {
        NetMessageRegistry.WriteHeader(writer, Id);
        writer.Put(EntityId);
    }

    /// <summary>
    /// Reads this message from the network reader.
    /// </summary>
    public static EntityDestroyMessage Deserialize(NetDataReader reader)
    {
        int entityId = reader.GetInt();
        return new EntityDestroyMessage(entityId);
    }
}
