using Rex.Shared.Timing;
using SystemMath = System.Math;

namespace Rex.Shared.Tests.Timing;

// Fixed timestep drain from a wall clock accumulator.
public sealed class PhasedLoopTests
{
    [Fact]
    // Fifty ms at sixty Hz runs three fixed steps and leaves a partial accumulator.
    public void RunFixedSteps_runs_while_accumulator_covers_intervals()
    {
        var clock = new TickClock(60);
        double acc = 0.0;
        int steps = 0;
        int count = PhasedLoop.RunFixedSteps(clock, ref acc, 0.05, () => steps++);

        Assert.Equal(3, count);
        Assert.Equal(3, steps);
        Assert.Equal(3u, clock.CurrentTick);
        Assert.True(acc < clock.TickInterval);
    }

    [Fact]
    // Huge frame delta is clamped before adding to the accumulator.
    public void RunFixedSteps_clamps_huge_frame_time()
    {
        var clock = new TickClock(60);
        double acc = 0.0;
        int steps = 0;
        _ = PhasedLoop.RunFixedSteps(clock, ref acc, 10.0, () => steps++, maxFrameSeconds: 0.25f);

        int expectedSteps = (int)SystemMath.Floor((0.25 / clock.TickInterval) + 1e-9);
        Assert.Equal(expectedSteps, steps);
    }
}
