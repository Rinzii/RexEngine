using Rex.Shared.Analyzers;
using Rex.Shared.Net.Messages;

namespace Rex.Shared.Simulation;

/// <summary>
/// Authoritative game world. Owns entity state, processes inputs, and advances the simulation.
/// Used by both the dedicated server (with DirtyTracker) and standalone clients (without).
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

    public int SpawnEntity(int ownerClientId, [ForbidLiteral] string entityType, float x, float y, float z)
    {
        // IDs are monotonic for now. ownerClientId and entityType reserved for replication rules later.
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
            return;

        const float moveSpeed = 5.0f;
        // MoveXZ and yaw only. Y unchanged (no jump or fly yet).
        var newX = current.X + input.MoveX * moveSpeed;
        var newZ = current.Z + input.MoveY * moveSpeed;
        var newRotY = input.LookY;

        _entities[entityId] = new EntityState(entityId, newX, current.Y, newZ, newRotY);
        _dirtyTracker?.MarkDirty(entityId, _currentTick);
    }

    public void Tick(float deltaTime)
    {
        // deltaTime reserved for physics and timers. Only the tick counter runs today.
        _currentTick++;
    }

    /// <summary>Builds a full snapshot of all entities. Used by the server for broadcasting.</summary>
    public WorldSnapshotMessage BuildSnapshot(uint serverTick, uint lastProcessedInputTick)
    {
        var entities = new List<EntityState>(_entities.Values);
        return new WorldSnapshotMessage(serverTick, lastProcessedInputTick, entities);
    }

    /// <summary>Builds a delta snapshot of only dirty entities. Used by the server for broadcasting.</summary>
    public WorldSnapshotMessage BuildDeltaSnapshot(uint serverTick, uint lastProcessedInputTick, HashSet<int> dirtyEntityIds)
    {
        var entities = new List<EntityState>();
        foreach (var entityId in dirtyEntityIds)
        {
            if (_entities.TryGetValue(entityId, out var state))
                entities.Add(state);
        }
        return new WorldSnapshotMessage(serverTick, lastProcessedInputTick, entities);
    }
}
