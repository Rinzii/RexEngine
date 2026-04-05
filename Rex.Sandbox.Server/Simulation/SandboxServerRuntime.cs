using LiteNetLib;
using Microsoft.Extensions.Logging;
using Rex.Sandbox.Shared.Net;
using Rex.Sandbox.Shared.Net.Messages;
using Rex.Sandbox.Shared.Net.Transfer;
using Rex.Sandbox.Shared.Simulation;
using Rex.Server.Net;
using Rex.Shared.Logging;
using Rex.Shared.Net;
using Rex.Shared.Net.Transfer;
using ConnectionState = Rex.Shared.Net.ConnectionState;

namespace Rex.Sandbox.Server.Simulation;

/// <summary>Configuration for the Sandbox server host.</summary>
public sealed class GameServerConfig
{
    public int TickRate { get; init; } = ProtocolConstants.DefaultTickRate;
    public int MaxPlayers { get; init; } = ProtocolConstants.DefaultMaxPlayers;
    public int Port { get; init; } = ProtocolConstants.DefaultPort;
    public string ServerName { get; init; } = "Rex Sandbox Server";
    public string ConnectionKey { get; init; } = SandboxProtocolConstants.ConnectionKey;
}

/// <summary>Per-client state tracked by the Sandbox server host.</summary>
public sealed class ClientSession
{
    public IServerNetChannel Channel { get; }
    public Guid ClientId => Channel.ClientId;
    public int PlayerEntityId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public uint LastProcessedInputTick { get; set; }
    public uint LastAcknowledgedTick { get; set; }

    private readonly Queue<PlayerInputMessage> _inputBuffer = new();

    public ClientSession(IServerNetChannel channel)
    {
        Channel = channel;
    }

    public void EnqueueInput(PlayerInputMessage input)
    {
        _inputBuffer.Enqueue(input);
    }

    public bool TryDequeueInput(out PlayerInputMessage? input)
    {
        return _inputBuffer.TryDequeue(out input);
    }
}

/// <summary>
/// Transport-agnostic Sandbox host. This sample policy sits outside the reusable engine layer.
/// </summary>
public sealed partial class GameServerHost
{
    private readonly GameServerConfig _config;
    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Dictionary<Guid, ClientSession> _sessions = new();
    private readonly GameWorld _world;
    private readonly Rex.Shared.Simulation.DirtyTracker _dirtyTracker = new();
    private readonly RexNetStatistics _statistics = new();

    private BulkTransferManager? _transferManager;
    private uint _currentTick;
    private bool _isRunning;

    public GameServerConfig Config => _config;
    public uint CurrentTick => _currentTick;
    public GameWorld World => _world;
    public bool IsRunning => _isRunning;
    public bool IsFull => _sessions.Count >= _config.MaxPlayers;
    public RexNetStatistics Statistics => _statistics;
    public BulkTransferManager? TransferManager => _transferManager;
    public IReadOnlyDictionary<Guid, ClientSession> Sessions => _sessions;

    public GameServerHost(GameServerConfig config, ILoggerFactory loggerFactory)
    {
        _config = config;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<GameServerHost>();
        _world = new GameWorld(_dirtyTracker);
    }

    public void Start()
    {
        if (_isRunning)
        {
            LogHostAlreadyRunning();
            throw new InvalidOperationException("Server host is already running.");
        }

        SandboxNetMessages.RegisterAll();
        _transferManager = new BulkTransferManager(_loggerFactory);
        _isRunning = true;

        LogServerHostStarted(_config.TickRate, _config.MaxPlayers);
    }

    public static Guid AllocateClientId()
    {
        return Guid.CreateVersion7();
    }

    public void AddSession(ClientSession session)
    {
        _sessions[session.ClientId] = session;
        LogSessionAdded(session.ClientId);
    }

    public void RemoveSession(Guid clientId)
    {
        if (!_sessions.Remove(clientId, out var session))
        {
            return;
        }

        if (session.PlayerEntityId != 0)
        {
            _world.DestroyEntity(session.PlayerEntityId);

            var destroyMsg = new EntityDestroyMessage(session.PlayerEntityId);
            foreach (var other in _sessions.Values)
            {
                if (other.ClientId != clientId && other.Channel.State == ConnectionState.InGame)
                {
                    other.Channel.Send(destroyMsg);
                }
            }
        }

        LogSessionRemoved(clientId);
    }

