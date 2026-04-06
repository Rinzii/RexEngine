namespace Rex.Shared.Timing;

/// <summary>Exponential smoothing of frame deltas for stable systems whose frame rate varies (cameras, UI).</summary>
public sealed class DeltaTimeSmoother
{
    private float _smooth;

    /// <summary>Applies exponential smoothing to a wall clock frame delta and returns the stable value.</summary>
    /// <param name="rawDeltaSeconds">Latest wall clock frame duration in seconds.</param>
    /// <param name="blendWeight">Responsiveness in (0,1]. Higher values track raw deltas faster. Typical values near 0.1 or 0.2 at 60 Hz.</param>
    public float Next(float rawDeltaSeconds, float blendWeight = 0.12f)
    {
        if (rawDeltaSeconds <= 0f || float.IsNaN(rawDeltaSeconds) || float.IsInfinity(rawDeltaSeconds))
        {
            return _smooth > 0f ? _smooth : 1f / 60f;
        }

        if (_smooth <= 0f)
        {
            _smooth = rawDeltaSeconds;
        }
        else
        {
            _smooth += (rawDeltaSeconds - _smooth) * blendWeight;
        }

        return _smooth;
    }

    /// <summary>Clears internal state so the next call seeds smoothing from the incoming raw delta.</summary>
    public void Reset()
    {
        _smooth = 0f;
    }
}
