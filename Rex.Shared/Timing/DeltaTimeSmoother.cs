namespace Rex.Shared.Timing;

/// <summary>Exponential smoothing of frame deltas for stable variable-rate systems (cameras, UI).</summary>
public sealed class DeltaTimeSmoother
{
    private float _smooth;

    /// <param name="rawDeltaSeconds">Latest wall-frame duration.</param>
    /// <param name="blendWeight">Responsiveness in (0,1]; higher tracks raw delta faster (Unity-like default ~0.1–0.2 per frame at 60 Hz).</param>
    public float Next(float rawDeltaSeconds, float blendWeight = 0.12f)
    {
        if (rawDeltaSeconds <= 0f || float.IsNaN(rawDeltaSeconds) || float.IsInfinity(rawDeltaSeconds))
            return _smooth > 0f ? _smooth : 1f / 60f;

        if (_smooth <= 0f)
            _smooth = rawDeltaSeconds;
        else
            _smooth += (rawDeltaSeconds - _smooth) * blendWeight;

        return _smooth;
    }

    public void Reset()
    {
        _smooth = 0f;
    }
}