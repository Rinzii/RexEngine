using LiteNetLib;
using LiteNetLib.Utils;

namespace Rex.Shared.Net;

/// <summary>
/// LiteNetLib-backed server transport for one remote client.
/// </summary>
public sealed class RemoteServerNetChannel : IServerNetChannel
{
    private readonly NetPeer _peer;
    private readonly NetDataWriter _writer = new();

    /// <inheritdoc />
    public int ClientId { get; }

    /// <inheritdoc />
    public bool IsLocal => false;

    /// <inheritdoc />
    public ConnectionState State { get; set; }

    /// <inheritdoc />
    public int RoundTripTimeMs => _peer.Ping;

    /// <summary>
    /// Wraps a connected LiteNetLib peer in the server channel interface.
    /// </summary>
    public RemoteServerNetChannel(NetPeer peer, int clientId)
    {
        _peer = peer;
        ClientId = clientId;
        State = ConnectionState.Connected;
    }

    /// <inheritdoc />
    public void Send(INetMessage message, byte channel, DeliveryMethod delivery)
    {
        _writer.Reset();
        message.Serialize(_writer);
        _peer.Send(_writer, channel, delivery);
    }

    /// <inheritdoc />
    public void Send(INetMessage message)
    {
        var (channel, delivery) = message.Group.GetDeliveryInfo();
        Send(message, channel, delivery);
    }

    /// <inheritdoc />
    public void Disconnect(string reason)
    {
        State = ConnectionState.Disconnecting;
        _writer.Reset();
        _writer.Put(reason);
        _peer.Disconnect(_writer);
        State = ConnectionState.Disconnected;
    }
}
