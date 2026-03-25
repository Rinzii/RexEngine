using Rex.Shared.Net.Messages;

namespace Rex.Client.Net;

/// <summary>
/// Stores two snapshots for interpolation between ticks.
/// </summary>
public sealed class ClientWorldState
{
    private WorldSnapshotMessage? _previousSnapshot;
    private WorldSnapshotMessage? _currentSnapshot;

    public WorldSnapshotMessage? CurrentSnapshot => _currentSnapshot;
    public uint LastServerTick => _currentSnapshot?.ServerTick ?? 0;

    public void ApplySnapshot(WorldSnapshotMessage snapshot)
    {
        _previousSnapshot = _currentSnapshot;
        _currentSnapshot = snapshot;
    }

    /// <summary>
    /// Lerps entity states between previous and current snapshot. Alpha 0..1.
    /// </summary>
    public IReadOnlyList<EntityState> GetInterpolatedState(float alpha)
    {
        if (_currentSnapshot == null)
            return Array.Empty<EntityState>();

        if (_previousSnapshot == null)
            return _currentSnapshot.Entities;

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
                // Interpolate between previous and current state.
                var x = previous.X + (current.X - previous.X) * alpha;
                var y = previous.Y + (current.Y - previous.Y) * alpha;
                var z = previous.Z + (current.Z - previous.Z) * alpha;
                var rotY = previous.RotationY + (current.RotationY - previous.RotationY) * alpha;
                result.Add(new EntityState(current.EntityId, x, y, z, rotY));
            }
            else
            {
                // New entity, no interpolation possible.
                result.Add(current);
            }
        }

        return result;
    }
}
