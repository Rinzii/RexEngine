using System.Net;
using System.Net.Sockets;
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
    private readonly ILogger _logger;
    private readonly Dictionary<NetPeer, Guid> _peerToClientId = [];

    private EventBasedNetListener? _listener;
    private NetManager? _netManager;

    // ReSharper disable once ConvertToPrimaryConstructor
    public GameServer(GameServerConfig config, ILoggerFactory loggerFactory)
    {
        Host = new GameServerHost(config, loggerFactory);
        _logger = loggerFactory.CreateLogger<GameServer>();
    }

    public GameServerHost Host { get; }

    public void Start()
    {
        _listener = new EventBasedNetListener();
        _netManager = new NetManager(_listener);
        LiteNetLibTransportConfiguration.ApplyServerDefaults(_netManager);

        _listener.ConnectionRequestEvent += OnConnectionRequest;
        _listener.PeerConnectedEvent += OnPeerConnected;
        _listener.PeerDisconnectedEvent += OnPeerDisconnected;
        _listener.NetworkReceiveEvent += OnNetworkReceive;
        _listener.NetworkErrorEvent += OnNetworkError;

        if (!_netManager.Start(Host.Config.Port))
        {
            LogCannotListenOnPort(Host.Config.Port);
            _netManager.Stop();
            _netManager = null;
            _listener = null;
            throw new PortAlreadyInUseException(Host.Config.Port);
        }

        Host.Start();
        LogServerListening(Host.Config.Port);
        Console.Out.WriteLine(SandboxProtocolConstants.ListenProcessReadyLine);
    }

    public void Tick()
    {
        _netManager?.PollEvents();
        Host.Tick();
    }

    public void Shutdown()
    {
        Host.Shutdown();
        _peerToClientId.Clear();
        _netManager?.Stop();
        LogServerNetworkStopped();
    }

    private void OnConnectionRequest(ConnectionRequest request)
    {
        if (Host.IsFull)
        {
            request.Reject();
            LogConnectionRejectedServerFull();
            return;
        }

        _ = request.AcceptIfKey(Host.Config.ConnectionKey);
    }

    private void OnPeerConnected(NetPeer peer)
    {
        Guid clientId = GameServerHost.AllocateClientId();
        var channel = new RemoteServerNetChannel(peer, clientId);
        var session = new ClientSession(channel);
        Host.AddSession(session);
        _peerToClientId[peer] = clientId;

        LogPeerConnected(peer.Address, clientId);
    }

    private void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        if (!_peerToClientId.TryGetValue(peer, out Guid clientId))
        {
            return;
        }

        LogPeerDisconnected(clientId, disconnectInfo.Reason);
        Host.RemoveSession(clientId);
        _ = _peerToClientId.Remove(peer);
    }

    private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
    {
        if (!_peerToClientId.TryGetValue(peer, out Guid clientId))
        {
            return;
        }

        Host.Statistics.RecordReceived(0, reader.AvailableBytes);
        try
        {
            INetMessage message = NetMessageRegistry.Deserialize(reader);
            Host.HandleMessage(clientId, message);
        }
        catch (Exception ex)
        {
            LogDeserializeMessageFailed(clientId, ex);
        }
    }

    private void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
    {
        LogNetworkError(endPoint, socketError);
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
    private partial void LogPeerConnected(IPAddress address, Guid clientId);

    [LoggerMessage(EventId = LogEventIds.GameServerNet.PeerDisconnected, Level = LogLevel.Information,
        Message = "Peer disconnected: ClientId {ClientId} ({Reason})")]
    private partial void LogPeerDisconnected(Guid clientId, DisconnectReason reason);

    [LoggerMessage(EventId = LogEventIds.GameServerNet.DeserializeMessageFailed, Level = LogLevel.Warning,
        Message = "Failed to deserialize inbound message for ClientId {ClientId}.")]
    private partial void LogDeserializeMessageFailed(Guid clientId, Exception ex);

    [LoggerMessage(EventId = LogEventIds.GameServerNet.NetworkError, Level = LogLevel.Warning,
        Message = "LiteNetLib server transport error from {EndPoint}: {SocketError}.")]
    private partial void LogNetworkError(IPEndPoint endPoint, SocketError socketError);
}
