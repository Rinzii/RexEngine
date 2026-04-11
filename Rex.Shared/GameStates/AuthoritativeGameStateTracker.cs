namespace Rex.Shared.GameStates;

/// <summary>
/// Tracks the current authoritative keyed entity state and applies full or delta frames.
/// </summary>
/// <typeparam name="TKey">Stable entity key type.</typeparam>
/// <typeparam name="TEntityState">Per-entity state payload type.</typeparam>
public sealed class AuthoritativeGameStateTracker<TKey, TEntityState>
    where TKey : notnull
{
    private readonly GameStateBuffer<TEntityState> _buffer = new();
    // Rows merged only from applied snapshots. Cleared when a full snapshot arrives.
    private readonly KeyedEntityStateStore<TKey, TEntityState> _authoritativeStateStore;
    // Working copy for the resolved frame. Rebuilt from authoritative rows plus pending out-of-band edits.
    private readonly KeyedEntityStateStore<TKey, TEntityState> _currentStateStore;
    // Spawn or destroy messages carry a server tick. Buckets replay until a snapshot tick consumes them.
    private readonly SortedDictionary<uint, List<PendingEntityChange>> _pendingChanges = [];
    private readonly Func<TEntityState, TKey> _keySelector;

    /// <summary>
    /// Creates one authoritative keyed state tracker.
    /// </summary>
    /// <param name="keySelector">Resolves the stable key for one entity state payload.</param>
    /// <param name="keyComparer">Optional comparer used for deterministic ordered frame output.</param>
    public AuthoritativeGameStateTracker(Func<TEntityState, TKey> keySelector, IComparer<TKey>? keyComparer = null)
    {
        _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
        _authoritativeStateStore = new KeyedEntityStateStore<TKey, TEntityState>(_keySelector, keyComparer);
        _currentStateStore = new KeyedEntityStateStore<TKey, TEntityState>(_keySelector, keyComparer);
    }

    /// <summary>Gets the previous resolved authoritative frame, if any.</summary>
    public IGameState<TEntityState>? Previous => _buffer.Previous;

    /// <summary>Gets the current resolved authoritative frame, if any.</summary>
    public IGameState<TEntityState>? Current => _buffer.Current;

    /// <summary>Gets the current resolved entity payloads.</summary>
    public IReadOnlyList<TEntityState> CurrentEntities => _buffer.Current?.Entities ?? [];

    /// <summary>Gets the current applied server tick.</summary>
    public uint LastServerTick => _buffer.LastServerTick;

    /// <summary>Gets a value indicating whether a full authoritative state is required before more deltas can apply.</summary>
    public bool NeedsFullState { get; private set; }

    /// <summary>Applies one full or delta authoritative frame.</summary>
    public GameStateApplyResult ApplySnapshot(IPartialGameState<TEntityState> snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.ServerTick <= _buffer.LastServerTick)
        {
            return GameStateApplyResult.IgnoredStale;
        }

        // Deltas need a prior frame so removals and partial entity lists stay meaningful.
        if (!snapshot.IsFullSnapshot && _buffer.Current == null)
        {
            NeedsFullState = true;
            return GameStateApplyResult.MissingBaseline;
        }

        if (snapshot.IsFullSnapshot)
        {
            _authoritativeStateStore.Clear();
        }

        foreach (TEntityState entity in snapshot.Entities)
        {
            _authoritativeStateStore.Upsert(entity);
        }

        if (!snapshot.IsFullSnapshot && snapshot is IRemovablePartialGameState<TKey, TEntityState> removableSnapshot)
        {
            foreach (TKey removedKey in removableSnapshot.RemovedKeys)
            {
                _ = _authoritativeStateStore.Remove(removedKey);
            }
        }

        // Spawn or destroy rows at or before this tick are already reflected in snapshot.Entities or RemovedKeys.
        DiscardPendingChangesUpTo(snapshot.ServerTick);
        RebuildCurrentStateStore();
        NeedsFullState = false;
        _buffer.Apply(new ResolvedGameState<TEntityState>(snapshot.ServerTick, _currentStateStore.BuildOrderedFrame()));
        return GameStateApplyResult.Applied;
    }

    /// <summary>Applies one live entity upsert outside the snapshot stream.</summary>
    public void ApplyUpsert(uint serverTick, TEntityState entityState)
    {
        // Baseline tick must exist and the message tick must stay strictly ahead of the last applied snapshot.
        if (_buffer.Current == null || serverTick <= _buffer.LastServerTick)
        {
            return;
        }

        QueuePendingUpsert(serverTick, entityState);
        RefreshCurrentFrame();
    }

    /// <summary>Attempts to read the current authoritative payload for one entity key.</summary>
    public bool TryGetCurrentEntity(TKey key, out TEntityState entityState)
    {
        return _currentStateStore.TryGet(key, out entityState);
    }

    /// <summary>Applies one live entity removal outside the snapshot stream.</summary>
    public void ApplyRemove(uint serverTick, TKey key)
    {
        // Same gating as ApplyUpsert so removals never run before the first snapshot establishes ticks.
        if (_buffer.Current == null || serverTick <= _buffer.LastServerTick)
        {
            return;
        }

        QueuePendingRemove(serverTick, key);
        RefreshCurrentFrame();
    }

    /// <summary>Clears the tracked authoritative state.</summary>
    public void Reset()
    {
        _authoritativeStateStore.Clear();
        _currentStateStore.Clear();
        _pendingChanges.Clear();
        _buffer.Clear();
        NeedsFullState = false;
    }

    /// <summary>Gets an interpolated view of the current state.</summary>
    public IReadOnlyList<TEntityState> GetInterpolatedState(float alpha, Func<TEntityState, TKey> keySelector,
        Func<TEntityState, TEntityState, float, TEntityState> lerp)
    {
        return GameStateInterpolation.Interpolate(_buffer, alpha, keySelector, lerp);
    }

    private void RefreshCurrentFrame()
    {
        if (_buffer.Current == null)
        {
            return;
        }

        RebuildCurrentStateStore();
        _buffer.ReplaceCurrent(new ResolvedGameState<TEntityState>(_buffer.LastServerTick, _currentStateStore.BuildOrderedFrame()));
    }

    private void QueuePendingUpsert(uint serverTick, TEntityState entityState)
    {
        GetPendingBucket(serverTick).Add(PendingEntityChange.CreateUpsert(entityState, _keySelector(entityState)));
    }

    private void QueuePendingRemove(uint serverTick, TKey key)
    {
        GetPendingBucket(serverTick).Add(PendingEntityChange.CreateRemove(key));
    }

    private List<PendingEntityChange> GetPendingBucket(uint serverTick)
    {
        if (!_pendingChanges.TryGetValue(serverTick, out List<PendingEntityChange>? pendingBucket))
        {
            pendingBucket = [];
            _pendingChanges.Add(serverTick, pendingBucket);
        }

        return pendingBucket;
    }

    private void DiscardPendingChangesUpTo(uint authoritativeTick)
    {
        if (_pendingChanges.Count == 0)
        {
            return;
        }

        uint[] consumedTicks = _pendingChanges.Keys
            .Where(tick => tick <= authoritativeTick)
            .ToArray();

        foreach (uint consumedTick in consumedTicks)
        {
            _ = _pendingChanges.Remove(consumedTick);
        }
    }

    private void RebuildCurrentStateStore()
    {
        _currentStateStore.ReplaceAll(_authoritativeStateStore.Entities.Values);

        // Replay walks ticks in ascending order. List order inside one tick is preserved.
        foreach ((_, List<PendingEntityChange> pendingBucket) in _pendingChanges)
        {
            for (int pendingIndex = 0; pendingIndex < pendingBucket.Count; pendingIndex++)
            {
                PendingEntityChange pendingChange = pendingBucket[pendingIndex];
                if (pendingChange.IsRemoval)
                {
                    _ = _currentStateStore.Remove(pendingChange.Key);
                    continue;
                }

                _currentStateStore.Upsert(pendingChange.EntityState);
            }
        }
    }

    private readonly record struct PendingEntityChange(bool IsRemoval, TKey Key, TEntityState EntityState)
    {
        public static PendingEntityChange CreateUpsert(TEntityState entityState, TKey key)
        {
            return new PendingEntityChange(false, key, entityState);
        }

        public static PendingEntityChange CreateRemove(TKey key)
        {
            return new PendingEntityChange(true, key, default!);
        }
    }
}
