using System.Diagnostics;

namespace Rex.Shared.Timing;

/// <summary>Fixed-timestep game loop with variable-rate rendering.</summary>
public sealed class GameLoop
{
    private readonly TickClock _clock;
    private readonly Stopwatch _stopwatch = new();
    private double _accumulator;

    public bool IsRunning { get; private set; }
    public Action? OnTick { get; set; }
    public Action<float>? OnRender { get; set; }

    /// <summary>True on servers to yield CPU. False on clients for max framerate.</summary>
    public bool YieldBetweenFrames { get; set; } = true;

    public TickClock Clock => _clock;

    public GameLoop(int tickRate)
    {
        _clock = new TickClock(tickRate);
    }

    public void Run()
    {
        IsRunning = true;
        _stopwatch.Start();
        var previousTime = _stopwatch.Elapsed.TotalSeconds;

        while (IsRunning)
        {
            var currentTime = _stopwatch.Elapsed.TotalSeconds;
            var frameTime = currentTime - previousTime;
            previousTime = currentTime;

            // Cap a hitch so we don't run many sim steps in one frame (spiral of death).
            if (frameTime > 0.25)
                frameTime = 0.25;

            _accumulator += frameTime;

            while (_accumulator >= _clock.TickInterval)
            {
                OnTick?.Invoke();
                _clock.IncrementTick();
                _accumulator -= _clock.TickInterval;
            }

            // How far we are from the next tick (0..1). Used for render interpolation.
            var alpha = (float)(_accumulator / _clock.TickInterval);
            _clock.SetAlpha(alpha);
            OnRender?.Invoke(alpha);

            if (YieldBetweenFrames)
                Thread.Sleep(1);
        }

        _stopwatch.Stop();
    }

    public void Stop() => IsRunning = false;
}
