namespace Rex.Shared.Timing;

/// <summary>Shared fixed-timestep accumulator drain (Unity <c>FixedUpdate</c> scheduling).</summary>
public static class PhasedLoop
{
    /// <summary>Default hitch clamp in seconds (spiral-of-death guard).</summary>
    public const float DefaultMaxFrameSeconds = 0.25f;

    /// <summary>Adds <paramref name="frameSeconds"/> to <paramref name="accumulator"/> (clamped), then runs <paramref name="fixedStep"/> until the accumulator is below one tick.</summary>
    /// <returns>Number of fixed steps executed.</returns>
    public static int RunFixedSteps(
        TickClock clock,
        ref double accumulator,
        double frameSeconds,
        Action fixedStep,
        float maxFrameSeconds = DefaultMaxFrameSeconds)
    {
        if (frameSeconds > maxFrameSeconds)
            frameSeconds = maxFrameSeconds;

        accumulator += frameSeconds;
        var steps = 0;
        var interval = clock.TickInterval;

        while (accumulator >= interval)
        {
            fixedStep();
            clock.IncrementTick();
            accumulator -= interval;
            steps++;
        }

        return steps;
    }
}