    public void HandleMessage(Guid clientId, INetMessage message)
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
            case Rex.Shared.Net.Messages.StateAckMessage stateAck:
                session.LastAcknowledgedTick = stateAck.AcknowledgedTick;
                break;
            case BulkTransferAckMessage transferAck:
                LogBulkTransferAcked(session.ClientId, transferAck.TransferId, transferAck.Success);
                break;
            case Rex.Shared.Net.Messages.DisconnectMessage disconnect:
                LogClientDisconnecting(session.ClientId, disconnect.Reason);
                session.Channel.Disconnect(disconnect.Reason);
                break;
            default:
                LogUnhandledNetMessage(clientId, message.MessageId, message.GetType().Name);
                break;
        }
    }

    public void SendBulkData<T>(Guid clientId, SandboxBulkDataType dataType, T data)
    {
        if (_transferManager == null || !_sessions.TryGetValue(clientId, out var session))
        {
            return;
        }

        _transferManager.SendBulkData(session.Channel, (byte)dataType, data);
    }

    public void BroadcastBulkData<T>(SandboxBulkDataType dataType, T data)
    {
        if (_transferManager == null)
        {
            return;
        }

        foreach (var session in _sessions.Values)
        {
            if (session.Channel.State == ConnectionState.InGame)
            {
                _transferManager.SendBulkData(session.Channel, (byte)dataType, data);
            }
        }
    }

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
                if (input != null && session.PlayerEntityId != 0)
                {
                    _world.ProcessInput(session.PlayerEntityId, input);
                    session.LastProcessedInputTick = input.Tick;
                }
            }
        }

        _world.Tick(1.0f / _config.TickRate);
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
            var reject = new ConnectResponseMessage(false, Guid.Empty, 0, 0, "Protocol version mismatch");
            session.Channel.Send(reject);
            session.Channel.Disconnect("Protocol version mismatch");
            return;
        }

        session.PlayerName = request.PlayerName;
        session.Channel.State = ConnectionState.Authenticated;
        LogClientAuthenticated(session.ClientId, request.PlayerName);

        var entityId = _world.SpawnEntity(session.ClientId, EntityTypeIds.Player, 0f, 0f, 0f);
        session.PlayerEntityId = entityId;

        var response = new ConnectResponseMessage(true, session.ClientId, _config.TickRate, entityId);
        session.Channel.Send(response);

        session.Channel.State = ConnectionState.InGame;
        var snapshot = _world.BuildSnapshot(_currentTick, 0);
        session.Channel.Send(snapshot, DeliveryChannel.Reliable, DeliveryChannel.ReliableMethod);

        var spawnMsg = new EntitySpawnMessage(entityId, session.ClientId, EntityTypeIds.Player, 0f, 0f, 0f);
        foreach (var other in _sessions.Values)
        {
            if (other.ClientId != session.ClientId && other.Channel.State == ConnectionState.InGame)
            {
                other.Channel.Send(spawnMsg);
            }
        }
    }
}

public sealed partial class GameServerHost
{
    [LoggerMessage(EventId = LogEventIds.GameServerHost.HostStarted, Level = LogLevel.Information,
        Message = "Sandbox server host started (tick rate: {TickRate}, max players: {MaxPlayers})")]
    private partial void LogServerHostStarted(int tickRate, int maxPlayers);

    [LoggerMessage(EventId = LogEventIds.GameServerHost.SessionAdded, Level = LogLevel.Information,
        Message = "Session added: ClientId {ClientId}")]
    private partial void LogSessionAdded(Guid clientId);

    [LoggerMessage(EventId = LogEventIds.GameServerHost.SessionRemoved, Level = LogLevel.Information,
        Message = "Session removed: ClientId {ClientId}")]
    private partial void LogSessionRemoved(Guid clientId);

    [LoggerMessage(EventId = LogEventIds.GameServerHost.BulkTransferAcked, Level = LogLevel.Debug,
        Message = "Client {ClientId} acked transfer {TransferId}: {Success}")]
    private partial void LogBulkTransferAcked(Guid clientId, Guid transferId, bool success);

    [LoggerMessage(EventId = LogEventIds.GameServerHost.ClientDisconnecting, Level = LogLevel.Information,
        Message = "Client {ClientId} disconnecting: {Reason}")]
    private partial void LogClientDisconnecting(Guid clientId, string reason);

    [LoggerMessage(EventId = LogEventIds.GameServerHost.HostShuttingDown, Level = LogLevel.Information,
        Message = "Sandbox server host shutting down...")]
    private partial void LogServerHostShuttingDown();

    [LoggerMessage(EventId = LogEventIds.GameServerHost.HostStopped, Level = LogLevel.Information,
        Message = "Sandbox server host stopped.")]
    private partial void LogServerHostStopped();

    [LoggerMessage(EventId = LogEventIds.GameServerHost.ClientAuthenticated, Level = LogLevel.Information,
        Message = "Client {ClientId} authenticated as '{PlayerName}'")]
    private partial void LogClientAuthenticated(Guid clientId, string playerName);

    [LoggerMessage(EventId = LogEventIds.GameServerHost.UnhandledNetMessage, Level = LogLevel.Debug,
        Message = "Unhandled message from ClientId {ClientId}: Id {MessageId} ({MessageType})")]
    private partial void LogUnhandledNetMessage(Guid clientId, ushort messageId, string messageType);

    [LoggerMessage(EventId = LogEventIds.GameServerHost.HostAlreadyRunning, Level = LogLevel.Error,
        Message = "Start called while the Sandbox server host is already running.")]
    private partial void LogHostAlreadyRunning();
}
