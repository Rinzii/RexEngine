using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Rex.Client.Graphics;
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
/// Sandbox-owned client application that exercises the reusable engine runtime as an internal consumer.
/// </summary>
public sealed partial class ClientApp : IDisposable
{
    private readonly NetMode _mode;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly TickClock _clock;
    private readonly DeltaTimeSmoother _deltaSmoother = new();
    private bool _isRunning;

    private GameClient? _client;
    private GameWorld? _world;
    private int _localEntityId;
    private IReadOnlyList<EntityState> _previousEntities = Array.Empty<EntityState>();
    private IReadOnlyList<EntityState> _currentEntities = Array.Empty<EntityState>();
    private List<EntityState> _entitySnapshotBuffer0 = [];
    private List<EntityState> _entitySnapshotBuffer1 = [];
    private readonly Dictionary<int, EntityState> _standaloneInterpPrevious = [];
    private readonly List<EntityState> _standaloneInterpolated = [];

    private InputCollector? _inputCollector;
    private IGameWindow? _window;
    private IRenderer? _renderer;

    public NetMode Mode => _mode;
    public TickClock Clock => _clock;
    public bool IsRunning => _isRunning;
    public bool Headless { get; init; }
    public float TimeScale { get; set; } = 1f;
    public Action<FrameContext>? OnUpdate { get; set; }
    public Action<FrameContext>? OnLateUpdate { get; set; }
    public GameClient? Client => _client;
    public GameWorld? World => _world;

    public IGameWindow? Window
    {
        get => _window;
        set => _window = value;
    }

    public IRenderer? Renderer
    {
        get => _renderer;
        set => _renderer = value;
    }

    public ClientApp(NetMode mode, ILoggerFactory loggerFactory, int tickRate = ProtocolConstants.DefaultTickRate)
    {
        _mode = mode;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<ClientApp>();
        _clock = new TickClock(tickRate);
    }

    public void SetInputCollector(InputCollector collector)
    {
        _inputCollector = collector;
    }

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

        while (_isRunning && !cancellationToken.IsCancellationRequested)
        {
            var currentTime = stopwatch.Elapsed.TotalSeconds;
            var frameTime = currentTime - previousTime;
            previousTime = currentTime;

            var fixedSteps = PhasedLoop.RunFixedSteps(_clock, ref accumulator, frameTime, FixedStep);
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

    private void InitializeWindow()
    {
        if (Headless || _window == null)
        {
            return;
        }

        _window.Open("Rex Sandbox", 1280, 720);
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

        _world!.Tick(1.0f / _clock.TickRate);

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
            var entities = _mode == NetMode.Standalone
                ? InterpolateStandalone(ctx.InterpolationAlpha)
                : _client!.WorldState.GetInterpolatedState(ctx.InterpolationAlpha);
            _renderer.RenderWorld(entities, ctx.InterpolationAlpha);
            _renderer.EndFrame();
        }

        _window?.SwapBuffers();
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

    [LoggerMessage(EventId = LogEventIds.ClientApp.OnUpdateFailed, Level = LogLevel.Error,
        Message = "OnUpdate threw an exception.")]
    private partial void LogOnUpdateFailed(Exception ex);

    [LoggerMessage(EventId = LogEventIds.ClientApp.OnLateUpdateFailed, Level = LogLevel.Error,
        Message = "OnLateUpdate threw an exception.")]
    private partial void LogOnLateUpdateFailed(Exception ex);

    [LoggerMessage(EventId = LogEventIds.ClientApp.MainLoopCancellationRequested, Level = LogLevel.Debug,
        Message = "Main loop exiting due to cancellation.")]
    private partial void LogMainLoopCancellationRequested();
}
