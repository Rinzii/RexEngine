using Rex.Shared.Prototypes;
using Rex.Shared.Serialization.Manager.Definition;
using Rex.Shared.Simulation;
using Rex.Shared.Timing;
using Rex.Shared.Utility;

namespace Rex.Shared.Tests.Regression;

// Locks non-network shared library behavior covered by sibling test types.
public sealed class SharedLibraryRegressionTests
{
    [Fact]
    public void Regression_data_definition_tag_lowercases_leading_character()
    {
        Assert.Equal("fooBar", DataDefinitionUtility.AutoGenerateTag("FooBar"));
    }

    [Fact]
    public void Regression_dirty_tracker_empty_tick_range_yields_empty_set()
    {
        var tracker = new DirtyTracker(16);
        Assert.Empty(tracker.GetDirtyEntities(4, 4)!);
    }

    [Fact]
    public void Regression_tick_clock_starts_at_zero_tick()
    {
        var clock = new TickClock(60);
        Assert.Equal(0u, clock.CurrentTick);
        Assert.Equal(60, clock.TickRate);
    }

    [Fact]
    public void Regression_frame_context_scaled_delta_respects_time_scale()
    {
        var clock = new TickClock(60);
        var ctx = new FrameContext(clock, 0.02f, 0.02f, 0.5f, 0, 0f, 0ul, 0.0);

        Assert.Equal(0.01f, ctx.ScaledDeltaTime);
    }

    [Fact]
    public void Regression_phased_loop_three_steps_for_fifty_ms_at_sixty_hz()
    {
        var clock = new TickClock(60);
        var acc = 0.0;
        var steps = 0;
        var count = PhasedLoop.RunFixedSteps(clock, ref acc, 0.05, () => steps++);

        Assert.Equal(3, count);
        Assert.Equal(3, steps);
    }

    [Fact]
    public void Regression_delta_time_smoother_first_sample_in_valid_range()
    {
        var s = new DeltaTimeSmoother();
        Assert.InRange(s.Next(1f / 60f), 0.015f, 0.018f);
    }

    [Fact]
    public void Regression_tick_ring_buffer_slot_reuse_by_modulo()
    {
        var buffer = new TickRingBuffer<int>(4);
        Assert.Same(buffer.GetSlot(0), buffer.GetSlot(4));
    }

    [Fact]
    public void Regression_prototype_name_strips_suffix()
    {
        Assert.Equal("player", PrototypeUtility.CalculatePrototypeName("PlayerPrototype"));
    }
}
