namespace Rex.Shared.GameStates;

/// <summary>
/// Immutable resolved game-state frame built from one full snapshot or from accumulated partial updates.
/// </summary>
/// <typeparam name="TEntityState">Per-entity state payload type.</typeparam>
public sealed class ResolvedGameState<TEntityState> : IGameState<TEntityState>
{
    /// <summary>
    /// Creates one resolved state frame.
    /// </summary>
    /// <param name="serverTick">Authoritative server tick represented by this frame.</param>
    /// <param name="entities">Fully resolved entity payload list for the frame.</param>
    public ResolvedGameState(uint serverTick, IReadOnlyList<TEntityState> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);

        ServerTick = serverTick;
        Entities = entities;
    }

    /// <inheritdoc />
    public uint ServerTick { get; }

    /// <inheritdoc />
    public IReadOnlyList<TEntityState> Entities { get; }
}
