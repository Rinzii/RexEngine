using LiteNetLib;
using LiteNetLib.Utils;
using Rex.Shared.Net;
using ConnectionState = Rex.Shared.Net.ConnectionState;

namespace Rex.Client.Net;

/// <summary>LiteNetLib-backed client transport for remote servers.</summary>
public sealed class RemoteClientNetChannel : IClientNetChannel
{
    private readonly EventBasedNetListener _listener;
    private readonly NetManager _netManager;
    private readonly NetDataWriter _writer = new();
    private readonly string _host;
    private readonly int _port;
    private readonly string _connectionKey;
    private NetPeer? _serverPeer;

    public ConnectionState State { get; set; }
    public int RoundTripTimeMs => _serverPeer?.Ping ?? 0;

    public event Action<INetMessage>? MessageReceived;
    public event Action? Connected;
    public event Action<string>? Disconnected;

    public RemoteClientNetChannel(string host, int port, string connectionKey)
    {
        _host = host;
        _port = port;
        _connectionKey = connectionKey;
        _listener = new EventBasedNetListener();
        _netManager = new NetManager(_listener);

        _listener.PeerConnectedEvent += OnPeerConnected;
        _listener.PeerDisconnectedEvent += OnPeerDisconnected;
        _listener.NetworkReceiveEvent += OnNetworkReceive;

        State = ConnectionState.Disconnected;
    }

    public void Connect()
    {
        State = ConnectionState.Connecting;
        if (!_netManager.Start())
        {
            State = ConnectionState.Disconnected;
            Disconnected?.Invoke("Client transport failed to start.");
            return;
        }

        _netManager.Connect(_host, _port, _connectionKey);
    }

    public void Send(INetMessage message, byte channel, DeliveryMethod delivery)
    {
        if (_serverPeer == null)
            return;

        _writer.Reset();
        message.Serialize(_writer);
        _serverPeer.Send(_writer, channel, delivery);
    }

    public void Send(INetMessage message)
    {
        var (channel, delivery) = message.Group.GetDeliveryInfo();
        Send(message, channel, delivery);
    }

    public void Disconnect(string reason)
    {
        State = ConnectionState.Disconnecting;
        if (_serverPeer != null)
        {
            _writer.Reset();
            _writer.Put(reason);
            _serverPeer.Disconnect(_writer);
        }
        _netManager.Stop();
        State = ConnectionState.Disconnected;
    }

    public void PollEvents()
    {
        _netManager.PollEvents();
    }

    private void OnPeerConnected(NetPeer peer)
    {
        _serverPeer = peer;
        State = ConnectionState.Connected;
        Connected?.Invoke();
    }

    private void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        _serverPeer = null;
        State = ConnectionState.Disconnected;
        Disconnected?.Invoke(disconnectInfo.Reason.ToString());
    }

    private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
    {
        var message = NetMessageRegistry.Deserialize(reader);
        reader.Recycle(); // pool backing buffer
        MessageReceived?.Invoke(message);
    }
}
