using Microsoft.Extensions.Logging;
using Rex.Shared;
using Rex.Shared.Net;
using Rex.Shared.Net.Messages;
using Rex.Shared.Net.Transfer;
using Rex.Shared.Simulation;

namespace Rex.Server.Simulation;

/// <summary>
/// Transport-agnostic server host. Manages sessions, processes inputs,
/// ticks the shared world, and broadcasts snapshots to connected clients.
/// </summary>
public sealed partial class GameServerHost
{
    private readonly GameServerConfig _config;
    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Dictionary<int, ClientSession> _sessions = new();
    private readonly GameWorld _world;
    private readonly DirtyTracker _dirtyTracker = new();
    private readonly RexNetStatistics _statistics = new();

    private BulkTransferManager? _transferManager;
    private int _nextClientId = 1;
    private uint _currentTick;
    private bool _isRunning;

    public GameServerConfig Config => _config;
    public uint CurrentTick => _currentTick;
    public GameWorld World => _world;
    public bool IsRunning => _isRunning;
    public bool IsFull => _sessions.Count >= _config.MaxPlayers;
    public RexNetStatistics Statistics => _statistics;
    public BulkTransferManager? TransferManager => _transferManager;
    public IReadOnlyDictionary<int, ClientSession> Sessions => _sessions;

    public GameServerHost(GameServerConfig config, ILoggerFactory loggerFactory)
    {
        _config = config;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<GameServerHost>();
        _world = new GameWorld(_dirtyTracker);
    }

    /// <summary>Initializes the host. Call before the first tick.</summary>
    public void Start()
    {
        if (_isRunning)
        {
            LogHostAlreadyRunning();
            throw new InvalidOperationException("Server host is already running.");
        }

        NetMessages.RegisterAll();
        _transferManager = new BulkTransferManager(_loggerFactory);
        _isRunning = true;

        LogServerHostStarted(_config.TickRate, _config.MaxPlayers);
    }

    /// <summary>Allocates a new client ID for an incoming connection.</summary>
    public int AllocateClientId()
    {
        return _nextClientId++;
    }

    /// <summary>Registers a session created by the transport layer.</summary>
    public void AddSession(ClientSession session)
    {
        _sessions[session.ClientId] = session;

        LogSessionAdded(session.ClientId);
    }

    /// <summary>Removes a session and destroys its entities.</summary>
    public void RemoveSession(int clientId)
    {
        if (!_sessions.Remove(clientId))
        {
            return;
        }

        _world.DestroyEntity(clientId);

        var destroyMsg = new EntityDestroyMessage(clientId);
        foreach (var other in _sessions.Values)
        {
            if (other.ClientId != clientId && other.Channel.State == ConnectionState.InGame)
            {
                other.Channel.Send(destroyMsg);
            }
        }

        LogSessionRemoved(clientId);
    }

    /// <summary>Routes an inbound message to the appropriate handler.</summary>
    public void HandleMessage(int clientId, INetMessage message)
    {
        if (!_sessions.TryGetValue(clientId, out var session))
        {
            return;
        }

        switch (message)
        {
            case ConnectRequestMessage connectRequest:
                HandleConnectRequest(session, connectRequest);
                break;
            case PlayerInputMessage playerInput:
                session.EnqueueInput(playerInput);
                break;
            case StateAckMessage stateAck:
                session.LastAcknowledgedTick = stateAck.AcknowledgedTick;
                break;
            case BulkTransferAckMessage transferAck:
                LogBulkTransferAcked(session.ClientId, transferAck.TransferId, transferAck.Success);
                break;
            case DisconnectMessage disconnect:
                LogClientDisconnecting(session.ClientId, disconnect.Reason);

                session.Channel.Disconnect(disconnect.Reason);
                break;
            default:
                LogUnhandledNetMessage(clientId, message.MessageId, message.GetType().Name);
                break;
        }
    }

