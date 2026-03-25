using Rex.Shared.Utility;

namespace Rex.Shared.Server;

/// <summary>
/// Ring buffer tracking which entities changed each tick for delta snapshots.
/// </summary>
public sealed class DirtyTracker
{
    private readonly TickRingBuffer<HashSet<int>> _dirtyBuffer;

    public DirtyTracker(int bufferSize = 256)
    {
        _dirtyBuffer = new TickRingBuffer<HashSet<int>>(bufferSize, static () => new HashSet<int>());
    }

    public void MarkDirty(int entityId, uint tick)
    {
        var slot = PrepareSlot(tick);
        slot.Value.Add(entityId);
    }

    public void ClearTick(uint tick)
    {
        PrepareSlot(tick).Value.Clear();
    }

    /// <summary>
    /// Returns null if the range exceeds buffer size (caller should send full state).
    /// </summary>
    public HashSet<int>? GetDirtyEntities(uint fromTick, uint toTick)
    {
        if (toTick <= fromTick)
            return new HashSet<int>();

        var range = toTick - fromTick;
        if (range >= (uint)_dirtyBuffer.Capacity)
            return null; // Too old, need full state.

        var result = new HashSet<int>();
        for (var tick = fromTick + 1; tick <= toTick; tick++)
        {
            var slot = _dirtyBuffer.GetSlot(tick);
            if (!slot.IsAssigned || slot.Tick != tick)
                continue;

            foreach (var entityId in slot.Value)
            {
                result.Add(entityId);
            }
        }

        return result;
    }

    private TickRingBuffer<HashSet<int>>.Entry PrepareSlot(uint tick)
    {
        var slot = _dirtyBuffer.GetSlot(tick);
        if (!slot.IsAssigned || slot.Tick != tick)
        {
            slot.Tick = tick;
            slot.IsAssigned = true;
            slot.Value.Clear();
        }

        return slot;
    }
}
