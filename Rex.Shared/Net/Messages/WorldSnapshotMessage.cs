using LiteNetLib;
using LiteNetLib.Utils;

namespace Rex.Shared.Net.Messages;

/// <summary>
/// Server snapshot for the current world state.
/// </summary>
public sealed class WorldSnapshotMessage : INetMessage
{
    public const ushort Id = 5;

    /// <inheritdoc />
    public ushort MessageId => Id;

    /// <inheritdoc />
    public MessageGroup Group => MessageGroup.Entity;

    /// <summary>
    /// Gets the server tick that produced this snapshot.
    /// </summary>
    public uint ServerTick { get; }

    /// <summary>
    /// Gets the latest input tick the server already applied for this client.
    /// </summary>
    public uint LastProcessedInputTick { get; }

    /// <summary>
    /// Gets the entity states included in this snapshot.
    /// </summary>
    public IReadOnlyList<EntityState> Entities { get; }

    /// <summary>
    /// Creates a world snapshot payload.
    /// </summary>
    public WorldSnapshotMessage(uint serverTick, uint lastProcessedInputTick, IReadOnlyList<EntityState> entities)
    {
        ServerTick = serverTick;
        LastProcessedInputTick = lastProcessedInputTick;
        Entities = entities;
    }

    /// <inheritdoc />
    public void Serialize(NetDataWriter writer)
    {
        NetMessageRegistry.WriteHeader(writer, Id);
        writer.Put(ServerTick);
        writer.Put(LastProcessedInputTick);
        writer.Put((ushort)Entities.Count);

        foreach (var entity in Entities)
        {
            entity.Serialize(writer);
        }
    }

    public static WorldSnapshotMessage Deserialize(NetPacketReader reader)
    {
        var serverTick = reader.GetUInt();
        var lastProcessedInputTick = reader.GetUInt();
        var entityCount = reader.GetUShort();
        var entities = new List<EntityState>(entityCount);

        for (var i = 0; i < entityCount; i++)
        {
            entities.Add(EntityState.Deserialize(reader));
        }

        return new WorldSnapshotMessage(serverTick, lastProcessedInputTick, entities);
    }
}

/// <summary>
/// Compact world state for one entity inside a snapshot.
/// </summary>
public sealed class EntityState
{
    /// <summary>
    /// Gets the entity ID.
    /// </summary>
    public int EntityId { get; }

    /// <summary>
    /// Gets the world X position.
    /// </summary>
    public float X { get; }

    /// <summary>
    /// Gets the world Y position.
    /// </summary>
    public float Y { get; }

    /// <summary>
    /// Gets the world Z position.
    /// </summary>
    public float Z { get; }

    /// <summary>
    /// Gets the Y rotation.
    /// </summary>
    public float RotationY { get; }

    /// <summary>
    /// Creates one entity snapshot entry.
    /// </summary>
    public EntityState(int entityId, float x, float y, float z, float rotationY)
    {
        EntityId = entityId;
        X = x;
        Y = y;
        Z = z;
        RotationY = rotationY;
    }

    /// <summary>
    /// Writes this entity state into the current packet.
    /// </summary>
    public void Serialize(NetDataWriter writer)
    {
        writer.Put(EntityId);
        writer.Put(X);
        writer.Put(Y);
        writer.Put(Z);
        writer.Put(RotationY);
    }

    /// <summary>
    /// Reads one entity state from the packet.
    /// </summary>
    public static EntityState Deserialize(NetPacketReader reader)
    {
        var entityId = reader.GetInt();
        var x = reader.GetFloat();
        var y = reader.GetFloat();
        var z = reader.GetFloat();
        var rotationY = reader.GetFloat();
        return new EntityState(entityId, x, y, z, rotationY);
    }
}
