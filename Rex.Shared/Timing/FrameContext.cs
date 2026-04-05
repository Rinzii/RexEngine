namespace Rex.Shared.Timing;

/// <summary>Immutable snapshot after all fixed steps for one display iteration. Carries timing for the variable display phase and metadata for the fixed batch that just finished.</summary>
/// <remarks>
/// Typical hosts run fixed steps, set interpolation alpha, call <c>OnUpdate</c> and <c>OnLateUpdate</c>, then present.
/// Prefer <see cref="ScaledDeltaTime"/> for gameplay that respects pause or slow motion.
/// Use <see cref="UnscaledDeltaTime"/> for UI and counters that follow wall clock time.
/// </remarks>
public readonly struct FrameContext
{
    /// <summary>Builds one frame snapshot after fixed simulation work for this display iteration.</summary>
    /// <param name="clock">Clock whose <see cref="TickClock.Alpha"/> matches <paramref name="interpolationAlpha"/>.</param>
    /// <param name="unscaledDeltaTime">Wall clock seconds for this display iteration before <paramref name="timeScale"/> is applied.</param>
    /// <param name="unscaledSmoothDeltaTime">Smoothed wall delta before <paramref name="timeScale"/> is applied.</param>
    /// <param name="timeScale">Multiplier for scaled deltas. Use 0 to pause simulation time.</param>
    /// <param name="fixedStepsThisFrame">How many fixed ticks ran during this iteration.</param>
    /// <param name="interpolationAlpha">Blend between previous and current fixed state for rendering.</param>
    /// <param name="frameIndex">Monotonic index of the display frame since the loop started.</param>
    /// <param name="elapsedRealtimeSeconds">Wall seconds since the main loop started.</param>
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

    /// <summary>Wall clock duration in seconds for this iteration before <see cref="TimeScale"/>. The loop already clamped huge hitches.</summary>
    public float UnscaledDeltaTime { get; }

    /// <summary>Multiplier for <see cref="ScaledDeltaTime"/>. Use 0 for pause or a value below 1 for slow motion.</summary>
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

    /// <summary>Monotonic counter of display frames since the loop started. The first variable phase is 1.</summary>
    public ulong FrameIndex { get; }

    /// <summary>Wall seconds since the main loop started.</summary>
    public double ElapsedRealtimeSeconds { get; }

    /// <summary>Fixed simulation step length in seconds (<c>1 / tickRate</c>).</summary>
    public float FixedDeltaTime => (float)Clock.TickInterval;
}
