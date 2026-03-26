using Rex.Shared.Utility;

namespace Rex.Shared.Simulation;

/// <summary>Tracks which entities changed each tick for delta snapshots.</summary>
public sealed class DirtyTracker
{
    private readonly TickRingBuffer<HashSet<int>> _dirtyBuffer;

    public DirtyTracker(int bufferSize = 256)
    {
        // Each tick gets a HashSet of entity ids that changed that tick.
        _dirtyBuffer = new TickRingBuffer<HashSet<int>>(bufferSize, static () => new HashSet<int>());
    }

    /// <summary>Record that <paramref name="entityId"/> changed during <paramref name="tick"/>.</summary>
    public void MarkDirty(int entityId, uint tick)
    {
        PrepareSlot(tick).Value.Add(entityId);
    }

    /// <summary>Call at tick start so each slot only holds current-tick dirt.</summary>
    public void ClearTick(uint tick)
    {
        PrepareSlot(tick).Value.Clear();
    }

    /// <summary>Returns null if the range is too old (caller should send full state).</summary>
    public HashSet<int>? GetDirtyEntities(uint fromTick, uint toTick)
    {
        if (toTick <= fromTick)
        {
            return new HashSet<int>();
        }

        var range = toTick - fromTick;
        if (range >= (uint)_dirtyBuffer.Capacity)
        {
            return null;
        }

        var result = new HashSet<int>();
        // Walk each tick in the ack gap. Skip empty or stale ring slots (wrong tick means reused bucket).
        for (var tick = fromTick + 1; tick <= toTick; tick++)
        {
            var slot = _dirtyBuffer.GetSlot(tick);
            if (!slot.IsAssigned || slot.Tick != tick)
            {
                continue;
            }

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
        // Ring reuse. If the bucket still holds an old tick, reset it.
        if (!slot.IsAssigned || slot.Tick != tick)
        {
            slot.Tick = tick;
            slot.IsAssigned = true;
            slot.Value.Clear();
        }

        return slot;
    }
}
