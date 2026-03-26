namespace Rex.Shared.Timing;

/// <summary>
/// Immutable snapshot for one display iteration, after all fixed simulation steps for this iteration have run.
/// Maps to Unity’s variable-rate phase (<c>Update</c> / <c>LateUpdate</c>), with explicit fixed-step metadata.
/// </summary>
/// <remarks>
/// Phase order (Rex): fixed steps (0..N) → set interpolation alpha → <c>OnUpdate</c> → <c>OnLateUpdate</c> → render.
/// Prefer <see cref="ScaledDeltaTime"/> for gameplay that should respect pause/slow-mo; use <see cref="UnscaledDeltaTime"/> for UI and real-time counters.
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

    /// <summary>Simulation clock after fixed steps; <see cref="TickClock.Alpha"/> matches <see cref="InterpolationAlpha"/>.</summary>
    public TickClock Clock { get; }

    /// <summary>Wall-frame duration in seconds before <see cref="TimeScale"/> (clamped max frame already applied by the loop).</summary>
    public float UnscaledDeltaTime { get; }

    /// <summary>Multiplier for <see cref="ScaledDeltaTime"/>; use 0 for pause, or a value below 1 for slow-mo.</summary>
    public float TimeScale { get; }

    /// <summary><see cref="UnscaledDeltaTime"/> × <see cref="TimeScale"/>.</summary>
    public float ScaledDeltaTime { get; }

    /// <summary>Smoothed wall delta (exponential average); stable for cameras and UI without affecting fixed sim.</summary>
    public float UnscaledSmoothDeltaTime { get; }

    /// <summary><see cref="UnscaledSmoothDeltaTime"/> × <see cref="TimeScale"/>.</summary>
    public float SmoothDeltaTime { get; }

    /// <summary>How many fixed ticks ran this iteration (0 if running faster than <see cref="TickClock.TickRate"/>).</summary>
    public int FixedStepsThisFrame { get; }

    /// <summary>Blend between last and current fixed state for rendering (same as <see cref="TickClock.Alpha"/>).</summary>
    public float InterpolationAlpha { get; }

    /// <summary>Monotonic display-frame counter since the loop started (first variable phase is 1).</summary>
    public ulong FrameIndex { get; }

    /// <summary>Wall seconds since the main loop started.</summary>
    public double ElapsedRealtimeSeconds { get; }

    /// <summary>Fixed simulation step length in seconds (<c>1 / tickRate</c>).</summary>
    public float FixedDeltaTime => (float)Clock.TickInterval;
}