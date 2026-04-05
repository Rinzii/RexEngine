using LiteNetLib;
using LiteNetLib.Utils;
using Microsoft.Extensions.Logging;
using Rex.Shared.Net;
using ConnectionState = Rex.Shared.Net.ConnectionState;

namespace Rex.Client.Net;

/// <summary>
/// LiteNetLib-backed client transport for remote servers.
/// </summary>
/// <remarks>
/// Subscribes to <see cref="EventBasedNetListener"/> callbacks on construction.
/// Call <see cref="PollEvents"/> regularly on the same thread that owns consumer-side runtime state so receives and
/// connection events are delivered coherently.
/// </remarks>
public sealed partial class RemoteClientNetChannel : IClientNetChannel
{
    private readonly EventBasedNetListener _listener;
    private readonly NetManager _netManager;

    // Shared buffer for outbound packets. Reset before each send.
    private readonly NetDataWriter _writer = new();

    private readonly string _host;
    private readonly int _port;
    private readonly string _connectionKey;
    private readonly ILogger _logger;

    // Non-null only while the server peer is connected.
    private NetPeer? _serverPeer;

    public ConnectionState State { get; set; }
    public int RoundTripTimeMs => _serverPeer?.Ping ?? 0;

    public event Action<INetMessage>? MessageReceived;
    public event Action? Connected;
    public event Action<string>? Disconnected;

    /// <summary>
    /// Creates a channel that will connect to the given host and port using the LiteNetLib connection key.
    /// </summary>
    /// <param name="host">Server host name or IP address.</param>
    /// <param name="port">Server UDP port.</param>
    /// <param name="connectionKey">Caller-supplied connection key. Must match the remote host's expected key.</param>
    /// <param name="logger">Logger for transport failures and deserialize errors.</param>
    public RemoteClientNetChannel(string host, int port, string connectionKey, ILogger logger)
    {
        _host = host;
        _port = port;
        _connectionKey = connectionKey;
        _logger = logger;
        _listener = new EventBasedNetListener();
        _netManager = new NetManager(_listener);

        _listener.PeerConnectedEvent += OnPeerConnected;
        _listener.PeerDisconnectedEvent += OnPeerDisconnected;
        _listener.NetworkReceiveEvent += OnNetworkReceive;

        State = ConnectionState.Disconnected;
    }

    /// <summary>
    /// Starts the client socket and begins the LiteNetLib connect handshake.
    /// </summary>
    /// <remarks>
    /// On failure, sets <see cref="State"/> to disconnected and raises <see cref="Disconnected"/>.
    /// On success, <see cref="Connected"/> fires when the server accepts (after future <see cref="PollEvents"/> calls).
    /// </remarks>
    public void Connect()
    {
        State = ConnectionState.Connecting;
        if (!_netManager.Start())
        {
            State = ConnectionState.Disconnected;
            LogTransportStartFailed();
            Disconnected?.Invoke("Client transport failed to start.");
            return;
        }

        _netManager.Connect(_host, _port, _connectionKey);
    }

    /// <summary>
    /// Serializes <paramref name="message"/> and sends it on the given LiteNetLib channel and delivery mode.
    /// </summary>
    /// <remarks>
    /// Does nothing when there is no connected server peer.
    /// The message id header is written by each <see cref="INetMessage"/> implementation as part of <see cref="INetMessage.Serialize"/>.
    /// </remarks>
    public void Send(INetMessage message, byte channel, DeliveryMethod delivery)
    {
        if (_serverPeer == null)
        {
            return;
        }

        _writer.Reset();
        message.Serialize(_writer);
        _serverPeer.Send(_writer, channel, delivery);
    }

    /// <inheritdoc />
    public void Send(INetMessage message)
    {
        var (channel, delivery) = message.Group.GetDeliveryInfo();
        Send(message, channel, delivery);
    }

    /// <summary>
    /// Disconnects from the server and stops the client <see cref="NetManager"/>.
    /// </summary>
    /// <param name="reason">Reason string sent to the server when a peer is connected.</param>
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

    /// <summary>
    /// Pumps LiteNetLib so connection and receive callbacks can run.
    /// </summary>
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
        try
        {
            var message = NetMessageRegistry.Deserialize(reader);
            reader.Recycle(); // Return pooled buffer to LiteNetLib.
            MessageReceived?.Invoke(message);
        }
        catch (Exception ex)
        {
            LogDeserializeMessageFailed(ex);
            reader.Recycle();
        }
    }
}
