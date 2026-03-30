using Rex.Shared.Numerics;

namespace Rex.Shared.Tests.Numerics;

public sealed class AngleMathTests
{
    [Fact]
    public void DeltaAngleDegrees_uses_shortest_arc()
    {
        Assert.Equal(-10f, AngleMath.DeltaAngleDegrees(0f, 350f));
        Assert.Equal(10f, AngleMath.DeltaAngleDegrees(350f, 0f));
    }

    [Fact]
    public void LerpAngleDegrees_interpolates_along_shortest_arc()
    {
        Assert.Equal(45f, AngleMath.LerpAngleDegrees(0f, 90f, 0.5f));
        Assert.Equal(360f, AngleMath.LerpAngleDegrees(350f, 10f, 0.5f));
    }

    [Fact]
    public void RepeatDegrees_wraps_into_zero_period()
    {
        Assert.Equal(350f, AngleMath.RepeatDegrees(-10f, 360f));
        Assert.Equal(0f, AngleMath.RepeatDegrees(360f, 360f));
    }
}
