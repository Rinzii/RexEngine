namespace Rex.Shared.Timing;

/// <summary>Maps wall clock duration of each display frame to zero or more fixed simulation ticks using a running accumulator.</summary>
public static class PhasedLoop
{
    /// <summary>Upper bound on wall seconds one frame may add to the accumulator. Limits how many fixed ticks run after a long stall.</summary>
    public const float DefaultMaxFrameSeconds = 0.25f;

    /// <summary>Adds clamped <paramref name="frameSeconds"/> to <paramref name="accumulator"/>. Runs <paramref name="fixedStep"/> until the accumulator is below one tick interval.</summary>
    /// <returns>Number of fixed steps executed.</returns>
    public static int RunFixedSteps(
        TickClock clock,
        ref double accumulator,
        double frameSeconds,
        Action fixedStep,
        float maxFrameSeconds = DefaultMaxFrameSeconds)
    {
        // One wall frame must not schedule an unbounded number of ticks after a long stall.
        if (frameSeconds > maxFrameSeconds)
        {
            frameSeconds = maxFrameSeconds;
        }

        // Bank leftover accumulator time plus the wall seconds for this call.
        accumulator += frameSeconds;
        var steps = 0;
        var interval = clock.TickInterval;

        // Drain whole tick intervals first so fixedStep and CurrentTick stay aligned.
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
