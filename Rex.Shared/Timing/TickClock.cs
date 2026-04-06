namespace Rex.Shared.Timing;

/// <summary>Fixed timestep clock with tick counter, elapsed sim time and blend factor for rendering between ticks.</summary>
public sealed class TickClock
{
    /// <summary>Target simulation rate in Hz.</summary>
    public int TickRate { get; }

    /// <summary>Number of completed fixed steps.</summary>
    public uint CurrentTick { get; private set; }

    /// <summary>Wall time for one sim tick in seconds (e.g. 60 Hz is ~0.0167).</summary>
    public double TickInterval { get; }

    /// <summary>Sim time advanced so far, in seconds (tick count * interval).</summary>
    public double ElapsedTime { get; private set; }

    /// <summary>Portion of the current tick not yet consumed by sim. Set each frame by the host app for render lerp.</summary>
    public float Alpha { get; private set; }

    /// <summary>Builds intervals from <paramref name="tickRate"/>.</summary>
    /// <param name="tickRate">Simulation steps per second.</param>
    public TickClock(int tickRate)
    {
        TickRate = tickRate;
        TickInterval = 1.0 / tickRate;
    }

    /// <summary>Advances <see cref="CurrentTick"/> and <see cref="ElapsedTime"/>.</summary>
    public void IncrementTick()
    {
        CurrentTick++;
        ElapsedTime += TickInterval;
    }

    /// <summary>Stores the fractional blend between ticks.</summary>
    /// <param name="alpha">Value in the zero to one range.</param>
    public void SetAlpha(float alpha)
    {
        Alpha = alpha;
    }
}
