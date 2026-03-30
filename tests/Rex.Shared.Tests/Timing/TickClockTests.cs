using Rex.Shared.Timing;

namespace Rex.Shared.Tests.Timing;

// Fixed rate sim clock and interpolation alpha.
public sealed class TickClockTests
{
    [Fact]
    // New clock starts at tick zero with matching interval.
    public void Constructor_sets_rate_and_interval()
    {
        var clock = new TickClock(60);
        Assert.Equal(60, clock.TickRate);
        Assert.Equal(1.0 / 60.0, clock.TickInterval, 12);
        Assert.Equal(0u, clock.CurrentTick);
        Assert.Equal(0.0, clock.ElapsedTime);
        Assert.Equal(0f, clock.Alpha);
    }

    [Fact]
    // Each increment adds one tick length to elapsed time.
    public void IncrementTick_advances_tick_and_elapsed()
    {
        var clock = new TickClock(30);
        clock.IncrementTick();
        clock.IncrementTick();
        Assert.Equal(2u, clock.CurrentTick);
        Assert.Equal(2.0 / 30.0, clock.ElapsedTime, 12);
    }

    [Fact]
    // Alpha stores the render blend factor.
    public void SetAlpha_round_trips()
    {
        var clock = new TickClock(60);
        clock.SetAlpha(0.75f);
        Assert.Equal(0.75f, clock.Alpha);
    }
}
