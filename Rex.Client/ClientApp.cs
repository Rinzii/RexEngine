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
/// Top-level client application. Runs a Unity-style loop: fixed simulation ticks, then variable-rate
/// <see cref="OnUpdate"/> / <see cref="OnLateUpdate"/>, then render with interpolation alpha.
/// </summary>
public sealed class ClientApp : IDisposable
{
    private readonly NetMode _mode;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly TickClock _clock;
    private readonly DeltaTimeSmoother _deltaSmoother = new();
    private bool _isRunning;

    // Networked modes only.
    private GameClient? _client;

    // Standalone mode: direct simulation, no networking.
    private GameWorld? _world;
    private int _localEntityId;
    private IReadOnlyList<EntityState> _previousEntities = Array.Empty<EntityState>();
    private IReadOnlyList<EntityState> _currentEntities = Array.Empty<EntityState>();

    // Input source (used by both standalone and networked modes).
    private InputCollector? _inputCollector;

    // Windowing and rendering (set before Run).
    private IGameWindow? _window;
    private IRenderer? _renderer;

    public NetMode Mode => _mode;
    public TickClock Clock => _clock;
    public bool IsRunning => _isRunning;
    public bool Headless { get; init; }

    /// <summary>
    /// Multiplies variable-phase <see cref="FrameContext.ScaledDeltaTime"/>; 0 freezes scaled time, 1 is normal.
    /// Fixed simulation ticks stay at <see cref="TickClock.TickRate"/> (unlike Unity, where <c>timeScale</c> also slows physics).
    /// </summary>
    public float TimeScale { get; set; } = 1f;

    /// <summary>Variable-rate phase after fixed ticks; use for cameras, UI, non-authoritative motion.</summary>
    public Action<FrameContext>? OnUpdate { get; set; }

    /// <summary>Runs after <see cref="OnUpdate"/> each frame; use when another system must read updated transform-like state.</summary>
    public Action<FrameContext>? OnLateUpdate { get; set; }

    /// <summary>The networked client. Only available in Client/ListenServer modes.</summary>
    public GameClient? Client => _client;

    /// <summary>The local world. Only available in Standalone mode.</summary>
    public GameWorld? World => _world;

    /// <summary>Optional window. Set before calling Run.</summary>
    public IGameWindow? Window
    {
        get => _window;
        set => _window = value;
    }

    /// <summary>Optional renderer. Set before calling Run.</summary>
    public IRenderer? Renderer
    {
        get => _renderer;
        set => _renderer = value;
    }

    /// <param name="tickRate">Sim ticks per second (fixed delta = 1/<paramref name="tickRate"/>).</param>
    public ClientApp(NetMode mode, ILoggerFactory loggerFactory, int tickRate = ProtocolConstants.DefaultTickRate)
    {
        _mode = mode;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<ClientApp>();
        _clock = new TickClock(tickRate);
    }

    /// <summary>Sets the input source sampled each tick.</summary>
    public void SetInputCollector(InputCollector collector)
    {
        _inputCollector = collector;
    }

    /// <summary>Initializes for the configured net mode, runs the game loop, and blocks until exit.</summary>
    public void Run(string? host = null, int port = ProtocolConstants.DefaultPort)
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
            default:
                _logger.LogError("Net mode {Mode} is not valid for the client.", _mode);
                return;
        }

        _logger.LogInformation("Client running in {Mode} mode.", _mode);
        RunMainLoop();

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

    private void RunMainLoop()
    {
        _isRunning = true;
        var stopwatch = Stopwatch.StartNew();
        var previousTime = stopwatch.Elapsed.TotalSeconds;
        double accumulator = 0;
        ulong frameIndex = 0;

        void FixedStep()
        {
            if (_mode == NetMode.Standalone)
                TickStandalone();
            else
                TickNetworked();
        }

        while (_isRunning)
        {
            var currentTime = stopwatch.Elapsed.TotalSeconds;
            var frameTime = currentTime - previousTime;
            previousTime = currentTime;

            var fixedSteps = PhasedLoop.RunFixedSteps(_clock, ref accumulator, frameTime, FixedStep);

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

            if (!Headless)
            {
                _window?.PollEvents();
                if (_window != null && !_window.IsOpen)
                {
                    Stop();
                    break;
                }
            }

            OnUpdate?.Invoke(ctx);
            OnLateUpdate?.Invoke(ctx);

            Render(ctx);

            if (Headless)
                Thread.Yield();
        }

        stopwatch.Stop();
    }

    private void InitializeWindow()
    {
        if (Headless || _window == null)
            return;

        _window.Open("RexEngine", 1280, 720);
        _renderer?.Initialize(_window);
    }

    private void SetupStandalone()
    {
        _world = new GameWorld();
        _localEntityId = _world.SpawnEntity(0, EntityTypeIds.Player, 0f, 0f, 0f);

        _logger.LogInformation("Standalone world initialized.");
    }

    private void SetupNetworked(string host, int port)
    {
        _client = new GameClient(_loggerFactory);

        if (_inputCollector != null)
            _client.SetInputCollector(_inputCollector);

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

        _previousEntities = _currentEntities;
        _currentEntities = new List<EntityState>(_world.Entities.Values);
    }

    private void TickNetworked()
    {
        _client!.Tick(_clock.CurrentTick);
    }

    private void Render(FrameContext ctx)
    {
        if (Headless)
            return;

        if (_renderer != null)
        {
            _renderer.BeginFrame();

            var entities = _mode == NetMode.Standalone
                ? InterpolateStandalone(ctx.InterpolationAlpha)
                : _client!.WorldState.GetInterpolatedState(ctx.InterpolationAlpha);

            _renderer.RenderWorld(entities, ctx.InterpolationAlpha);
            _renderer.EndFrame();
        }

        _window?.SwapBuffers();
    }

    /// <summary>Lerps between previous and current tick entity states for standalone mode.</summary>
    private IReadOnlyList<EntityState> InterpolateStandalone(float alpha)
    {
        if (_currentEntities.Count == 0)
            return _currentEntities;

        if (_previousEntities.Count == 0)
            return _currentEntities;

        var previousLookup = new Dictionary<int, EntityState>();
        foreach (var entity in _previousEntities)
            previousLookup[entity.EntityId] = entity;

        var result = new List<EntityState>(_currentEntities.Count);
        foreach (var current in _currentEntities)
        {
            if (previousLookup.TryGetValue(current.EntityId, out var previous))
            {
                var x = previous.X + (current.X - previous.X) * alpha;
                var y = previous.Y + (current.Y - previous.Y) * alpha;
                var z = previous.Z + (current.Z - previous.Z) * alpha;
                var rotY = previous.RotationY + (current.RotationY - previous.RotationY) * alpha;
                result.Add(new EntityState(current.EntityId, x, y, z, rotY));
            }
            else
            {
                result.Add(current);
            }
        }

        return result;
    }

    private void Shutdown()
    {
        _client?.Disconnect();
        _window?.Close();
    }
}
