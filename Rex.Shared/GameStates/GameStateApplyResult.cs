namespace Rex.Shared.GameStates;

/// <summary>
/// Result of applying one authoritative state frame.
/// </summary>
public enum GameStateApplyResult
{
    /// <summary>The frame was ignored because it was stale.</summary>
    IgnoredStale = 0,

    /// <summary>The frame could not be applied because a full baseline is required first.</summary>
    MissingBaseline = 1,

    /// <summary>The frame was applied successfully.</summary>
    Applied = 2
}
