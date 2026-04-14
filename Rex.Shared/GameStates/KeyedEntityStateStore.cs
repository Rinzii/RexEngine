namespace Rex.Shared.GameStates;

/// <summary>
/// Stores keyed entity state payloads and can materialize deterministic ordered frames.
/// </summary>
/// <typeparam name="TKey">Stable entity key type.</typeparam>
/// <typeparam name="TEntityState">Per-entity state payload type.</typeparam>
public sealed class KeyedEntityStateStore<TKey, TEntityState>
    where TKey : notnull
{
    private readonly Dictionary<TKey, TEntityState> _entities = [];
    private readonly Func<TEntityState, TKey> _keySelector;
    private readonly IComparer<TKey> _keyComparer;

    /// <summary>
    /// Creates one keyed entity-state store.
    /// </summary>
    /// <param name="keySelector">Resolves the stable key for one entity state payload.</param>
    /// <param name="keyComparer">Optional comparer used for deterministic ordered frame output.</param>
    public KeyedEntityStateStore(Func<TEntityState, TKey> keySelector, IComparer<TKey>? keyComparer = null)
    {
        _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
        _keyComparer = keyComparer ?? Comparer<TKey>.Default;
    }

    /// <summary>Gets the current keyed entity states.</summary>
    public IReadOnlyDictionary<TKey, TEntityState> Entities => _entities;

    /// <summary>Upserts one entity state.</summary>
    public void Upsert(TEntityState entityState)
    {
        _entities[_keySelector(entityState)] = entityState;
    }

    /// <summary>Removes one entity state by key.</summary>
    public bool Remove(TKey key)
    {
        return _entities.Remove(key);
    }

    /// <summary>Attempts to read one entity state by key.</summary>
    public bool TryGet(TKey key, out TEntityState entityState)
    {
        return _entities.TryGetValue(key, out entityState!);
    }

    /// <summary>Clears every stored entity state.</summary>
    public void Clear()
    {
        _entities.Clear();
    }

    /// <summary>Replaces the store contents with the supplied entity states.</summary>
    public void ReplaceAll(IEnumerable<TEntityState> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        _entities.Clear();
        foreach (TEntityState entity in entities)
        {
            Upsert(entity);
        }
    }

    /// <summary>Builds one deterministically ordered frame of the current states.</summary>
    public List<TEntityState> BuildOrderedFrame()
    {
        // Stable key order keeps wire payloads and client merge paths repeatable across runs.
        return [.. _entities.OrderBy(static pair => pair.Key, _keyComparer).Select(static pair => pair.Value)];
    }
}
