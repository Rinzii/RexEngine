using LiteNetLib;
using Microsoft.Extensions.Logging;
using Rex.Server.Net;
using Rex.Server.Simulation;
using Rex.Shared.Net;
using Rex.Shared.Net.Messages;

namespace Rex.Server.Core;

/// <summary>LiteNetLib facade. Accepts peers and delegates simulation to <see cref="GameServerHost"/>.</summary>
public sealed partial class GameServer
{
    private readonly GameServerHost _host;
    private readonly ILogger _logger;
    private readonly Dictionary<NetPeer, int> _peerToClientId = new();

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

        // Bind before GameServerHost.Start so we do not run the sim if the port is taken.
        if (!_netManager.Start(_host.Config.Port))
        {
            LogCannotListenOnPort(_host.Config.Port);
            _netManager.Stop();
            _netManager = null;
            _listener = null;
            throw new InvalidOperationException($"Port {_host.Config.Port} is already in use.");
        }

        _host.Start();

        LogServerListening(_host.Config.Port);
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
        // Reject before accept if we are at max players.
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
        // One ClientSession and id for the lifetime of this peer.
        var clientId = _host.AllocateClientId();
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

        // Tear down sim and notify other clients via host.
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

        // Byte count only here. Message id can be counted after deserialize if you extend stats.
        _host.Statistics.RecordReceived(0, reader.AvailableBytes);
        var message = NetMessageRegistry.Deserialize(reader);
        reader.Recycle();
        _host.HandleMessage(clientId, message);
    }
}
