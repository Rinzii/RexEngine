using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Rex.Client.Graphics;
using Rex.Client.Input;
using Rex.Client.Net;
using Rex.Shared;
using Rex.Shared.Net;
using Rex.Shared.Net.Messages;
using Rex.Shared.Simulation;
using Rex.Shared.Timing;

namespace Rex.Client;

/// <summary>
/// Top-level client application. Runs fixed-step simulation, then variable-rate <see cref="OnUpdate"/> and
/// <see cref="OnLateUpdate"/>, then draws with interpolation between simulation ticks.
/// </summary>
public sealed partial class ClientApp : IDisposable
{
    private readonly NetMode _mode;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly TickClock _clock;
    private readonly DeltaTimeSmoother _deltaSmoother = new();
    private bool _isRunning;

    // Networked play only (client or listen server).
    private GameClient? _client;

    // Standalone only. Local world, no network session.
    private GameWorld? _world;
    private int _localEntityId;
    private IReadOnlyList<EntityState> _previousEntities = Array.Empty<EntityState>();
    private IReadOnlyList<EntityState> _currentEntities = Array.Empty<EntityState>();
    private List<EntityState> _entitySnapshotBuffer0 = [];
    private List<EntityState> _entitySnapshotBuffer1 = [];
    private readonly Dictionary<int, EntityState> _standaloneInterpPrevious = [];
    private readonly List<EntityState> _standaloneInterpolated = [];

    // Sampled once per fixed tick in both standalone and networked modes.
    private InputCollector? _inputCollector;

    // Optional. Set before Run().
    private IGameWindow? _window;
    private IRenderer? _renderer;

    public NetMode Mode => _mode;
    public TickClock Clock => _clock;
    public bool IsRunning => _isRunning;
    public bool Headless { get; init; }

    /// <summary>
    /// Multiplies variable-phase <see cref="FrameContext.ScaledDeltaTime"/>. Use 0 to freeze scaled time and 1 for normal speed.
    /// Fixed ticks always use <see cref="TickClock.TickRate"/>. Rex does not scale fixed ticks.
    /// </summary>
    public float TimeScale { get; set; } = 1f;

    /// <summary>Variable-rate work after fixed ticks. Cameras, UI, or non-authoritative motion.</summary>
    public Action<FrameContext>? OnUpdate { get; set; }

    /// <summary>Runs after <see cref="OnUpdate"/>. For logic that reads state that <see cref="OnUpdate"/> may have changed.</summary>
    public Action<FrameContext>? OnLateUpdate { get; set; }

    /// <summary>Network session. Non-null in Client and ListenServer modes.</summary>
    public GameClient? Client => _client;

    /// <summary>Local simulation world. Non-null in Standalone mode.</summary>
    public GameWorld? World => _world;

    /// <summary>Optional window. Set before <see cref="Run"/>.</summary>
    public IGameWindow? Window
    {
        get => _window;
        set => _window = value;
    }

    /// <summary>Optional renderer. Set before <see cref="Run"/>.</summary>
    public IRenderer? Renderer
    {
        get => _renderer;
        set => _renderer = value;
    }

    /// <summary>Creates a client with a fixed-rate simulation clock and the given networking role.</summary>
    /// <param name="mode">Standalone, remote client, listen server, or dedicated server enum value. See <see cref="NetMode"/>.</param>
    /// <param name="loggerFactory">Creates loggers for this app and nested systems such as networking.</param>
    /// <param name="tickRate">Simulation ticks per second. Each fixed step advances sim time by <c>1 / tickRate</c> seconds.</param>
    public ClientApp(NetMode mode, ILoggerFactory loggerFactory, int tickRate = ProtocolConstants.DefaultTickRate)
    {
        _mode = mode;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<ClientApp>();
        _clock = new TickClock(tickRate);
    }

    /// <summary>Registers input sampled each fixed tick.</summary>
    public void SetInputCollector(InputCollector collector)
    {
        _inputCollector = collector;
    }

