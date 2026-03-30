using Rex.Shared.Numerics;

namespace Rex.Shared.Tests.Numerics;

public sealed class PeriodicMathTests
{
    [Fact]
    public void Repeat_maps_into_zero_period()
    {
        Assert.Equal(2.5f, PeriodicMath.Repeat(7.5f, 5f));
        Assert.Equal(4f, PeriodicMath.Repeat(-1f, 5f));
        Assert.Equal(350.0, PeriodicMath.Repeat(-10.0, 360.0));
    }

    [Fact]
    public void Repeat_throws_when_period_not_positive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PeriodicMath.Repeat(1f, 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => PeriodicMath.Repeat(1f, -1f));
    }
}
