namespace Rex.Shared.Numerics;

/// <summary>Inverse lerp, range remap and smooth curves not provided on <see cref="Math"/> or primitive static APIs.</summary>
public static class InterpolationMath
{
    /// <summary>Linear weight of <paramref name="value"/> between <paramref name="a"/> and <paramref name="b"/>. Unclamped. Returns 0 when <paramref name="a"/> equals <paramref name="b"/>.</summary>
    public static float InverseLerp(float a, float b, float value) =>
        a == b ? 0f : (value - a) / (b - a);

    /// <summary>Linear weight of <paramref name="value"/> between <paramref name="a"/> and <paramref name="b"/>. Unclamped. Returns 0 when <paramref name="a"/> equals <paramref name="b"/>.</summary>
    public static double InverseLerp(double a, double b, double value) =>
        a == b ? 0.0 : (value - a) / (b - a);

    /// <summary>Maps <paramref name="value"/> linearly from [<paramref name="fromMin"/>, <paramref name="fromMax"/>] into [<paramref name="toMin"/>, <paramref name="toMax"/>]. Unclamped.</summary>
    public static float Remap(float fromMin, float fromMax, float toMin, float toMax, float value) =>
        float.Lerp(toMin, toMax, InverseLerp(fromMin, fromMax, value));

    /// <summary>Maps <paramref name="value"/> linearly from [<paramref name="fromMin"/>, <paramref name="fromMax"/>] into [<paramref name="toMin"/>, <paramref name="toMax"/>]. Unclamped.</summary>
    public static double Remap(double fromMin, double fromMax, double toMin, double toMax, double value) =>
        double.Lerp(toMin, toMax, InverseLerp(fromMin, fromMax, value));

    /// <summary>Hermite edge blend from 0 to 1. When both edges coincide, the result is 0 below that value and 1 at or above.</summary>
    public static float SmoothStep(float edge0, float edge1, float x)
    {
        var denom = edge1 - edge0;
        if (denom == 0f)
        {
            return x < edge0 ? 0f : 1f;
        }

        var t = Math.Clamp((x - edge0) / denom, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    /// <summary>Hermite edge blend from 0 to 1. When both edges coincide, the result is 0 below that value and 1 at or above.</summary>
    public static double SmoothStep(double edge0, double edge1, double x)
    {
        var denom = edge1 - edge0;
        if (denom == 0.0)
        {
            return x < edge0 ? 0.0 : 1.0;
        }

        var t = Math.Clamp((x - edge0) / denom, 0.0, 1.0);
        return t * t * (3.0 - 2.0 * t);
    }
}
