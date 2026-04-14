using Rex.Shared.Timing;

namespace Rex.Shared.Tests.Timing;

// One variable rate frame after fixed steps.
public sealed class FrameContextTests
{
    [Fact]
    // Scaled deltas multiply wall deltas by time scale.
    public void Scaled_and_smooth_deltas_respect_time_scale()
    {
        var clock = new TickClock(60);
        clock.SetAlpha(0.25f);
        const float Unscaled = 0.02f;
        const float Smooth = 0.018f;
        const float Scale = 0.5f;

        var ctx = new FrameContext(clock, Unscaled, Smooth, Scale, 2, 0.25f, 10ul, 123.4);

        Assert.Equal(Unscaled * Scale, ctx.ScaledDeltaTime);
        Assert.Equal(Smooth * Scale, ctx.SmoothDeltaTime);
        Assert.Equal(2, ctx.FixedStepsThisFrame);
        Assert.Equal(0.25f, ctx.InterpolationAlpha);
        Assert.Equal(10ul, ctx.FrameIndex);
        Assert.Equal(123.4, ctx.ElapsedRealtimeSeconds);
        Assert.Equal((float)clock.TickInterval, ctx.FixedDeltaTime);
    }
}
