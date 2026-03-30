using Rex.Shared.Utility;

namespace Rex.Shared.Tests.Utility;

// Ring slots keyed by tick modulo capacity.
public sealed class TickRingBufferTests
{
    [Fact]
    // Zero or negative capacity throws.
    public void Constructor_rejects_non_positive_capacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TickRingBuffer<int>(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TickRingBuffer<int>(-1));
    }

    [Fact]
    // Null value factory throws.
    public void Constructor_rejects_null_factory()
    {
        Assert.Throws<ArgumentNullException>(() => new TickRingBuffer<int>(4, null!));
    }

    [Fact]
    // Same physical slot for tick and tick plus capacity.
    public void GetSlot_uses_tick_modulo_capacity()
    {
        var buffer = new TickRingBuffer<int>(4);
        Assert.Same(buffer.GetSlot(0), buffer.GetSlotAt(0));
        Assert.Same(buffer.GetSlot(4), buffer.GetSlotAt(0));
        Assert.Same(buffer.GetSlot(5), buffer.GetSlotAt(1));
    }

    [Fact]
    // Entry Tick Value and IsAssigned are writable.
    public void Entry_properties_round_trip()
    {
        var buffer = new TickRingBuffer<string>(2);
        var slot = buffer.GetSlot(1);
        slot.Value = "a";
        slot.Tick = 99;
        slot.IsAssigned = true;

        Assert.Equal("a", slot.Value);
        Assert.Equal(99u, slot.Tick);
        Assert.True(slot.IsAssigned);
    }
}
