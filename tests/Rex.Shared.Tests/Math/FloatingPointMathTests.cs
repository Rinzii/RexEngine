using Rex.Shared.Numerics;

namespace Rex.Shared.Tests.Numerics;

public sealed class FloatingPointMathTests
{
    [Fact]
    public void IsNearlyEqual_uses_tolerance()
    {
        Assert.True(FloatingPointMath.IsNearlyEqual(1f, 1f + 1e-6f));
        Assert.False(FloatingPointMath.IsNearlyEqual(1f, 1.1f));
    }

    [Fact]
    public void IsNearlyZero_matches_abs_bound()
    {
        Assert.True(FloatingPointMath.IsNearlyZero(1e-6f));
        Assert.False(FloatingPointMath.IsNearlyZero(1f));
    }
}
