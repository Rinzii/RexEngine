namespace Rex.Shared.Timing;

public sealed class TickClock
{
    public uint CurrentTick { get; private set; }
    public double TickInterval { get; }
    public double ElapsedTime { get; private set; }
    public float Alpha { get; private set; }

    public TickClock(int tickRate)
    {
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
