using Rex.Shared.Net.Messages;

namespace Rex.Shared.Numerics;

/// <summary>Blends two snapshot poses for render interpolation.</summary>
public static class EntityStateInterpolation
{
    /// <summary>Linear position and shortest-arc yaw between ticks.</summary>
    public static EntityState Lerp(EntityState previous, EntityState current, float alpha) =>
        new(
            current.EntityId,
            float.Lerp(previous.X, current.X, alpha),
            float.Lerp(previous.Y, current.Y, alpha),
            float.Lerp(previous.Z, current.Z, alpha),
            AngleMath.LerpAngleDegrees(previous.RotationY, current.RotationY, alpha));
}
