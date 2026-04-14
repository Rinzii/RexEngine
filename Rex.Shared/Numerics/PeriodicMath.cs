namespace Rex.Shared.Numerics;

/// <summary>Wrapping and modular reduction for scalars. The BCL does not expose a single generic repeat helper.</summary>
public static class PeriodicMath
{
    /// <summary>Maps <paramref name="value"/> into [0, <paramref name="period"/>). Requires a positive <paramref name="period"/>.</summary>
    public static float Repeat(float value, float period)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(period, 0f);
        return value - (MathF.Floor(value / period) * period);
    }

    /// <summary>Maps <paramref name="value"/> into [0, <paramref name="period"/>). Requires a positive <paramref name="period"/>.</summary>
    public static double Repeat(double value, double period)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(period, 0.0);
        return value - (Math.Floor(value / period) * period);
    }
}
