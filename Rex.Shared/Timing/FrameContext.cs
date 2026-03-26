namespace Rex.Shared.Timing;

/// <summary>
/// Immutable snapshot for one display iteration after every fixed simulation step for this iteration has run.
/// Same role as the Unity variable-rate phase (<c>Update</c> and <c>LateUpdate</c>) plus fixed-step metadata.
/// </summary>
/// <remarks>
/// Rex runs fixed steps, sets interpolation alpha, runs <c>OnUpdate</c>, runs <c>LateUpdate</c>, then renders.
/// Prefer <see cref="ScaledDeltaTime"/> for gameplay that respects pause or slow-mo.
/// Use <see cref="UnscaledDeltaTime"/> for UI and real-time counters.
/// </remarks>
public readonly struct FrameContext
{
    public FrameContext(
        TickClock clock,
        float unscaledDeltaTime,
        float unscaledSmoothDeltaTime,
        float timeScale,
        int fixedStepsThisFrame,
        float interpolationAlpha,
        ulong frameIndex,
        double elapsedRealtimeSeconds)
    {
        Clock = clock;
        UnscaledDeltaTime = unscaledDeltaTime;
        TimeScale = timeScale;
        ScaledDeltaTime = unscaledDeltaTime * timeScale;
        UnscaledSmoothDeltaTime = unscaledSmoothDeltaTime;
        SmoothDeltaTime = unscaledSmoothDeltaTime * timeScale;
        FixedStepsThisFrame = fixedStepsThisFrame;
        InterpolationAlpha = interpolationAlpha;
        FrameIndex = frameIndex;
        ElapsedRealtimeSeconds = elapsedRealtimeSeconds;
    }

    /// <summary>Simulation clock after fixed steps. <see cref="TickClock.Alpha"/> matches <see cref="InterpolationAlpha"/>.</summary>
    public TickClock Clock { get; }

    /// <summary>Wall-frame duration in seconds before <see cref="TimeScale"/>. The loop already clamped huge hitches.</summary>
    public float UnscaledDeltaTime { get; }

    /// <summary>Multiplier for <see cref="ScaledDeltaTime"/>. Use 0 for pause or a value below 1 for slow-mo.</summary>
    public float TimeScale { get; }

    /// <summary>Product of <see cref="UnscaledDeltaTime"/> and <see cref="TimeScale"/>.</summary>
    public float ScaledDeltaTime { get; }

    /// <summary>Smoothed wall delta (exponential average). Stable for cameras and UI without affecting fixed sim.</summary>
    public float UnscaledSmoothDeltaTime { get; }

    /// <summary>Product of <see cref="UnscaledSmoothDeltaTime"/> and <see cref="TimeScale"/>.</summary>
    public float SmoothDeltaTime { get; }

    /// <summary>How many fixed ticks ran this iteration (0 if the machine outruns <see cref="TickClock.TickRate"/>).</summary>
    public int FixedStepsThisFrame { get; }

    /// <summary>Blend between last and current fixed state for rendering. Same value as <see cref="TickClock.Alpha"/>.</summary>
    public float InterpolationAlpha { get; }

    /// <summary>Monotonic display-frame counter since the loop started. The first variable phase is 1.</summary>
    public ulong FrameIndex { get; }

    /// <summary>Wall seconds since the main loop started.</summary>
    public double ElapsedRealtimeSeconds { get; }

    /// <summary>Fixed simulation step length in seconds (<c>1 / tickRate</c>).</summary>
    public float FixedDeltaTime => (float)Clock.TickInterval;
}
