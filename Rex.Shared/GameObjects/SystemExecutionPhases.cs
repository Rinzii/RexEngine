namespace Rex.Shared.GameObjects;

/// <summary>
/// Fixed-step execution phases used by the gameplay-facing entity system manager.
/// </summary>
public enum SystemUpdatePhase
{
    /// <summary>Runs before the default simulation phase.</summary>
    PreUpdate = 0,

    /// <summary>Runs during the default simulation phase.</summary>
    Update = 1,

    /// <summary>Runs after the default simulation phase.</summary>
    PostUpdate = 2
}

/// <summary>
/// Render-frame execution phases used by the gameplay-facing entity system manager.
/// </summary>
public enum FrameUpdatePhase
{
    /// <summary>Runs before the default frame phase.</summary>
    PreFrame = 0,

    /// <summary>Runs during the default frame phase.</summary>
    Frame = 1,

    /// <summary>Runs after the default frame phase.</summary>
    PostFrame = 2
}