    public void SendBulkData<T>(int clientId, BulkDataType dataType, T data)
    {
        if (_transferManager == null || !_sessions.TryGetValue(clientId, out var session))
        {
            return;
        }

        _transferManager.SendBulkData(session.Channel, dataType, data);
    }

    public void BroadcastBulkData<T>(BulkDataType dataType, T data)
    {
        if (_transferManager == null)
        {
            return;
        }

        foreach (var session in _sessions.Values)
        {
            if (session.Channel.State == ConnectionState.InGame)
            {
                _transferManager.SendBulkData(session.Channel, dataType, data);
            }
        }
    }

    /// <summary>Processes inputs, advances the world, and broadcasts snapshots.</summary>
    public void Tick()
    {
        if (!_isRunning)
        {
            return;
        }

        _dirtyTracker.ClearTick(_currentTick);

        foreach (var session in _sessions.Values)
        {
            while (session.TryDequeueInput(out var input))
            {
                if (input != null)
                {
                    _world.ProcessInput(session.ClientId, input);
                    session.LastProcessedInputTick = input.Tick;
                }
            }
        }

        var deltaTime = 1.0f / _config.TickRate;
        _world.Tick(deltaTime);
        _currentTick++;
        BroadcastSnapshots();
    }

    public void Shutdown()
    {
        if (!_isRunning)
        {
            return;
        }

        LogServerHostShuttingDown();

        foreach (var session in _sessions.Values)
        {
            session.Channel.Disconnect("Server shutting down");
        }

        _sessions.Clear();
        _isRunning = false;

        LogServerHostStopped();
    }

    private void BroadcastSnapshots()
    {
        foreach (var session in _sessions.Values)
        {
            if (session.Channel.State != ConnectionState.InGame)
            {
                continue;
            }

            var dirtyEntities = _dirtyTracker.GetDirtyEntities(session.LastAcknowledgedTick, _currentTick);
            WorldSnapshotMessage snapshot;

            // Null means ack too old for the ring buffer. Send full state once.
            if (dirtyEntities == null)
            {
                snapshot = _world.BuildSnapshot(_currentTick, session.LastProcessedInputTick);
            }
            else
            {
                snapshot = _world.BuildDeltaSnapshot(_currentTick, session.LastProcessedInputTick, dirtyEntities);
            }

            var (channel, delivery) = AdaptiveReliability.GetAdaptiveDelivery(snapshot);
            session.Channel.Send(snapshot, channel, delivery);
        }
    }

    private void HandleConnectRequest(ClientSession session, ConnectRequestMessage request)
    {
        if (request.ProtocolVersion != ProtocolConstants.ProtocolVersion)
        {
            var reject = new ConnectResponseMessage(false, 0, 0, "Protocol version mismatch");
            session.Channel.Send(reject);
            session.Channel.Disconnect("Protocol version mismatch");
            return;
        }

        session.PlayerName = request.PlayerName;
        session.Channel.State = ConnectionState.Authenticated;
        LogClientAuthenticated(session.ClientId, request.PlayerName);

        var response = new ConnectResponseMessage(true, session.ClientId, _config.TickRate);
        session.Channel.Send(response);

        _world.SpawnEntity(session.ClientId, EntityTypeIds.Player, 0f, 0f, 0f);

        session.Channel.State = ConnectionState.InGame;
        var snapshot = _world.BuildSnapshot(_currentTick, 0);
        session.Channel.Send(snapshot, DeliveryChannel.Reliable, DeliveryChannel.ReliableMethod);

        var spawnMsg = new EntitySpawnMessage(session.ClientId, session.ClientId, EntityTypeIds.Player, 0f, 0f, 0f);
        foreach (var other in _sessions.Values)
        {
            if (other.ClientId != session.ClientId && other.Channel.State == ConnectionState.InGame)
            {
                other.Channel.Send(spawnMsg);
            }
        }
    }
}
