using Rex.Shared.Timing;

namespace Rex.Shared.Tests.Timing;

// Exponential smoothing for frame delta seconds.
public sealed class DeltaTimeSmootherTests
{
    [Fact]
    // First good sample becomes the smooth value.
    public void First_valid_sample_seeds_smooth_value()
    {
        var s = new DeltaTimeSmoother();
        float v = s.Next(1f / 60f);
        Assert.InRange(v, 0.015f, 0.018f);
    }

    [Fact]
    // Bad deltas fall back to prior smooth or about one sixtieth of a second.
    public void Invalid_raw_delta_returns_prior_smooth_or_default_60fps()
    {
        var s = new DeltaTimeSmoother();
        Assert.InRange(s.Next(-1f), 0.015f, 0.018f);
        Assert.InRange(s.Next(float.NaN), 0.015f, 0.018f);
        Assert.InRange(s.Next(float.PositiveInfinity), 0.015f, 0.018f);

        s.Reset();
        float fallback = s.Next(0f);
        Assert.InRange(fallback, 0.015f, 0.018f);
    }

    [Fact]
    // Reset clears state so the next sample seeds again.
    public void Reset_allows_fresh_seed()
    {
        var s = new DeltaTimeSmoother();
        _ = s.Next(0.1f);
        s.Reset();
        float v = s.Next(1f / 30f);
        Assert.InRange(v, 0.03f, 0.04f);
    }
}
