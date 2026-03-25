using System.Collections.Concurrent;
using LiteNetLib;

namespace Rex.Shared.Net;

/// <summary>
/// In-process server transport for the listen server host player.
/// Messages stay as object references and move through local queues.
/// </summary>
public sealed class LocalServerNetChannel : IServerNetChannel
{
    private readonly ConcurrentQueue<INetMessage> _outboundToClient = new();
    private readonly ConcurrentQueue<INetMessage> _inboundFromClient = new();

    /// <inheritdoc />
    public int ClientId { get; }

    /// <inheritdoc />
    public bool IsLocal => true;

    /// <inheritdoc />
    public ConnectionState State { get; set; }

    /// <inheritdoc />
    public int RoundTripTimeMs => 0;

    /// <summary>
    /// Creates the server side of a local channel pair.
    /// </summary>
    public LocalServerNetChannel(int clientId)
    {
        ClientId = clientId;
        State = ConnectionState.Connected;
    }

    /// <inheritdoc />
    public void Send(INetMessage message, byte channel, DeliveryMethod delivery)
    {
        _outboundToClient.Enqueue(message);
    }

    /// <inheritdoc />
    public void Send(INetMessage message)
    {
        _outboundToClient.Enqueue(message);
    }

    /// <inheritdoc />
    public void Disconnect(string reason)
    {
        State = ConnectionState.Disconnected;
    }

    /// <summary>
    /// Queues a message from the local client to the server.
    /// </summary>
    public void EnqueueFromClient(INetMessage message)
    {
        _inboundFromClient.Enqueue(message);
    }

    /// <summary>
    /// Tries to dequeue one pending message from the local client.
    /// </summary>
    public bool TryDequeueFromClient(out INetMessage? message)
    {
        return _inboundFromClient.TryDequeue(out message);
    }

    /// <summary>
    /// Tries to dequeue one pending message for the local client.
    /// </summary>
    public bool TryDequeueToClient(out INetMessage? message)
    {
        return _outboundToClient.TryDequeue(out message);
    }
}
