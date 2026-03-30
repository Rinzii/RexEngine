using Rex.Shared.Timing;

namespace Rex.Shared.Tests.Timing;

// One variable-rate frame after fixed steps.
public sealed class FrameContextTests
{
    [Fact]
    // Scaled deltas multiply wall deltas by time scale.
    public void Scaled_and_smooth_deltas_respect_time_scale()
    {
        var clock = new TickClock(60);
        clock.SetAlpha(0.25f);
        const float unscaled = 0.02f;
        const float smooth = 0.018f;
        const float scale = 0.5f;

        var ctx = new FrameContext(clock, unscaled, smooth, scale, 2, 0.25f, 10ul, 123.4);

        Assert.Equal(unscaled * scale, ctx.ScaledDeltaTime);
        Assert.Equal(smooth * scale, ctx.SmoothDeltaTime);
        Assert.Equal(2, ctx.FixedStepsThisFrame);
        Assert.Equal(0.25f, ctx.InterpolationAlpha);
        Assert.Equal(10ul, ctx.FrameIndex);
        Assert.Equal(123.4, ctx.ElapsedRealtimeSeconds);
        Assert.Equal((float)clock.TickInterval, ctx.FixedDeltaTime);
    }
}
