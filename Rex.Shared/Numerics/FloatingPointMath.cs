namespace Rex.Shared.Numerics;

/// <summary>Approximate comparisons for floating scalars. Use when exact equality is not meaningful.</summary>
public static class FloatingPointMath
{
    /// <summary>Default tolerance for world scale single precision checks.</summary>
    public const float DefaultFloatTolerance = 1e-5f;

    /// <summary>Default tolerance for double precision checks.</summary>
    public const double DefaultDoubleTolerance = 1e-9;

    /// <summary>True when the absolute difference is at most <paramref name="tolerance"/>.</summary>
    public static bool IsNearlyEqual(float a, float b, float tolerance = DefaultFloatTolerance) =>
        MathF.Abs(a - b) <= tolerance;

    /// <summary>True when the absolute difference is at most <paramref name="tolerance"/>.</summary>
    public static bool IsNearlyEqual(double a, double b, double tolerance = DefaultDoubleTolerance) =>
        Math.Abs(a - b) <= tolerance;

    /// <summary>True when <paramref name="value"/> is within <paramref name="tolerance"/> of zero.</summary>
    public static bool IsNearlyZero(float value, float tolerance = DefaultFloatTolerance) =>
        MathF.Abs(value) <= tolerance;

    /// <summary>True when <paramref name="value"/> is within <paramref name="tolerance"/> of zero.</summary>
    public static bool IsNearlyZero(double value, double tolerance = DefaultDoubleTolerance) =>
        Math.Abs(value) <= tolerance;
}
