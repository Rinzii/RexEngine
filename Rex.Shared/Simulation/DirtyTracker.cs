using Rex.Shared.Utility;

namespace Rex.Shared.Simulation;

/// <summary>
/// Records which entity ids changed on each simulation tick so networking can send deltas across an ack window.
/// </summary>
/// <remarks>
/// Backed by a <see cref="TickRingBuffer{T}"/> of hash sets per tick. When the tick span in
/// <see cref="GetDirtyEntities"/> reaches the ring capacity the method returns null so the caller can send a full snapshot instead of a partial delta.
/// </remarks>
public sealed class DirtyTracker
{
    private readonly TickRingBuffer<HashSet<int>> _dirtyBuffer;

    /// <summary>
    /// Builds a ring with one reusable hash set per slot.
    /// </summary>
    /// <param name="bufferSize">Number of ticks kept. Must be larger than the longest ack gap you want to express as deltas.</param>
    public DirtyTracker(int bufferSize = 256)
    {
        // One set per ring slot. Slots are rebound in PrepareSlot when the tick advances.
        _dirtyBuffer = new TickRingBuffer<HashSet<int>>(bufferSize, static () => []);
    }

    /// <summary>Adds <paramref name="entityId"/> to the dirty set for <paramref name="tick"/>.</summary>
    public void MarkDirty(int entityId, uint tick)
    {
        _ = PrepareSlot(tick).Value.Add(entityId);
    }

    /// <summary>Clears the set for <paramref name="tick"/> at tick boundary before new writes.</summary>
    public void ClearTick(uint tick)
    {
        PrepareSlot(tick).Value.Clear();
    }

    /// <summary>
    /// Unions every dirty id for ticks strictly after <paramref name="fromTick"/> through <paramref name="toTick"/>.
    /// </summary>
    /// <param name="fromTick">Peer baseline. Usually the last tick they already applied.</param>
    /// <param name="toTick">Inclusive end of the range on the authority.</param>
    /// <returns>
    /// Empty set when <paramref name="toTick"/> is not greater than <paramref name="fromTick"/>. Null when the span is
    /// not fully covered by the ring so the caller should send a full state instead of a delta.
    /// </returns>
    public HashSet<int>? GetDirtyEntities(uint fromTick, uint toTick)
    {
        if (toTick <= fromTick)
        {
            return [];
        }

        uint range = toTick - fromTick;
        if (range >= (uint)_dirtyBuffer.Capacity)
        {
            return null;
        }

        var result = new HashSet<int>();
        // Only trust a slot when Entry.Tick matches. Otherwise, the bucket was recycled for another tick.
        for (uint tick = fromTick + 1; tick <= toTick; tick++)
        {
            TickRingBuffer<HashSet<int>>.Entry slot = _dirtyBuffer.GetSlot(tick);
            if (!slot.IsAssigned || slot.Tick != tick)
            {
                continue;
            }

            foreach (int entityId in slot.Value)
            {
                _ = result.Add(entityId);
            }
        }

        return result;
    }

    /// <summary>
    /// Binds the ring slot for <paramref name="tick"/> and clears it when the slot still carried an older tick.
    /// </summary>
    private TickRingBuffer<HashSet<int>>.Entry PrepareSlot(uint tick)
    {
        TickRingBuffer<HashSet<int>>.Entry slot = _dirtyBuffer.GetSlot(tick);
        if (!slot.IsAssigned || slot.Tick != tick)
        {
            slot.Tick = tick;
            slot.IsAssigned = true;
            slot.Value.Clear();
        }

        return slot;
    }
}
