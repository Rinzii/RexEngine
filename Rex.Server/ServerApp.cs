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
    private readonly GameLoop _gameLoop;

    private GameServer? _server;

    public GameServerConfig Config => _config;
    public GameLoop GameLoop => _gameLoop;
    public GameServer? Server => _server;
    public bool IsRunning => _gameLoop.IsRunning;

    public ServerApp(GameServerConfig config, ILoggerFactory loggerFactory)
    {
        _config = config;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<ServerApp>();
        _gameLoop = new GameLoop(config.TickRate) { YieldBetweenFrames = true };
    }

    /// <summary>Starts networking and the game loop. Blocks until stopped.</summary>
    public void Run()
    {
        _server = new GameServer(_config, _loggerFactory);
        _server.Start();

        _gameLoop.OnTick = _server.Tick;

        _logger.LogInformation("Dedicated server running. Press Ctrl+C to stop.");
        _gameLoop.Run();

        _server.Shutdown();
        _server = null;
    }

    /// <summary>Signals the game loop to exit after the current frame.</summary>
    public void Stop()
    {
        _logger.LogInformation("Shutdown signal received.");
        _gameLoop.Stop();
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
