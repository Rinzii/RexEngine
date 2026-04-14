using LiteNetLib;
using Microsoft.Extensions.Logging;
using Rex.Sandbox.Shared.Net;
using Rex.Sandbox.Shared.Net.Messages;
using Rex.Sandbox.Shared.Net.Transfer;
using Rex.Sandbox.Shared.Simulation;
using Rex.Shared.Logging;
using Rex.Shared.Net;
using Rex.Shared.Net.Messages;
using Rex.Shared.Net.Transfer;
using Rex.Shared.Simulation;
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

/// <summary>Tracks each Sandbox client's state on the server host.</summary>
public sealed class ClientSession
{
    private readonly Queue<PlayerInputMessage> _inputBuffer = new();

    public ClientSession(IServerNetChannel channel)
    {
        Channel = channel;
    }

    public IServerNetChannel Channel { get; }
    public Guid ClientId => Channel.ClientId;
    public int PlayerEntityId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public uint LastProcessedInputTick { get; set; }
    public uint LastAcknowledgedTick { get; set; }

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
/// Sandbox sample host that does not depend on a specific transport. This sample policy sits outside the reusable engine layer.
/// </summary>
public sealed partial class GameServerHost
{
    private readonly DirtyTracker _dirtyTracker = new();
    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Dictionary<Guid, ClientSession> _sessions = [];

    public GameServerHost(GameServerConfig config, ILoggerFactory loggerFactory)
    {
        Config = config;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<GameServerHost>();
        World = new GameWorld(_dirtyTracker);
    }

    public GameServerConfig Config { get; }
    public uint CurrentTick { get; private set; }
    public GameWorld World { get; }
    public bool IsRunning { get; private set; }
    public bool IsFull => _sessions.Count >= Config.MaxPlayers;
    public RexNetStatistics Statistics { get; } = new();
    public BulkTransferManager? TransferManager { get; private set; }
    public IReadOnlyDictionary<Guid, ClientSession> Sessions => _sessions;

