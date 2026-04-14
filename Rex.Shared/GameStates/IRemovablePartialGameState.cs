namespace Rex.Shared.GameStates;

/// <summary>
/// Common contract for one full or delta authoritative game-state frame that can also retire removed entities.
/// </summary>
/// <typeparam name="TKey">Stable entity key type.</typeparam>
/// <typeparam name="TEntityState">Per-entity state payload type.</typeparam>
public interface IRemovablePartialGameState<TKey, out TEntityState> : IPartialGameState<TEntityState>
    where TKey : notnull
{
    /// <summary>Gets the stable keys removed by this authoritative frame.</summary>
    IReadOnlyList<TKey> RemovedKeys { get; }
}
