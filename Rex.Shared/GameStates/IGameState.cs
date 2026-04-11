namespace Rex.Shared.GameStates;

/// <summary>
/// Common contract for one server-authored game-state frame.
/// </summary>
/// <typeparam name="TEntityState">Per-entity state payload type.</typeparam>
public interface IGameState<out TEntityState>
{
    /// <summary>Gets the authoritative server tick for this state frame.</summary>
    uint ServerTick { get; }

    /// <summary>Gets the entity state payloads carried by this frame.</summary>
    IReadOnlyList<TEntityState> Entities { get; }
}
