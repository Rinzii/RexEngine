using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Rex.Server.Core;
using Rex.Server.Simulation;
using Rex.Shared.Timing;

namespace Rex.Server;

/// <summary>
/// Top-level dedicated server application. Owns the game loop, networking,
/// and simulation lifecycle. Runs as a headless console process.
/// </summary>
public sealed class ServerApp : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly GameServerConfig _config;
    private readonly TickClock _clock;
    private bool _isRunning;

    private GameServer? _server;

    public GameServerConfig Config => _config;
    public TickClock Clock => _clock;
    public GameServer? Server => _server;
    public bool IsRunning => _isRunning;

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

        while (_isRunning)
        {
            var currentTime = stopwatch.Elapsed.TotalSeconds;
            var frameTime = currentTime - previousTime;
            previousTime = currentTime;

            if (frameTime > 0.25)
                frameTime = 0.25;

            accumulator += frameTime;

            while (accumulator >= _clock.TickInterval)
            {
                _server.Tick();
                _clock.IncrementTick();
                accumulator -= _clock.TickInterval;
            }

            var alpha = (float)(accumulator / _clock.TickInterval);
            _clock.SetAlpha(alpha);

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
