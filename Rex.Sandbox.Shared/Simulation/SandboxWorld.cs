using Rex.Sandbox.Shared.Net.Messages;
using Rex.Shared.Simulation;

namespace Rex.Sandbox.Shared.Simulation;

/// <summary>
/// Simulation tuning for the Sandbox sample, shared between the authoritative world and client prediction.
/// </summary>
public static class MovementConstants
{
    public const float PlanarUnitsPerInputTick = 5f;
}

/// <summary>
/// Stable Sandbox entity type names shared by simulation and replication.
/// A future external game repository would own its own equivalents.
/// </summary>
public static class EntityTypeIds
{
    public const string Player = "player";
}

/// <summary>
/// Authoritative Sandbox world. This demo simulation stays outside the reusable engine layer.
/// </summary>
public sealed class GameWorld
{
    private readonly DirtyTracker? _dirtyTracker;
    private readonly Dictionary<int, EntityState> _entities = [];
    private int _nextEntityId = 1;

    public GameWorld(DirtyTracker? dirtyTracker = null)
    {
        _dirtyTracker = dirtyTracker;
    }

    public IReadOnlyDictionary<int, EntityState> Entities => _entities;
    public uint CurrentTick { get; private set; }

    public int SpawnEntity(Guid ownerClientId, string entityType, float x, float y, float z)
    {
        _ = ownerClientId;
        _ = entityType;
        int entityId = _nextEntityId++;
        _entities[entityId] = new EntityState(entityId, x, y, z, 0f);
        _dirtyTracker?.MarkDirty(entityId, CurrentTick);
        return entityId;
    }

    public void DestroyEntity(int entityId)
    {
        _ = _entities.Remove(entityId);
    }

    public void ProcessInput(int entityId, PlayerInputMessage input)
    {
        if (!_entities.TryGetValue(entityId, out EntityState? current))
        {
            return;
        }

        float newX = MathF.FusedMultiplyAdd(input.MoveX, MovementConstants.PlanarUnitsPerInputTick, current.X);
        float newZ = MathF.FusedMultiplyAdd(input.MoveY, MovementConstants.PlanarUnitsPerInputTick, current.Z);
        float newRotY = input.LookY;

        _entities[entityId] = new EntityState(entityId, newX, current.Y, newZ, newRotY);
        _dirtyTracker?.MarkDirty(entityId, CurrentTick);
    }

    public void Tick(float deltaTime)
    {
        _ = deltaTime;
        CurrentTick++;
    }

    public WorldSnapshotMessage BuildSnapshot(uint serverTick, uint lastProcessedInputTick)
    {
        List<EntityState> entities = [.. _entities.Values];
        return new WorldSnapshotMessage(serverTick, lastProcessedInputTick, entities);
    }

    public WorldSnapshotMessage BuildDeltaSnapshot(uint serverTick, uint lastProcessedInputTick,
        HashSet<int> dirtyEntityIds)
    {
        List<EntityState> entities = [];
        foreach (int entityId in dirtyEntityIds)
        {
            if (_entities.TryGetValue(entityId, out EntityState? state))
            {
                entities.Add(state);
            }
        }

        return new WorldSnapshotMessage(serverTick, lastProcessedInputTick, entities);
    }
}
