using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Rex.Server.Core;
using Rex.Server.Simulation;
using Rex.Shared.Timing;

namespace Rex.Server;

/// <summary>
/// Top-level dedicated server application. Runs fixed simulation ticks (Unity <c>FixedUpdate</c>-style),
/// then optional variable-rate <see cref="OnUpdate"/> / <see cref="OnLateUpdate"/> for housekeeping.
/// </summary>
public sealed class ServerApp : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly GameServerConfig _config;
    private readonly TickClock _clock;
    private readonly DeltaTimeSmoother _deltaSmoother = new();
    private bool _isRunning;

    private GameServer? _server;

    public GameServerConfig Config => _config;
    public TickClock Clock => _clock;
    public GameServer? Server => _server;
    public bool IsRunning => _isRunning;

    /// <summary>Multiplies variable-phase <see cref="FrameContext.ScaledDeltaTime"/>; fixed ticks stay at config tick rate.</summary>
    public float TimeScale { get; set; } = 1f;

    /// <summary>Variable-rate phase after fixed ticks (metrics, admin hooks). Keep authoritative sim in <see cref="GameServer.Tick"/>.</summary>
    public Action<FrameContext>? OnUpdate { get; set; }

    /// <summary>Runs after <see cref="OnUpdate"/> each outer iteration.</summary>
    public Action<FrameContext>? OnLateUpdate { get; set; }

    public ServerApp(GameServerConfig config, ILoggerFactory loggerFactory)
    {
        _config = config;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<ServerApp>();
        _clock = new TickClock(config.TickRate);
    }

    /// <summary>Starts networking and the game loop. Blocks until stopped.</summary>
    public void Run()
    {
        _server = new GameServer(_config, _loggerFactory);
        _server.Start();

        _logger.LogInformation("Dedicated server running. Press Ctrl+C to stop.");

        _isRunning = true;
        var stopwatch = Stopwatch.StartNew();
        var previousTime = stopwatch.Elapsed.TotalSeconds;
        double accumulator = 0;
        ulong frameIndex = 0;

        while (_isRunning)
        {
            var currentTime = stopwatch.Elapsed.TotalSeconds;
            var frameTime = currentTime - previousTime;
            previousTime = currentTime;

            var fixedSteps = PhasedLoop.RunFixedSteps(_clock, ref accumulator, frameTime, () => _server!.Tick());

            var alpha = (float)(accumulator / _clock.TickInterval);
            _clock.SetAlpha(alpha);
            frameIndex++;

            var unscaledDt = (float)Math.Min(frameTime, PhasedLoop.DefaultMaxFrameSeconds);
            var smoothDt = _deltaSmoother.Next(unscaledDt);
            var ctx = new FrameContext(
                _clock,
                unscaledDt,
                smoothDt,
                TimeScale,
                fixedSteps,
                alpha,
                frameIndex,
                stopwatch.Elapsed.TotalSeconds);

            OnUpdate?.Invoke(ctx);
            OnLateUpdate?.Invoke(ctx);

            Thread.Yield();
        }

        stopwatch.Stop();

        _server.Shutdown();
        _server = null;
    }

    /// <summary>Signals the game loop to exit after the current frame.</summary>
    public void Stop()
    {
        _logger.LogInformation("Shutdown signal received.");
        _isRunning = false;
    }

    public void Dispose()
    {
        if (_server != null)
        {
            _server.Shutdown();
            _server = null;
        }
    }
}
