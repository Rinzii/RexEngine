using Rex.Shared.Simulation;
using Rex.Sandbox.Shared.Net.Messages;

namespace Rex.Sandbox.Shared.Simulation;

/// <summary>
/// Sandbox-owned simulation tuning shared between the authoritative world and client prediction.
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
    private readonly Dictionary<int, EntityState> _entities = new();
    private readonly DirtyTracker? _dirtyTracker;
    private int _nextEntityId = 1;
    private uint _currentTick;

    public IReadOnlyDictionary<int, EntityState> Entities => _entities;
    public uint CurrentTick => _currentTick;

    public GameWorld(DirtyTracker? dirtyTracker = null)
    {
        _dirtyTracker = dirtyTracker;
    }

    public int SpawnEntity(Guid ownerClientId, string entityType, float x, float y, float z)
    {
        var entityId = _nextEntityId++;
        _entities[entityId] = new EntityState(entityId, x, y, z, 0f);
        _dirtyTracker?.MarkDirty(entityId, _currentTick);
        return entityId;
    }

    public void DestroyEntity(int entityId)
    {
        _entities.Remove(entityId);
    }

    public void ProcessInput(int entityId, PlayerInputMessage input)
    {
        if (!_entities.TryGetValue(entityId, out var current))
        {
            return;
        }

        var newX = MathF.FusedMultiplyAdd(input.MoveX, MovementConstants.PlanarUnitsPerInputTick, current.X);
        var newZ = MathF.FusedMultiplyAdd(input.MoveY, MovementConstants.PlanarUnitsPerInputTick, current.Z);
        var newRotY = input.LookY;

        _entities[entityId] = new EntityState(entityId, newX, current.Y, newZ, newRotY);
        _dirtyTracker?.MarkDirty(entityId, _currentTick);
    }

    public void Tick(float deltaTime)
    {
        _currentTick++;
    }

    public WorldSnapshotMessage BuildSnapshot(uint serverTick, uint lastProcessedInputTick)
    {
        var entities = new List<EntityState>(_entities.Values);
        return new WorldSnapshotMessage(serverTick, lastProcessedInputTick, entities);
    }

    public WorldSnapshotMessage BuildDeltaSnapshot(uint serverTick, uint lastProcessedInputTick,
        HashSet<int> dirtyEntityIds)
    {
        var entities = new List<EntityState>();
        foreach (var entityId in dirtyEntityIds)
        {
            if (_entities.TryGetValue(entityId, out var state))
            {
                entities.Add(state);
            }
        }

        return new WorldSnapshotMessage(serverTick, lastProcessedInputTick, entities);
    }
}
