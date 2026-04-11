namespace Rex.Shared.GameStates;

/// <summary>
/// Tracks the previous and current authoritative game-state frames for interpolation and reconciliation.
/// </summary>
/// <typeparam name="TEntityState">Per-entity state payload type.</typeparam>
public sealed class GameStateBuffer<TEntityState>
{
    /// <summary>Gets the previous applied state frame, if any.</summary>
    public IGameState<TEntityState>? Previous { get; private set; }

    /// <summary>Gets the current applied state frame, if any.</summary>
    public IGameState<TEntityState>? Current { get; private set; }

    /// <summary>Gets the server tick of the current frame.</summary>
    public uint LastServerTick => Current?.ServerTick ?? 0;

    /// <summary>Applies one new authoritative game-state frame.</summary>
    /// <param name="gameState">State frame to make current.</param>
    public void Apply(IGameState<TEntityState> gameState)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        // Normal snapshot path advances history so interpolation can see prior and current ticks.
        Previous = Current;
        Current = gameState;
    }

    /// <summary>Replaces the current frame without shifting it into <see cref="Previous"/>.</summary>
    /// <param name="gameState">Replacement current frame.</param>
    public void ReplaceCurrent(IGameState<TEntityState> gameState)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        // Same server tick refresh. Keeps Previous frozen so render blend does not jump when only current mutates.
        Current = gameState;
    }

    /// <summary>Clears both applied state frames.</summary>
    public void Clear()
    {
        Previous = null;
        Current = null;
    }
}
