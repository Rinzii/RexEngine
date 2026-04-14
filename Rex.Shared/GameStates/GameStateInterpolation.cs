namespace Rex.Shared.GameStates;

/// <summary>
/// Helper routines for interpolating between two applied game-state frames.
/// </summary>
public static class GameStateInterpolation
{
    /// <summary>
    /// Interpolates the current frame against the previous frame using one entity key selector and lerp callback.
    /// </summary>
    /// <typeparam name="TEntityState">Per-entity state payload type.</typeparam>
    /// <typeparam name="TKey">Stable entity key type.</typeparam>
    /// <param name="buffer">Applied state buffer.</param>
    /// <param name="alpha">Interpolation alpha between 0 and 1.</param>
    /// <param name="keySelector">Key selector used to match entity payloads across frames.</param>
    /// <param name="lerp">Interpolation callback for matching payloads.</param>
    /// <returns>The interpolated entity state list.</returns>
    public static IReadOnlyList<TEntityState> Interpolate<TEntityState, TKey>(
        GameStateBuffer<TEntityState> buffer,
        float alpha,
        Func<TEntityState, TKey> keySelector,
        Func<TEntityState, TEntityState, float, TEntityState> lerp)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(lerp);

        if (buffer.Current == null)
        {
            return [];
        }

        if (buffer.Previous == null)
        {
            // First frame after connect. Nothing to blend against yet.
            return buffer.Current.Entities;
        }

        Dictionary<TKey, TEntityState> previousEntities = [];
        foreach (TEntityState entity in buffer.Previous.Entities)
        {
            previousEntities[keySelector(entity)] = entity;
        }

        List<TEntityState> result = [];
        foreach (TEntityState current in buffer.Current.Entities)
        {
            TKey key = keySelector(current);
            if (previousEntities.TryGetValue(key, out TEntityState? previous))
            {
                result.Add(lerp(previous, current, alpha));
            }
            else
            {
                result.Add(current);
            }
        }

        return result;
    }
}