    public void Start()
    {
        if (IsRunning)
        {
            LogHostAlreadyRunning();
            throw new InvalidOperationException("Server host is already running.");
        }

        SandboxNetMessages.RegisterAll();
        TransferManager = new BulkTransferManager(_loggerFactory);
        IsRunning = true;

        LogServerHostStarted(Config.TickRate, Config.MaxPlayers);
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
        if (!_sessions.Remove(clientId, out ClientSession? session))
        {
            return;
        }

        if (session.PlayerEntityId != 0)
        {
            World.DestroyEntity(session.PlayerEntityId);

            var destroyMsg = new EntityDestroyMessage(CurrentTick, session.PlayerEntityId);
            foreach (ClientSession other in _sessions.Values)
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
        if (!_sessions.TryGetValue(clientId, out ClientSession? session))
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
            case RequestFullStateMessage fullStateRequest:
                HandleFullStateRequest(session, fullStateRequest);
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

    public void SendBulkData<T>(Guid clientId, SandboxBulkDataType dataType, T data)
    {
        if (TransferManager == null || !_sessions.TryGetValue(clientId, out ClientSession? session))
        {
            return;
        }

        TransferManager.SendBulkData(session.Channel, (byte)dataType, data);
    }

    public void BroadcastBulkData<T>(SandboxBulkDataType dataType, T data)
    {
        if (TransferManager == null)
        {
            return;
        }

        foreach (ClientSession session in _sessions.Values)
        {
            if (session.Channel.State == ConnectionState.InGame)
            {
                TransferManager.SendBulkData(session.Channel, (byte)dataType, data);
            }
        }
    }

    public void Tick()
    {
        if (!IsRunning)
        {
            return;
        }

        foreach (ClientSession session in _sessions.Values)
        {
            while (session.TryDequeueInput(out PlayerInputMessage? input))
            {
                if (input != null && session.PlayerEntityId != 0)
                {
                    World.ProcessInput(session.PlayerEntityId, input);
                    session.LastProcessedInputTick = input.Tick;
                }
            }
        }

        World.Tick(1.0f / Config.TickRate);
        CurrentTick++;
        BroadcastSnapshots();
    }

    public void Shutdown()
    {
        if (!IsRunning)
        {
            return;
        }

        LogServerHostShuttingDown();

        foreach (ClientSession session in _sessions.Values)
        {
            session.Channel.Disconnect("Server shutting down");
        }

        _sessions.Clear();
        IsRunning = false;
        LogServerHostStopped();
    }

    private void BroadcastSnapshots()
    {
        foreach (ClientSession session in _sessions.Values)
        {
            if (session.Channel.State != ConnectionState.InGame)
            {
                continue;
            }

            HashSet<int>? dirtyEntities = _dirtyTracker.GetDirtyEntities(session.LastAcknowledgedTick, CurrentTick);
            HashSet<int>? removedEntities = _dirtyTracker.GetRemovedEntities(session.LastAcknowledgedTick, CurrentTick);
            // Either ring lookup returning null means the ack window exceeded buffer history, so send a full snapshot.
            WorldSnapshotMessage snapshot = dirtyEntities == null
                                           || removedEntities == null
                ? World.BuildSnapshot(CurrentTick, session.LastProcessedInputTick)
                : World.BuildDeltaSnapshot(CurrentTick, session.LastProcessedInputTick, dirtyEntities, removedEntities);
            (byte channel, DeliveryMethod delivery) = AdaptiveReliability.GetAdaptiveDelivery(snapshot);
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

        int entityId = World.SpawnEntity(session.ClientId, EntityTypeIds.Player, 0f, 0f, 0f);
        session.PlayerEntityId = entityId;

        var response = new ConnectResponseMessage(true, session.ClientId, Config.TickRate, entityId);
        session.Channel.Send(response);

        session.Channel.State = ConnectionState.InGame;
        WorldSnapshotMessage snapshot = World.BuildSnapshot(CurrentTick, 0);
        session.Channel.Send(snapshot, DeliveryChannel.Reliable, DeliveryChannel.ReliableMethod);

        var spawnMsg = new EntitySpawnMessage(CurrentTick, entityId, session.ClientId, EntityTypeIds.Player, 0f, 0f, 0f, 0f);
        foreach (ClientSession other in _sessions.Values)
        {
            if (other.ClientId != session.ClientId && other.Channel.State == ConnectionState.InGame)
            {
                other.Channel.Send(spawnMsg);
            }
        }
    }

    private void HandleFullStateRequest(ClientSession session, RequestFullStateMessage request)
    {
        if (session.Channel.State != ConnectionState.InGame)
        {
            return;
        }

        // Ignore LastAppliedServerTick for now. Always ship one fresh full snapshot at the current authority tick.
        LogClientRequestedFullState(session.ClientId, request.LastAppliedServerTick);
        WorldSnapshotMessage snapshot = World.BuildSnapshot(CurrentTick, session.LastProcessedInputTick);
        session.Channel.Send(snapshot, DeliveryChannel.Reliable, DeliveryChannel.ReliableMethod);
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

    [LoggerMessage(EventId = LogEventIds.GameServerHost.ClientRequestedFullState, Level = LogLevel.Information,
        Message = "Client {ClientId} requested a full state resync from tick {LastAppliedServerTick}")]
    private partial void LogClientRequestedFullState(Guid clientId, uint lastAppliedServerTick);

    [LoggerMessage(EventId = LogEventIds.GameServerHost.UnhandledNetMessage, Level = LogLevel.Debug,
        Message = "Unhandled message from ClientId {ClientId}: Id {MessageId} ({MessageType})")]
    private partial void LogUnhandledNetMessage(Guid clientId, ushort messageId, string messageType);

    [LoggerMessage(EventId = LogEventIds.GameServerHost.HostAlreadyRunning, Level = LogLevel.Error,
        Message = "Start called while the Sandbox server host is already running.")]
    private partial void LogHostAlreadyRunning();
}
