using LiteNetLib;
using LiteNetLib.Utils;
using Microsoft.Extensions.Logging;
using Rex.Shared.Net;
using Rex.Shared.Net.Messages;
using Rex.Shared.Net.Transfer;
using ConnectionState = Rex.Shared.Net.ConnectionState;

namespace Rex.Shared.Server;

/// <summary>
/// Server-side networking controller that accepts clients, buffers inputs, and broadcasts snapshots.
/// </summary>
public sealed class GameServer
{
    private readonly GameServerConfig _config;
    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Dictionary<int, ClientSession> _sessions = new();
    private readonly Dictionary<NetPeer, int> _peerToClientId = new();
    private readonly GameWorld _world;
    private readonly DirtyTracker _dirtyTracker = new();
    private readonly RexNetStatistics _statistics = new();

    private EventBasedNetListener? _listener;
    private NetManager? _netManager;
    private BulkTransferManager? _transferManager;
    private int _nextClientId = 1;
    private uint _currentTick;
    private bool _isRunning;

    /// <summary>
    /// Gets the active server config.
    /// </summary>
    public GameServerConfig Config => _config;

    /// <summary>
    /// Gets the current server tick.
    /// </summary>
    public uint CurrentTick => _currentTick;

    /// <summary>
    /// Gets the game world owned by this server.
    /// </summary>
    public GameWorld World => _world;

    /// <summary>
    /// Gets a value that indicates whether the server is running.
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Gets the network statistics sampler.
    /// </summary>
    public RexNetStatistics Statistics => _statistics;

    /// <summary>
    /// Gets the bulk transfer manager while the server is running.
    /// </summary>
    public BulkTransferManager? TransferManager => _transferManager;

    /// <summary>
    /// Creates the server networking controller.
    /// </summary>
    public GameServer(GameServerConfig config, ILoggerFactory loggerFactory)
    {
        _config = config;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<GameServer>();
        _world = new GameWorld(_dirtyTracker);
    }

    /// <summary>
    /// Starts the remote networking transport and begins accepting clients.
    /// </summary>
    public void Start()
    {
        if (_isRunning)
            throw new InvalidOperationException("Server is already running.");

        NetMessages.RegisterAll();

        _listener = new EventBasedNetListener();
        _netManager = new NetManager(_listener);
        _transferManager = new BulkTransferManager(_loggerFactory);

        _listener.ConnectionRequestEvent += OnConnectionRequest;
        _listener.PeerConnectedEvent += OnPeerConnected;
        _listener.PeerDisconnectedEvent += OnPeerDisconnected;
        _listener.NetworkReceiveEvent += OnNetworkReceive;

        _netManager.Start(_config.Port);
        _isRunning = true;
        _logger.LogInformation("Server started on port {Port} (tick rate: {TickRate}, max players: {MaxPlayers})",
            _config.Port, _config.TickRate, _config.MaxPlayers);
    }

    /// <summary>
    /// Adds a local client for listen server mode and returns its client transport.
    /// </summary>
    public IClientNetChannel AddLocalClient()
    {
        var clientId = _nextClientId++;
        var serverChannel = new LocalServerNetChannel(clientId);
        var clientChannel = new LocalClientNetChannel(serverChannel);

        var session = new ClientSession(serverChannel);
        _sessions[clientId] = session;

        _logger.LogInformation("Local client added with ID {ClientId}", clientId);
        return clientChannel;
    }

    /// <summary>
    /// Sends one bulk payload to a specific client if that client is active.
    /// </summary>
    public void SendBulkData<T>(int clientId, BulkDataType dataType, T data)
    {
        if (_transferManager == null || !_sessions.TryGetValue(clientId, out var session))
            return;

        _transferManager.SendBulkData(session.Channel, dataType, data);
    }

    /// <summary>
    /// Sends one bulk payload to every in-game client.
    /// </summary>
    public void BroadcastBulkData<T>(BulkDataType dataType, T data)
    {
        if (_transferManager == null)
            return;

        foreach (var session in _sessions.Values)
        {
            if (session.Channel.State == ConnectionState.InGame)
            {
                _transferManager.SendBulkData(session.Channel, dataType, data);
            }
        }
    }

