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
/// Top-level client application. Owns the game loop and provides seams
/// for windowing and rendering. In standalone mode, runs the simulation
/// directly with no networking. In networked modes, delegates to GameClient.
/// </summary>
public sealed class ClientApp : IDisposable
{
    private readonly NetMode _mode;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly GameLoop _gameLoop;

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
    public GameLoop GameLoop => _gameLoop;
    public bool Headless { get; init; }

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

    /// <param name="tickRate">Sim ticks per second for the embedded <see cref="GameLoop"/>.</param>
    public ClientApp(NetMode mode, ILoggerFactory loggerFactory, int tickRate = ProtocolConstants.DefaultTickRate)
    {
        _mode = mode;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<ClientApp>();
        _gameLoop = new GameLoop(tickRate);
    }

    /// <summary>Sets the input source sampled each tick.</summary>
    public void SetInputCollector(InputCollector collector)
    {
        _inputCollector = collector;
    }

    /// <summary>Initializes for the configured net mode, runs the game loop, and blocks until exit.</summary>
    public void Run(string? host = null, int port = ProtocolConstants.DefaultPort)
    {
        _gameLoop.YieldBetweenFrames = Headless;

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
        _gameLoop.Run();

        Shutdown();
    }

    public void Stop()
    {
        _gameLoop.Stop();
    }

    public void Dispose()
    {
        _renderer?.Dispose();
        _window?.Dispose();
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

        _gameLoop.OnTick = TickStandalone;
        _gameLoop.OnRender = Render;

        _logger.LogInformation("Standalone world initialized.");
    }

    private void SetupNetworked(string host, int port)
    {
        _client = new GameClient(_loggerFactory);

        if (_inputCollector != null)
            _client.SetInputCollector(_inputCollector);

        _client.Connect(host, port);

        _gameLoop.OnTick = TickNetworked;
        _gameLoop.OnRender = Render;
    }

    private void TickStandalone()
    {
        // Apply input directly to the world. No messages or serialization.
        if (_inputCollector != null)
        {
            var input = _inputCollector.Sample(_gameLoop.Clock.CurrentTick);
            _world!.ProcessInput(_localEntityId, input);
        }

        var deltaTime = 1.0f / _gameLoop.Clock.TickRate;
        _world!.Tick(deltaTime);

        // Store previous/current entity snapshots for render interpolation.
        _previousEntities = _currentEntities;
        _currentEntities = new List<EntityState>(_world.Entities.Values);
    }

    private void TickNetworked()
    {
        _client!.Tick(_gameLoop.Clock.CurrentTick);
    }

    private void Render(float alpha)
    {
        if (Headless)
            return;

        _window?.PollEvents();

        if (_window != null && !_window.IsOpen)
        {
            _gameLoop.Stop();
            return;
        }

        if (_renderer != null)
        {
            _renderer.BeginFrame();

            var entities = _mode == NetMode.Standalone
                ? InterpolateStandalone(alpha)
                : _client!.WorldState.GetInterpolatedState(alpha);

            _renderer.RenderWorld(entities, alpha);
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
                // New this frame. Nothing to blend with.
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
