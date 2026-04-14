using LiteNetLib.Utils;
using Rex.Shared.Analyzers;
using Rex.Shared.GameStates;
using Rex.Shared.Net;
using Rex.Shared.Net.Replication;

namespace Rex.Sandbox.Shared.Net.Messages;

/// <summary>
/// Server snapshot for the current Sandbox world state.
/// </summary>
public sealed class WorldSnapshotMessage : INetMessage, IRemovablePartialGameState<int, ReplicatedEntityState>
{
    public const ushort Id = 5;

    public WorldSnapshotMessage(uint serverTick, uint lastProcessedInputTick, IReadOnlyList<ReplicatedEntityState> entities,
        bool isFullSnapshot, IReadOnlyList<int>? removedEntityIds = null)
    {
        ServerTick = serverTick;
        LastProcessedInputTick = lastProcessedInputTick;
        Entities = entities;
        IsFullSnapshot = isFullSnapshot;
        RemovedKeys = removedEntityIds ?? [];
    }

    public uint ServerTick { get; }
    public uint LastProcessedInputTick { get; }
    public IReadOnlyList<ReplicatedEntityState> Entities { get; }
    public bool IsFullSnapshot { get; }
    /// <summary>Gets the stable entity ids removed by this snapshot.</summary>
    public IReadOnlyList<int> RemovedKeys { get; }

    public ushort MessageId => Id;
    public MessageGroup Group => MessageGroup.Entity;

    public void Serialize(NetDataWriter writer)
    {
        NetMessageRegistry.WriteHeader(writer, Id);
        writer.Put(ServerTick);
        writer.Put(LastProcessedInputTick);
        writer.Put(IsFullSnapshot);
        // Wire layout is removed ids first then entity blocks with length prefixed component blobs.
        writer.Put((ushort)RemovedKeys.Count);
        foreach (int removedEntityId in RemovedKeys)
        {
            writer.Put(removedEntityId);
        }

        writer.Put((ushort)Entities.Count);

        foreach (ReplicatedEntityState entity in Entities)
        {
            writer.Put(entity.EntityId);
            writer.Put((ushort)entity.Components.Count);
            foreach (ReplicatedComponentState component in entity.Components)
            {
                writer.Put(component.ComponentId);
                writer.Put(component.Payload.Length);
                writer.Put(component.Payload);
            }
        }
    }

    public static WorldSnapshotMessage Deserialize(NetDataReader reader)
    {
        uint serverTick = reader.GetUInt();
        uint lastProcessedInputTick = reader.GetUInt();
        bool isFullSnapshot = reader.GetBool();
        ushort removedEntityCount = reader.GetUShort();
        var removedEntityIds = new List<int>(removedEntityCount);
        for (int removedIndex = 0; removedIndex < removedEntityCount; removedIndex++)
        {
            removedEntityIds.Add(reader.GetInt());
        }

        ushort entityCount = reader.GetUShort();
        var entities = new List<ReplicatedEntityState>(entityCount);

        for (int i = 0; i < entityCount; i++)
        {
            int entityId = reader.GetInt();
            ushort componentCount = reader.GetUShort();
            var components = new List<ReplicatedComponentState>(componentCount);
            for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                int componentId = reader.GetInt();
                int payloadLength = reader.GetInt();
                byte[] payload = new byte[payloadLength];
                for (int payloadIndex = 0; payloadIndex < payloadLength; payloadIndex++)
                {
                    payload[payloadIndex] = reader.GetByte();
                }

                components.Add(new ReplicatedComponentState(componentId, payload));
            }

            entities.Add(new ReplicatedEntityState(entityId, components));
        }

        return new WorldSnapshotMessage(serverTick, lastProcessedInputTick, entities, isFullSnapshot, removedEntityIds);
    }
}

/// <summary>
/// Client asks the server for a full authoritative world snapshot.
/// </summary>
public sealed class RequestFullStateMessage : INetMessage
{
    public const ushort Id = 9;

    public RequestFullStateMessage(uint lastAppliedServerTick)
    {
        LastAppliedServerTick = lastAppliedServerTick;
    }

    public uint LastAppliedServerTick { get; }

    public ushort MessageId => Id;
    public MessageGroup Group => MessageGroup.Core;

    public void Serialize(NetDataWriter writer)
    {
        NetMessageRegistry.WriteHeader(writer, Id);
        writer.Put(LastAppliedServerTick);
    }

    public static RequestFullStateMessage Deserialize(NetDataReader reader)
    {
        uint lastAppliedServerTick = reader.GetUInt();
        return new RequestFullStateMessage(lastAppliedServerTick);
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
    /// Reads entity fields from the network reader.
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

    public EntitySpawnMessage(uint serverTick, int entityId, Guid ownerClientId, [ForbidLiteral] string entityType,
        float x, float y, float z, float rotationY)
    {
        ServerTick = serverTick;
        EntityId = entityId;
        OwnerClientId = ownerClientId;
        EntityType = entityType;
        X = x;
        Y = y;
        Z = z;
        RotationY = rotationY;
    }

    public uint ServerTick { get; }
    public int EntityId { get; }
    public Guid OwnerClientId { get; }
    public string EntityType { get; }
    public float X { get; }
    public float Y { get; }
    public float Z { get; }
    public float RotationY { get; }
    public ushort MessageId => Id;
    public MessageGroup Group => MessageGroup.EntityEvent;

    public void Serialize(NetDataWriter writer)
    {
        NetMessageRegistry.WriteHeader(writer, Id);
        writer.Put(ServerTick);
        writer.Put(EntityId);
        writer.PutGuid(OwnerClientId);
        writer.Put(EntityType);
        writer.Put(X);
        writer.Put(Y);
        writer.Put(Z);
        writer.Put(RotationY);
    }

    public static EntitySpawnMessage Deserialize(NetDataReader reader)
    {
        uint serverTick = reader.GetUInt();
        int entityId = reader.GetInt();
        Guid ownerClientId = reader.ReadGuid();
        string entityType = reader.GetString();
        float x = reader.GetFloat();
        float y = reader.GetFloat();
        float z = reader.GetFloat();
        float rotationY = reader.GetFloat();
        return new EntitySpawnMessage(serverTick, entityId, ownerClientId, entityType, x, y, z, rotationY);
    }
}

/// <summary>
/// Reliable notice that an entity left the Sandbox replicated world.
/// </summary>
public sealed class EntityDestroyMessage : INetMessage
{
    public const ushort Id = 7;

    public EntityDestroyMessage(uint serverTick, int entityId)
    {
        ServerTick = serverTick;
        EntityId = entityId;
    }

    public uint ServerTick { get; }
    public int EntityId { get; }

    public ushort MessageId => Id;
    public MessageGroup Group => MessageGroup.EntityEvent;

    public void Serialize(NetDataWriter writer)
    {
        NetMessageRegistry.WriteHeader(writer, Id);
        writer.Put(ServerTick);
        writer.Put(EntityId);
    }

    /// <summary>
    /// Reads this message from the network reader.
    /// </summary>
    public static EntityDestroyMessage Deserialize(NetDataReader reader)
    {
        uint serverTick = reader.GetUInt();
        int entityId = reader.GetInt();
        return new EntityDestroyMessage(serverTick, entityId);
    }
}
