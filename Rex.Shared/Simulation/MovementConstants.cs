namespace Rex.Shared.Simulation;

/// <summary>Tuning shared between the authoritative world and client prediction.</summary>
public static class MovementConstants
{
    /// <summary>XZ displacement in world units per simulation tick per unit input axis.</summary>
    public const float PlanarUnitsPerInputTick = 5f;
}
