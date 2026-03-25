using Rex.Shared.Net.Messages;

namespace Rex.Shared.Server;

public sealed class GameWorld
{
    private readonly Dictionary<int, EntityState> _entities = new();
    private readonly DirtyTracker _dirtyTracker;
    private int _nextEntityId = 1;
    private uint _currentTick;

    public GameWorld(DirtyTracker dirtyTracker)
    {
        _dirtyTracker = dirtyTracker;
    }

    public int SpawnEntity(int ownerClientId, string entityType, float x, float y, float z)
    {
        var entityId = _nextEntityId++;
        _entities[entityId] = new EntityState(entityId, x, y, z, 0f);
        _dirtyTracker.MarkDirty(entityId, _currentTick);
        return entityId;
    }

    public void DestroyEntity(int entityId)
    {
        _entities.Remove(entityId);
    }

    public void ProcessInput(int clientId, PlayerInputMessage input)
    {
        if (!_entities.TryGetValue(clientId, out var current))
            return;

        const float moveSpeed = 5.0f;
        var newX = current.X + input.MoveX * moveSpeed;
        var newZ = current.Z + input.MoveY * moveSpeed;
        var newRotY = input.LookY;

        _entities[clientId] = new EntityState(clientId, newX, current.Y, newZ, newRotY);
        _dirtyTracker.MarkDirty(clientId, _currentTick);
    }

    public void Tick(float deltaTime)
    {
        _currentTick++;
        // Future: physics, AI, game logic.
    }

    public WorldSnapshotMessage BuildSnapshot(uint serverTick, uint lastProcessedInputTick)
    {
        var entities = new List<EntityState>(_entities.Values);
        return new WorldSnapshotMessage(serverTick, lastProcessedInputTick, entities);
    }

    /// <summary>
    /// Only includes entities in the dirty set.
    /// </summary>
    public WorldSnapshotMessage BuildDeltaSnapshot(uint serverTick, uint lastProcessedInputTick, HashSet<int> dirtyEntityIds)
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

    public IReadOnlyDictionary<int, EntityState> Entities => _entities;
}
