using Microsoft.Extensions.Logging;
using Rex.Sandbox.Server.Core;
using Rex.Sandbox.Server.Simulation;
using Rex.Server.Runtime;
using Rex.Shared.Logging;
using Rex.Shared.Timing;

namespace Rex.Sandbox.Server;

/// <summary>
/// Dedicated server loop for the Sandbox sample. The reusable server transport stays in `Rex.Server`.
/// </summary>
public sealed partial class ServerApp : IDisposable
{
    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ServerRuntimeHost _runtime;

    public ServerApp(GameServerConfig config, ILoggerFactory loggerFactory)
    {
        Config = config;
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

    public GameServerConfig Config { get; }
    public TickClock Clock => _runtime.Clock;
    public GameServer? Server { get; private set; }
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

    /// <summary>
    /// Releases resources owned by this instance.
    /// </summary>
    public void Dispose()
    {
        _runtime.Dispose();
        Server?.Shutdown();
        Server = null;
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

    private void InitializeServer()
    {
        Server = new GameServer(Config, _loggerFactory);
        Server.Start();
        LogDedicatedServerRunning();
    }

    private void TickServer()
    {
        Server!.Tick();
    }

    private void ShutdownServer()
    {
        Server?.Shutdown();
        Server = null;
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
