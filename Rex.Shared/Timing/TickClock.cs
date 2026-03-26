namespace Rex.Shared.Timing;

/// <summary>Fixed timestep clock: tick counter, sim time, and blend factor for rendering between ticks.</summary>
public sealed class TickClock
{
    public int TickRate { get; }
    public uint CurrentTick { get; private set; }
    /// <summary>Wall time for one sim tick in seconds (e.g. 60 Hz is ~0.0167).</summary>
    public double TickInterval { get; }
    /// <summary>Sim time advanced so far, in seconds (tick count * interval).</summary>
    public double ElapsedTime { get; private set; }
    /// <summary>Portion of the current tick not yet consumed by sim. Set by <see cref="GameLoop"/> for render lerp.</summary>
    public float Alpha { get; private set; }

    public TickClock(int tickRate)
    {
        TickRate = tickRate;
        TickInterval = 1.0 / tickRate;
    }

    public void IncrementTick()
    {
        CurrentTick++;
        ElapsedTime += TickInterval;
    }

    public void SetAlpha(float alpha)
    {
        Alpha = alpha;
    }
}
