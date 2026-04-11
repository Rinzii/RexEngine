namespace Rex.Shared.GameStates;

/// <summary>
/// Common contract for one full or delta authoritative game-state frame.
/// </summary>
/// <typeparam name="TEntityState">Per-entity state payload type.</typeparam>
public interface IPartialGameState<out TEntityState> : IGameState<TEntityState>
{
    /// <summary>Gets a value indicating whether this frame is a full authoritative replacement.</summary>
    bool IsFullSnapshot { get; }
}
