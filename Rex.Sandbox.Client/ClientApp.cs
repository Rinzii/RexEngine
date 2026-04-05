using Microsoft.Extensions.Logging;
using Rex.Client.Graphics;
using Rex.Client.Runtime;
using Rex.Sandbox.Client.Graphics;
using Rex.Sandbox.Client.Input;
using Rex.Sandbox.Client.Net;
using Rex.Sandbox.Shared.Net.Messages;
using Rex.Sandbox.Shared.Simulation;
using Rex.Shared.Logging;
using Rex.Shared.Net;
using Rex.Shared.Timing;

namespace Rex.Sandbox.Client;

/// <summary>
/// Client application for the Sandbox sample. It exercises the reusable engine runtime as an internal consumer.
/// </summary>
public sealed partial class ClientApp : IDisposable
{
    private readonly NetMode _mode;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly ClientRuntimeHost _runtime;

    private GameClient? _client;
    private GameWorld? _world;
    private int _localEntityId;
    private IReadOnlyList<EntityState> _previousEntities = Array.Empty<EntityState>();
    private IReadOnlyList<EntityState> _currentEntities = Array.Empty<EntityState>();
    private List<EntityState> _entitySnapshotBuffer0 = [];
    private List<EntityState> _entitySnapshotBuffer1 = [];
    private readonly Dictionary<int, EntityState> _standaloneInterpPrevious = [];
    private readonly List<EntityState> _standaloneInterpolated = [];

    private string _host = "127.0.0.1";
    private int _port = ProtocolConstants.DefaultPort;
    private InputCollector? _inputCollector;
    private ISandboxWorldRenderer? _renderer;

    public NetMode Mode => _mode;
    public TickClock Clock => _runtime.Clock;
    public bool IsRunning => _runtime.IsRunning;

    public bool Headless
    {
        get => _runtime.Options.Headless;
        init => _runtime.Options.Headless = value;
    }

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

    public GameClient? Client => _client;
    public GameWorld? World => _world;

    public IGameWindow? Window
    {
        get => _runtime.Window;
        set => _runtime.Window = value;
    }
    public ISandboxWorldRenderer? Renderer
    {
        get => _renderer;
        set => _renderer = value;
    }
    public ClientApp(NetMode mode, ILoggerFactory loggerFactory, int tickRate = ProtocolConstants.DefaultTickRate)
    {
        _mode = mode;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<ClientApp>();
        _runtime = new ClientRuntimeHost(
            new ClientRuntimeOptions
            {
                TickRate = tickRate,
                WindowTitle = "Rex Sandbox"
            },
            loggerFactory)
        {
            OnInitialize = InitializeRuntime,
            OnFixedUpdate = FixedStep,
            OnRender = Render,
            OnShutdown = Shutdown
        };
    }

    public void SetInputCollector(InputCollector collector)
    {
        _inputCollector = collector;
    }

    public void Run(string? host = null, int port = ProtocolConstants.DefaultPort, CancellationToken cancellationToken = default)
    {
        if (_mode == NetMode.DedicatedServer)
        {
            LogInvalidClientNetMode(_mode);
            return;
        }

        _host = host ?? "127.0.0.1";
        _port = port;
        _runtime.Run(cancellationToken);
    }

    public void Stop()
    {
        _runtime.Stop();
    }

    /// <summary>
    /// Releases resources owned by this instance.
    /// </summary>
    public void Dispose()
    {
        _renderer?.Dispose();
        _runtime.Dispose();
    }

    private void FixedStep()
    {
        if (_mode == NetMode.Standalone)
        {
            TickStandalone();
        }
        else
        {
            TickNetworked();
        }
    }

    private void InitializeRuntime()
    {
        switch (_mode)
        {
            case NetMode.Standalone:
                SetupStandalone();
                break;
            case NetMode.Client:
            case NetMode.ListenServer:
                SetupNetworked(_host, _port);
                break;
        }

        if (!Headless && Window != null)
        {
            _renderer?.Initialize(Window);
        }

        LogClientRunning(_mode);
    }

