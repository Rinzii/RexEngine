using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Rex.Sandbox.Server.Core;
using Rex.Sandbox.Server.Simulation;
using Rex.Shared.Logging;
using Rex.Shared.Timing;

namespace Rex.Sandbox.Server;

/// <summary>
/// Sandbox-owned dedicated server loop. The reusable server transport stays in `Rex.Server`.
/// </summary>
public sealed partial class ServerApp : IDisposable
{
    private readonly ILogger _logger;
    private readonly GameServerConfig _config;
    private readonly TickClock _clock;
    private readonly DeltaTimeSmoother _deltaSmoother = new();
    private readonly ILoggerFactory _loggerFactory;
    private bool _isRunning;

    private GameServer? _server;

    public GameServerConfig Config => _config;
    public TickClock Clock => _clock;
    public GameServer? Server => _server;
    public bool IsRunning => _isRunning;
    public float TimeScale { get; set; } = 1f;
    public Action<FrameContext>? OnUpdate { get; set; }
    public Action<FrameContext>? OnLateUpdate { get; set; }

    public ServerApp(GameServerConfig config, ILoggerFactory loggerFactory)
    {
        _config = config;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<ServerApp>();
        _clock = new TickClock(config.TickRate);
    }

    public void Run(CancellationToken cancellationToken = default)
    {
        _server = new GameServer(_config, _loggerFactory);
        _server.Start();

        LogDedicatedServerRunning();

        _isRunning = true;
        var stopwatch = Stopwatch.StartNew();
        var previousTime = stopwatch.Elapsed.TotalSeconds;
        double accumulator = 0;
        ulong frameIndex = 0;

        while (_isRunning && !cancellationToken.IsCancellationRequested)
        {
            var currentTime = stopwatch.Elapsed.TotalSeconds;
            var frameTime = currentTime - previousTime;
            previousTime = currentTime;

            var fixedSteps = PhasedLoop.RunFixedSteps(_clock, ref accumulator, frameTime, () => _server!.Tick());
            var alpha = (float)(accumulator / _clock.TickInterval);
            _clock.SetAlpha(alpha);
            frameIndex++;

            var unscaledDt = Math.Min((float)frameTime, PhasedLoop.DefaultMaxFrameSeconds);
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

            InvokeUpdateCallbacks(ctx);
            Thread.Yield();
        }

        stopwatch.Stop();
        _server.Shutdown();
        _server = null;
    }

    public void Stop()
    {
        LogShutdownSignalReceived();
        _isRunning = false;
    }

    public void Dispose()
    {
        _server?.Shutdown();
        _server = null;
    }

    private void InvokeUpdateCallbacks(FrameContext ctx)
    {
        if (OnUpdate != null)
        {
            try
            {
                OnUpdate(ctx);
            }
            catch (Exception ex)
            {
                LogOnUpdateFailed(ex);
            }
        }

        if (OnLateUpdate != null)
        {
            try
            {
                OnLateUpdate(ctx);
            }
            catch (Exception ex)
            {
                LogOnLateUpdateFailed(ex);
            }
        }
    }
}

public sealed partial class ServerApp
{
    [LoggerMessage(EventId = LogEventIds.ServerApp.DedicatedServerRunning, Level = LogLevel.Information,
        Message = "Sandbox dedicated server running. Press Ctrl+C to stop.")]
    private partial void LogDedicatedServerRunning();

    [LoggerMessage(EventId = LogEventIds.ServerApp.ShutdownSignal, Level = LogLevel.Information,
        Message = "Shutdown signal received.")]
    private partial void LogShutdownSignalReceived();

    [LoggerMessage(EventId = LogEventIds.ServerApp.OnUpdateFailed, Level = LogLevel.Error,
        Message = "OnUpdate threw an exception.")]
    private partial void LogOnUpdateFailed(Exception ex);

    [LoggerMessage(EventId = LogEventIds.ServerApp.OnLateUpdateFailed, Level = LogLevel.Error,
        Message = "OnLateUpdate threw an exception.")]
    private partial void LogOnLateUpdateFailed(Exception ex);
}
