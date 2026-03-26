using Rex.Shared.Net.Messages;

namespace Rex.Client.Net;

/// <summary>
/// Stores two snapshots for interpolation between server ticks.
/// </summary>
public sealed class ClientWorldState
{
    private WorldSnapshotMessage? _previousSnapshot;
    private WorldSnapshotMessage? _currentSnapshot;

    public WorldSnapshotMessage? CurrentSnapshot => _currentSnapshot;
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
            return [];

        if (_previousSnapshot == null)
            return _currentSnapshot.Entities;

        var result = new List<EntityState>();
        var previousEntities = new Dictionary<int, EntityState>();

        foreach (var entity in _previousSnapshot.Entities)
            previousEntities[entity.EntityId] = entity;

        foreach (var current in _currentSnapshot.Entities)
            if (previousEntities.TryGetValue(current.EntityId, out var previous))
            {
                var x = previous.X + (current.X - previous.X) * alpha;
                var y = previous.Y + (current.Y - previous.Y) * alpha;
                var z = previous.Z + (current.Z - previous.Z) * alpha;
                var rotY = previous.RotationY + (current.RotationY - previous.RotationY) * alpha;
                result.Add(new EntityState(current.EntityId, x, y, z, rotY));
            }
            else
            {
                // First time we see this entity in the older snapshot. No lerp, use current.
                result.Add(current);
            }

        return result;
    }
}