    private void SetupStandalone()
    {
        _world = new GameWorld();
        _localEntityId = _world.SpawnEntity(Guid.Empty, EntityTypeIds.Player, 0f, 0f, 0f);
        LogStandaloneWorldInitialized();
    }

    private void SetupNetworked(string host, int port)
    {
        _client = new GameClient(_loggerFactory);
        if (_inputCollector != null)
        {
            _client.SetInputCollector(_inputCollector);
        }

        _client.Connect(host, port);
    }

    private void TickStandalone()
    {
        if (_inputCollector != null)
        {
            var input = _inputCollector.Sample(Clock.CurrentTick);
            _world!.ProcessInput(_localEntityId, input);
        }

        _world!.Tick(1.0f / Clock.TickRate);

        (_entitySnapshotBuffer0, _entitySnapshotBuffer1) = (_entitySnapshotBuffer1, _entitySnapshotBuffer0);
        _previousEntities = _entitySnapshotBuffer0;
        var newCurrent = _entitySnapshotBuffer1;
        newCurrent.Clear();
        foreach (var state in _world.Entities.Values)
        {
            newCurrent.Add(state);
        }

        _currentEntities = newCurrent;
    }

    private void TickNetworked()
    {
        _client!.Tick(Clock.CurrentTick);
    }

    private void Render(FrameContext ctx)
    {
        if (Headless)
        {
            return;
        }

        if (_renderer != null)
        {
            _renderer.BeginFrame();
            var entities = _mode == NetMode.Standalone
                ? InterpolateStandalone(ctx.InterpolationAlpha)
                : _client!.WorldState.GetInterpolatedState(ctx.InterpolationAlpha);
            _renderer.RenderWorld(entities, ctx.InterpolationAlpha);
            _renderer.EndFrame();
        }
    }

    private IReadOnlyList<EntityState> InterpolateStandalone(float alpha)
    {
        if (_currentEntities.Count == 0 || _previousEntities.Count == 0)
        {
            return _currentEntities;
        }

        _standaloneInterpPrevious.Clear();
        foreach (var entity in _previousEntities)
        {
            _standaloneInterpPrevious[entity.EntityId] = entity;
        }

        _standaloneInterpolated.Clear();
        foreach (var current in _currentEntities)
        {
            if (_standaloneInterpPrevious.TryGetValue(current.EntityId, out var previous))
            {
                _standaloneInterpolated.Add(EntityStateInterpolation.Lerp(previous, current, alpha));
            }
            else
            {
                _standaloneInterpolated.Add(current);
            }
        }

        return _standaloneInterpolated;
    }

    private void Shutdown()
    {
        _client?.Disconnect();
    }
}

internal static class EntityStateInterpolation
{
    public static EntityState Lerp(EntityState previous, EntityState current, float alpha) =>
        new(
            current.EntityId,
            float.Lerp(previous.X, current.X, alpha),
            float.Lerp(previous.Y, current.Y, alpha),
            float.Lerp(previous.Z, current.Z, alpha),
            Rex.Shared.Numerics.AngleMath.LerpAngleDegrees(previous.RotationY, current.RotationY, alpha));
}

public sealed partial class ClientApp
{
    [LoggerMessage(EventId = LogEventIds.ClientApp.InvalidNetMode, Level = LogLevel.Error,
        Message = "Net mode {Mode} is not valid for the Sandbox client.")]
    private partial void LogInvalidClientNetMode(NetMode mode);

    [LoggerMessage(EventId = LogEventIds.ClientApp.ClientRunning, Level = LogLevel.Information,
        Message = "Sandbox client running in {Mode} mode.")]
    private partial void LogClientRunning(NetMode mode);

    [LoggerMessage(EventId = LogEventIds.ClientApp.StandaloneWorldInitialized, Level = LogLevel.Information,
        Message = "Sandbox standalone world initialized.")]
    private partial void LogStandaloneWorldInitialized();

}