    /// <summary>Wires the chosen net mode, runs the main loop until stop, then shuts down.</summary>
    /// <param name="port"></param>
    /// <param name="cancellationToken">When canceled, the loop exits after the current frame.</param>
    /// <param name="host"></param>
    public void Run(string? host = null, int port = ProtocolConstants.DefaultPort, CancellationToken cancellationToken = default)
    {
        InitializeWindow();

        switch (_mode)
        {
            case NetMode.Standalone:
                SetupStandalone();
                break;
            case NetMode.Client:
            case NetMode.ListenServer:
                SetupNetworked(host ?? "127.0.0.1", port);
                break;
            case NetMode.DedicatedServer:
            default:
                LogInvalidClientNetMode(_mode);
                return;
        }

        LogClientRunning(_mode);

        RunMainLoop(cancellationToken);

        Shutdown();
    }

    public void Stop()
    {
        _isRunning = false;
    }

    public void Dispose()
    {
        _renderer?.Dispose();
        _window?.Dispose();
    }

    private void RunMainLoop(CancellationToken cancellationToken)
    {
        _isRunning = true;
        var stopwatch = Stopwatch.StartNew();
        var previousTime = stopwatch.Elapsed.TotalSeconds;
        double accumulator = 0;
        ulong frameIndex = 0;

        void FixedStep()
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

        while (_isRunning && !cancellationToken.IsCancellationRequested)
        {
            var currentTime = stopwatch.Elapsed.TotalSeconds;
            var frameTime = currentTime - previousTime;
            previousTime = currentTime;

            // Fixed sim may run zero or many times so wall time stays in sync with tick rate.
            var fixedSteps = PhasedLoop.RunFixedSteps(_clock, ref accumulator, frameTime, FixedStep);

            // How far we are between the last tick and the next, for draw interpolation.
            var alpha = (float)(accumulator / _clock.TickInterval);
            _clock.SetAlpha(alpha);
            frameIndex++;

            // Same cap as the accumulator uses, so Update sees a consistent frame length.
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

            if (!Headless)
            {
                _window?.PollEvents();
                if (_window is { IsOpen: false })
                {
                    Stop();
                    break;
                }
            }

            InvokeUpdateCallbacks(ctx);

            Render(ctx);

            if (Headless)
            {
                Thread.Yield();
            }
        }

        if (cancellationToken.IsCancellationRequested)
        {
            LogMainLoopCancellationRequested();
        }

        stopwatch.Stop();
    }

    private void InitializeWindow()
    {
        if (Headless || _window == null)
        {
            return;
        }

        _window.Open("RexEngine", 1280, 720);
        _renderer?.Initialize(_window);
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
            var input = _inputCollector.Sample(_clock.CurrentTick);
            _world!.ProcessInput(_localEntityId, input);
        }

        var deltaTime = 1.0f / _clock.TickRate;
        _world!.Tick(deltaTime);

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
        _client!.Tick(_clock.CurrentTick);
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

            // Standalone lerps local snapshots. Networked uses predicted or interpolated server state.
            var entities = _mode == NetMode.Standalone
                ? InterpolateStandalone(ctx.InterpolationAlpha)
                : _client!.WorldState.GetInterpolatedState(ctx.InterpolationAlpha);

            _renderer.RenderWorld(entities, ctx.InterpolationAlpha);
            _renderer.EndFrame();
        }

        _window?.SwapBuffers();
    }

    /// <summary>Linear blend between entity state at the previous tick and the current tick. Standalone draw path.</summary>
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
                var x = previous.X + (current.X - previous.X) * alpha;
                var y = previous.Y + (current.Y - previous.Y) * alpha;
                var z = previous.Z + (current.Z - previous.Z) * alpha;
                var rotY = previous.RotationY + (current.RotationY - previous.RotationY) * alpha;
                _standaloneInterpolated.Add(new EntityState(current.EntityId, x, y, z, rotY));
            }
            else
            {
                _standaloneInterpolated.Add(current);
            }
        }

        return _standaloneInterpolated;
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

    private void Shutdown()
    {
        _client?.Disconnect();
        _window?.Close();
    }
}
