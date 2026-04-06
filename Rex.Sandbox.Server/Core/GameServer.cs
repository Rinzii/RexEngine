using LiteNetLib;
using Microsoft.Extensions.Logging;
using Rex.Sandbox.Server.Simulation;
using Rex.Sandbox.Shared.Net;
using Rex.Server.Net;
using Rex.Shared.Logging;
using Rex.Shared.Net;

namespace Rex.Sandbox.Server.Core;

/// <summary>LiteNetLib facade for the Sandbox server.</summary>
public sealed partial class GameServer
{
    private readonly GameServerHost _host;
    private readonly ILogger _logger;
    private readonly Dictionary<NetPeer, Guid> _peerToClientId = new();

    private EventBasedNetListener? _listener;
    private NetManager? _netManager;

    public GameServerHost Host => _host;

    public GameServer(GameServerConfig config, ILoggerFactory loggerFactory)
    {
        _host = new GameServerHost(config, loggerFactory);
        _logger = loggerFactory.CreateLogger<GameServer>();
    }

    public void Start()
    {
        _listener = new EventBasedNetListener();
        _netManager = new NetManager(_listener);

        _listener.ConnectionRequestEvent += OnConnectionRequest;
        _listener.PeerConnectedEvent += OnPeerConnected;
        _listener.PeerDisconnectedEvent += OnPeerDisconnected;
        _listener.NetworkReceiveEvent += OnNetworkReceive;

        if (!_netManager.Start(_host.Config.Port))
        {
            LogCannotListenOnPort(_host.Config.Port);
            _netManager.Stop();
            _netManager = null;
            _listener = null;
            throw new PortAlreadyInUseException(_host.Config.Port);
        }

        _host.Start();
        LogServerListening(_host.Config.Port);
        Console.Out.WriteLine(SandboxProtocolConstants.ListenProcessReadyLine);
    }

    public void Tick()
    {
        _netManager?.PollEvents();
        _host.Tick();
    }

    public void Shutdown()
    {
        _host.Shutdown();
        _peerToClientId.Clear();
        _netManager?.Stop();
        LogServerNetworkStopped();
    }

    private void OnConnectionRequest(ConnectionRequest request)
    {
        if (_host.IsFull)
        {
            request.Reject();
            LogConnectionRejectedServerFull();
            return;
        }

        request.AcceptIfKey(_host.Config.ConnectionKey);
    }

    private void OnPeerConnected(NetPeer peer)
    {
        var clientId = GameServerHost.AllocateClientId();
        var channel = new RemoteServerNetChannel(peer, clientId);
        var session = new ClientSession(channel);
        _host.AddSession(session);
        _peerToClientId[peer] = clientId;

        LogPeerConnected(peer.Address, clientId);
    }

    private void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        if (!_peerToClientId.TryGetValue(peer, out var clientId))
        {
            return;
        }

        LogPeerDisconnected(clientId, disconnectInfo.Reason);
        _host.RemoveSession(clientId);
        _peerToClientId.Remove(peer);
    }

    private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
    {
        if (!_peerToClientId.TryGetValue(peer, out var clientId))
        {
            reader.Recycle();
            return;
        }

        _host.Statistics.RecordReceived(0, reader.AvailableBytes);
        try
        {
            var message = NetMessageRegistry.Deserialize(reader);
            reader.Recycle();
            _host.HandleMessage(clientId, message);
        }
        catch (Exception ex)
        {
            LogDeserializeMessageFailed(clientId, ex);
            reader.Recycle();
        }
    }
}

/// <summary>
/// Raised when the Sandbox server cannot bind its requested UDP port during startup.
/// </summary>
public sealed class PortAlreadyInUseException : InvalidOperationException
{
    public PortAlreadyInUseException(int port)
        : base($"Port {port} is already in use.")
    {
        Port = port;
    }
    public int Port { get; }
}

public sealed partial class GameServer
{
    [LoggerMessage(EventId = LogEventIds.GameServerNet.CannotListenOnPort, Level = LogLevel.Error,
        Message =
            "Cannot listen on port {Port}. It is probably already in use. Stop the other process or use --port with a different value.")]
    private partial void LogCannotListenOnPort(int port);

    [LoggerMessage(EventId = LogEventIds.GameServerNet.ServerListening, Level = LogLevel.Information,
        Message = "Sandbox server listening on port {Port}")]
    private partial void LogServerListening(int port);

    [LoggerMessage(EventId = LogEventIds.GameServerNet.ServerNetworkStopped, Level = LogLevel.Information,
        Message = "Sandbox server network layer stopped.")]
    private partial void LogServerNetworkStopped();

    [LoggerMessage(EventId = LogEventIds.GameServerNet.ConnectionRejectedServerFull, Level = LogLevel.Warning,
        Message = "Connection rejected: server full.")]
    private partial void LogConnectionRejectedServerFull();

    [LoggerMessage(EventId = LogEventIds.GameServerNet.PeerConnected, Level = LogLevel.Information,
        Message = "Peer connected: {Address} -> ClientId {ClientId}")]
    private partial void LogPeerConnected(System.Net.IPAddress address, Guid clientId);

    [LoggerMessage(EventId = LogEventIds.GameServerNet.PeerDisconnected, Level = LogLevel.Information,
        Message = "Peer disconnected: ClientId {ClientId} ({Reason})")]
    private partial void LogPeerDisconnected(Guid clientId, DisconnectReason reason);

    [LoggerMessage(EventId = LogEventIds.GameServerNet.DeserializeMessageFailed, Level = LogLevel.Warning,
        Message = "Failed to deserialize inbound message for ClientId {ClientId}.")]
    private partial void LogDeserializeMessageFailed(Guid clientId, Exception ex);
}
