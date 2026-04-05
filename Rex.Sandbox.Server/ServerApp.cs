using Microsoft.Extensions.Logging;
using Rex.Sandbox.Server.Core;
using Rex.Sandbox.Server.Simulation;
using Rex.Server.Runtime;
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
    private readonly ILoggerFactory _loggerFactory;
    private readonly ServerRuntimeHost _runtime;

    private GameServer? _server;

    public GameServerConfig Config => _config;
    public TickClock Clock => _runtime.Clock;
    public GameServer? Server => _server;
    public bool IsRunning => _runtime.IsRunning;
    public float TimeScale
    {
        get => _runtime.TimeScale;
        set => _runtime.TimeScale = value;
    }

    public Action<FrameContext>? OnUpdate
    {
        get => _runtime.OnUpdate;
        set => _runtime.OnUpdate = value;
    }

    public Action<FrameContext>? OnLateUpdate
    {
        get => _runtime.OnLateUpdate;
        set => _runtime.OnLateUpdate = value;
    }

    public ServerApp(GameServerConfig config, ILoggerFactory loggerFactory)
    {
        _config = config;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<ServerApp>();
        _runtime = new ServerRuntimeHost(
            new ServerRuntimeOptions { TickRate = config.TickRate },
            loggerFactory)
        {
            OnInitialize = InitializeServer,
            OnFixedUpdate = TickServer,
            OnShutdown = ShutdownServer
        };
    }

    public void Run(CancellationToken cancellationToken = default)
    {
        _runtime.Run(cancellationToken);
    }

    public void Stop()
    {
        LogShutdownSignalReceived();
        _runtime.Stop();
    }

    public void Dispose()
    {
        _runtime.Dispose();
        _server?.Shutdown();
        _server = null;
    }

    private void InitializeServer()
    {
        _server = new GameServer(_config, _loggerFactory);
        _server.Start();
        LogDedicatedServerRunning();
    }

    private void TickServer()
    {
        _server!.Tick();
    }

    private void ShutdownServer()
    {
        _server?.Shutdown();
        _server = null;
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
