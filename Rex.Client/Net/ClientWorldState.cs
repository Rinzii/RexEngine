using Rex.Shared.Net.Messages;
using Rex.Shared.Numerics;

namespace Rex.Client.Net;

/// <summary>
/// Stores two snapshots for interpolation between server ticks.
/// </summary>
public sealed class ClientWorldState
{
    private WorldSnapshotMessage? _previousSnapshot;
    private WorldSnapshotMessage? _currentSnapshot;

    /// <summary>Latest world snapshot from the server.</summary>
    public WorldSnapshotMessage? CurrentSnapshot => _currentSnapshot;

    /// <summary>Server tick from <see cref="CurrentSnapshot"/>, or 0 if none.</summary>
    public uint LastServerTick => _currentSnapshot?.ServerTick ?? 0;

    public void ApplySnapshot(WorldSnapshotMessage snapshot)
    {
        // Shift buffers so GetInterpolatedState can lerp between last and new server ticks.
        _previousSnapshot = _currentSnapshot;
        _currentSnapshot = snapshot;
    }

    /// <summary>Lerps entity states between previous and current snapshot.</summary>
    public IReadOnlyList<EntityState> GetInterpolatedState(float alpha)
    {
        if (_currentSnapshot == null)
        {
            return [];
        }

        if (_previousSnapshot == null)
        {
            return _currentSnapshot.Entities;
        }

        var result = new List<EntityState>();
        var previousEntities = new Dictionary<int, EntityState>();

        foreach (var entity in _previousSnapshot.Entities)
        {
            previousEntities[entity.EntityId] = entity;
        }

        foreach (var current in _currentSnapshot.Entities)
        {
            if (previousEntities.TryGetValue(current.EntityId, out var previous))
            {
                result.Add(EntityStateInterpolation.Lerp(previous, current, alpha));
            }
            else
            {
                // First time we see this entity in the older snapshot. No lerp, use current.
                result.Add(current);
            }
        }

        return result;
    }
}
