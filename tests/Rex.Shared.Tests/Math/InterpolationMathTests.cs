using Rex.Shared.Numerics;

namespace Rex.Shared.Tests.Math;

public sealed class InterpolationMathTests
{
    [Fact]
    public void InverseLerp_is_unclamped_and_zero_when_range_collapses()
    {
        Assert.Equal(0.5f, InterpolationMath.InverseLerp(0f, 10f, 5f));
        Assert.Equal(1.5f, InterpolationMath.InverseLerp(0f, 10f, 15f));
        Assert.Equal(0f, InterpolationMath.InverseLerp(3f, 3f, 99f));
    }

    [Fact]
    public void Remap_transfers_linear_ratio()
    {
        Assert.Equal(150f, InterpolationMath.Remap(0f, 10f, 100f, 200f, 5f));
    }

    [Fact]
    public void SmoothStep_clamps_and_eases()
    {
        Assert.Equal(0f, InterpolationMath.SmoothStep(0f, 1f, -1f));
        Assert.Equal(1f, InterpolationMath.SmoothStep(0f, 1f, 2f));
        Assert.Equal(0.5f, InterpolationMath.SmoothStep(0f, 1f, 0.5f));
    }

    [Fact]
    public void SmoothStep_collapsed_edges_are_a_step()
    {
        Assert.Equal(0f, InterpolationMath.SmoothStep(2f, 2f, 1f));
        Assert.Equal(1f, InterpolationMath.SmoothStep(2f, 2f, 3f));
    }
}