    /// <summary>
    /// Pumps transport events, runs one simulation tick, and broadcasts snapshots.
    /// </summary>
    public void Tick()
    {
        if (!_isRunning)
            return;

        _dirtyTracker.ClearTick(_currentTick);
        _netManager?.PollEvents();
        foreach (var session in _sessions.Values)
        {
            if (session.Channel is LocalServerNetChannel localChannel)
            {
                while (localChannel.TryDequeueFromClient(out var message))
                {
                    if (message != null)
                    {
                        HandleMessage(session, message);
                    }
                }
            }
        }

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

    /// <summary>
    /// Disconnects clients and stops the remote transport.
    /// </summary>
    public void Shutdown()
    {
        if (!_isRunning)
            return;

        _logger.LogInformation("Server shutting down...");

        foreach (var session in _sessions.Values)
        {
            session.Channel.Disconnect("Server shutting down");
        }

        _sessions.Clear();
        _peerToClientId.Clear();
        _netManager?.Stop();
        _isRunning = false;

        _logger.LogInformation("Server stopped.");
    }

    /// <summary>
    /// Builds a snapshot for each client based on its latest ack and sends it with adaptive delivery.
    /// </summary>
    private void BroadcastSnapshots()
    {
        foreach (var session in _sessions.Values)
        {
            if (session.Channel.State != ConnectionState.InGame)
                continue;

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

    private void OnConnectionRequest(ConnectionRequest request)
    {
        if (_sessions.Count >= _config.MaxPlayers)
        {
            request.Reject();
            _logger.LogWarning("Connection rejected: server full.");
            return;
        }

        request.AcceptIfKey(_config.ConnectionKey);
    }

    private void OnPeerConnected(NetPeer peer)
    {
        var clientId = _nextClientId++;
        var channel = new RemoteServerNetChannel(peer, clientId);
        var session = new ClientSession(channel);
        _sessions[clientId] = session;
        _peerToClientId[peer] = clientId;

        _logger.LogInformation("Remote peer connected: {EndPoint} assigned ClientId {ClientId}",
            peer.Address, clientId);
    }

    private void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        if (!_peerToClientId.TryGetValue(peer, out var clientId))
            return;

        _logger.LogInformation("Remote peer disconnected: ClientId {ClientId} ({Reason})",
            clientId, disconnectInfo.Reason);

        _world.DestroyEntity(clientId);

        var destroyMsg = new EntityDestroyMessage(clientId);
        foreach (var other in _sessions.Values)
        {
            if (other.ClientId != clientId && other.Channel.State == ConnectionState.InGame)
            {
                other.Channel.Send(destroyMsg);
            }
        }

        _sessions.Remove(clientId);
        _peerToClientId.Remove(peer);
    }

    private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
    {
        if (!_peerToClientId.TryGetValue(peer, out var clientId))
        {
            reader.Recycle();
            return;
        }

        if (!_sessions.TryGetValue(clientId, out var session))
        {
            reader.Recycle();
            return;
        }

        _statistics.RecordReceived(0, reader.AvailableBytes);
        var message = NetMessageRegistry.Deserialize(reader);
        reader.Recycle();
        HandleMessage(session, message);
    }

    private void HandleMessage(ClientSession session, INetMessage message)
    {
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
                _logger.LogDebug("Client {ClientId} acknowledged transfer {TransferId}: {Success}",
                    session.ClientId, transferAck.TransferId, transferAck.Success);
                break;
            case DisconnectMessage disconnect:
                _logger.LogInformation("Client {ClientId} disconnecting: {Reason}",
                    session.ClientId, disconnect.Reason);
                session.Channel.Disconnect(disconnect.Reason);
                break;
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
        _logger.LogInformation("Client {ClientId} authenticated as '{PlayerName}'",
            session.ClientId, request.PlayerName);

        var response = new ConnectResponseMessage(true, session.ClientId, _config.TickRate);
        session.Channel.Send(response);

        _world.SpawnEntity(session.ClientId, "player", 0f, 0f, 0f);

        session.Channel.State = ConnectionState.InGame;
        var snapshot = _world.BuildSnapshot(_currentTick, 0);
        session.Channel.Send(snapshot, DeliveryChannel.Reliable, DeliveryChannel.ReliableMethod);

        var spawnMsg = new EntitySpawnMessage(session.ClientId, session.ClientId, "player", 0f, 0f, 0f);
        foreach (var other in _sessions.Values)
        {
            if (other.ClientId != session.ClientId && other.Channel.State == ConnectionState.InGame)
            {
                other.Channel.Send(spawnMsg);
            }
        }
    }
}
