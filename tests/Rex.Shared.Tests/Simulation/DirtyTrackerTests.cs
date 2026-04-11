using Rex.Shared.Simulation;

namespace Rex.Shared.Tests.Simulation;

// Dirty entity ids per tick for delta snapshots.
public sealed class DirtyTrackerTests
{
    [Fact]
    // Zero or inverted ranges yield an empty set not null.
    public void GetDirtyEntities_empty_range_returns_empty_set()
    {
        var tracker = new DirtyTracker(32);
        Assert.Empty(tracker.GetDirtyEntities(5, 5)!);
        Assert.Empty(tracker.GetDirtyEntities(7, 3)!);
    }

    [Fact]
    // Range wider than the ring buffer returns null.
    public void GetDirtyEntities_range_at_or_beyond_capacity_returns_null()
    {
        var tracker = new DirtyTracker(8);
        Assert.Null(tracker.GetDirtyEntities(0, 8));
        Assert.Null(tracker.GetDirtyEntities(0, 100));
    }

    [Fact]
    // Union of ids across ticks in the open interval.
    public void MarkDirty_aggregates_entities_across_tick_window()
    {
        var tracker = new DirtyTracker(64);
        tracker.MarkDirty(10, 1);
        tracker.MarkDirty(20, 1);
        tracker.MarkDirty(30, 2);

        HashSet<int>? dirty = tracker.GetDirtyEntities(0, 2);
        Assert.NotNull(dirty);
        Assert.Contains(10, dirty!);
        Assert.Contains(20, dirty);
        Assert.Contains(30, dirty);
    }

    [Fact]
    // ClearTick removes prior marks before new marks on the same tick.
    public void ClearTick_clears_slot_for_that_tick()
    {
        var tracker = new DirtyTracker(64);
        tracker.MarkDirty(1, 5);
        tracker.ClearTick(5);
        tracker.MarkDirty(2, 5);

        HashSet<int>? dirty = tracker.GetDirtyEntities(4, 5);
        Assert.NotNull(dirty);
        Assert.Contains(2, dirty!);
        Assert.DoesNotContain(1, dirty);
    }
}
