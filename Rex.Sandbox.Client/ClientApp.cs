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
using Rex.Shared.Numerics;
using Rex.Shared.Timing;

namespace Rex.Sandbox.Client;

/// <summary>
/// Client application for the Sandbox sample. It exercises the reusable engine runtime as an internal consumer.
/// </summary>
public sealed partial class ClientApp : IDisposable
{
    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ClientRuntimeHost _runtime;
    private readonly List<EntityState> _standaloneInterpolated = [];
    private readonly Dictionary<int, EntityState> _standaloneInterpPrevious = [];
    private List<EntityState> _currentEntities = [];
    private List<EntityState> _entitySnapshotBuffer0 = [];
    private List<EntityState> _entitySnapshotBuffer1 = [];

    private string _host = "127.0.0.1";
    private InputCollector? _inputCollector;
    private int _localEntityId;
    private int _port = ProtocolConstants.DefaultPort;
    private List<EntityState> _previousEntities = [];

    public ClientApp(NetMode mode, ILoggerFactory loggerFactory, int tickRate = ProtocolConstants.DefaultTickRate)
    {
        Mode = mode;
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

    private NetMode Mode { get; }

    private TickClock Clock => _runtime.Clock;
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

    public GameClient? Client { get; private set; }
    public GameWorld? World { get; private set; }

    public IGameWindow? Window
    {
        get => _runtime.Window;
        set => _runtime.Window = value;
    }

    public ISandboxWorldRenderer? Renderer { get; set; }

    /// <summary>
    /// Releases resources owned by this instance.
    /// </summary>
    public void Dispose()
    {
        Renderer?.Dispose();
        _runtime.Dispose();
    }

    public void SetInputCollector(InputCollector collector)
    {
        _inputCollector = collector;
    }

    public void Run(string? host = null, int port = ProtocolConstants.DefaultPort,
        CancellationToken cancellationToken = default)
    {
        if (Mode == NetMode.DedicatedServer)
        {
            LogInvalidClientNetMode(Mode);
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

    private void FixedStep()
    {
        if (Mode == NetMode.Standalone)
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
        switch (Mode)
        {
            case NetMode.Standalone:
                SetupStandalone();
                break;
            case NetMode.Client:
            case NetMode.ListenServer:
                SetupNetworked(_host, _port);
                break;
            case NetMode.DedicatedServer:
                LogDedicatedServerRequestedOnAClient();
                break;
        }

        if (!Headless && Window != null)
        {
            Renderer?.Initialize(Window);
        }

        LogClientRunning(Mode);
    }

    private void SetupStandalone()
    {
        World = new GameWorld();
        _localEntityId = World.SpawnEntity(Guid.Empty, EntityTypeIds.Player, 0f, 0f, 0f);
        LogStandaloneWorldInitialized();
    }

    internal void InitializeStandaloneForTesting()
    {
        SetupStandalone();
    }

    internal void InitializeNetworkedForTesting(string? host = null, int port = ProtocolConstants.DefaultPort)
    {
        SetupNetworked(host ?? "127.0.0.1", port, autoConnect: false);
    }

    private void SetupNetworked(string host, int port, bool autoConnect = true)
    {
        Client = new GameClient(_loggerFactory);
        if (_inputCollector != null)
        {
            Client.SetInputCollector(_inputCollector);
        }

        if (autoConnect)
        {
            Client.Connect(host, port);
        }
    }

    private void TickStandalone()
    {
        if (_inputCollector != null)
        {
            PlayerInputMessage input = _inputCollector.Sample(Clock.CurrentTick);
            World!.ProcessInput(_localEntityId, input);
        }

        World!.Tick(1.0f / Clock.TickRate);

        (_entitySnapshotBuffer0, _entitySnapshotBuffer1) = (_entitySnapshotBuffer1, _entitySnapshotBuffer0);
        _previousEntities = _entitySnapshotBuffer0;
        List<EntityState> newCurrent = _entitySnapshotBuffer1;
        newCurrent.Clear();
        foreach (EntityState state in World.Entities.Values)
        {
            newCurrent.Add(state);
        }

        _currentEntities = newCurrent;
    }

    private void TickNetworked()
    {
        Client!.Tick(Clock.CurrentTick);
    }

    private void Render(FrameContext ctx)
    {
        if (Headless)
        {
            return;
        }

        if (Renderer != null)
        {
            Renderer.BeginFrame();
            IReadOnlyList<EntityState> entities = Mode == NetMode.Standalone
                ? InterpolateStandalone(ctx.InterpolationAlpha)
                : Client!.WorldState.GetInterpolatedState(ctx.InterpolationAlpha);
            Renderer.RenderWorld(entities, ctx.InterpolationAlpha);
            Renderer.EndFrame();
        }
    }

    private List<EntityState> InterpolateStandalone(float alpha)
    {
        if (_currentEntities.Count == 0 || _previousEntities.Count == 0)
        {
            return _currentEntities;
        }

        _standaloneInterpPrevious.Clear();
        foreach (EntityState entity in _previousEntities)
        {
            _standaloneInterpPrevious[entity.EntityId] = entity;
        }

        _standaloneInterpolated.Clear();
        foreach (EntityState current in _currentEntities)
        {
            if (_standaloneInterpPrevious.TryGetValue(current.EntityId, out EntityState? previous))
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
        Client?.Disconnect();
    }

    [LoggerMessage(LogLevel.Warning, "Dedicated server requested on a client?")]
    partial void LogDedicatedServerRequestedOnAClient();
}

internal static class EntityStateInterpolation
{
    public static EntityState Lerp(EntityState previous, EntityState current, float alpha) =>
        new(
            current.EntityId,
            float.Lerp(previous.X, current.X, alpha),
            float.Lerp(previous.Y, current.Y, alpha),
            float.Lerp(previous.Z, current.Z, alpha),
            AngleMath.LerpAngleDegrees(previous.RotationY, current.RotationY, alpha));
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
