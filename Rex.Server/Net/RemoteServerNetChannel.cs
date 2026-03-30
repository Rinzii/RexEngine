using LiteNetLib;
using LiteNetLib.Utils;
using Rex.Shared.Net;
using ConnectionState = Rex.Shared.Net.ConnectionState;

namespace Rex.Server.Net;

/// <summary>LiteNetLib-backed server transport for one remote client.</summary>
public sealed class RemoteServerNetChannel : IServerNetChannel
{
    private readonly NetPeer _peer;
    private readonly NetDataWriter _writer = new();

    public Guid ClientId { get; }
    public bool IsLocal => false;
    public ConnectionState State { get; set; }
    public int RoundTripTimeMs => _peer.Ping;

    /// <param name="peer">LiteNetLib peer for this client after accept.</param>
    /// <param name="clientId">Same id the host uses in sessions.</param>
    public RemoteServerNetChannel(NetPeer peer, Guid clientId)
    {
        _peer = peer;
        ClientId = clientId;
        State = ConnectionState.Connected;
    }

    public void Send(INetMessage message, byte channel, DeliveryMethod delivery)
    {
        _writer.Reset();
        message.Serialize(_writer);
        _peer.Send(_writer, channel, delivery);
    }

    public void Send(INetMessage message)
    {
        var (channel, delivery) = message.Group.GetDeliveryInfo();
        Send(message, channel, delivery);
    }

    public void Disconnect(string reason)
    {
        State = ConnectionState.Disconnecting;
        _writer.Reset();
        _writer.Put(reason);
        _peer.Disconnect(_writer);
        State = ConnectionState.Disconnected;
    }
}
