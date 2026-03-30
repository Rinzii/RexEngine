namespace Rex.Shared.Numerics;

/// <summary>Yaw and similar angles stored in degrees, matching snapshot rotation fields.</summary>
public static class AngleMath
{
    /// <summary>Wraps <paramref name="degrees"/> into [0, <paramref name="period"/>).</summary>
    public static float RepeatDegrees(float degrees, float period = 360f) => PeriodicMath.Repeat(degrees, period);

    /// <summary>Signed shortest difference from <paramref name="fromDegrees"/> to <paramref name="toDegrees"/>, in (-180, 180].</summary>
    public static float DeltaAngleDegrees(float fromDegrees, float toDegrees)
    {
        var delta = RepeatDegrees(toDegrees - fromDegrees);
        return delta > 180f ? delta - 360f : delta;
    }

    /// <summary>Interpolates from <paramref name="fromDegrees"/> toward <paramref name="toDegrees"/> along the shortest arc.</summary>
    public static float LerpAngleDegrees(float fromDegrees, float toDegrees, float t) =>
        MathF.FusedMultiplyAdd(DeltaAngleDegrees(fromDegrees, toDegrees), t, fromDegrees);
}
