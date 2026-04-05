using LiteNetLib;

namespace Rex.Shared.Net;

/// <summary>Builds two channels that exchange messages through paired queues (no UDP).</summary>
public static class LocalNetChannelPair
{
    /// <summary>Client and server share queues. Each side reads messages the peer enqueued.</summary>
    /// <param name="clientId">Stable id for the server channel.</param>
    /// <returns>Paired client and server endpoints.</returns>
    public static (LocalClientNetChannel Client, LocalServerNetChannel Server) Create(Guid clientId)
    {
        var serverToClient = new Queue<INetMessage>();
        var clientToServer = new Queue<INetMessage>();

        var server = new LocalServerNetChannel(clientId, clientToServer, serverToClient);
        var client = new LocalClientNetChannel(clientToServer, serverToClient);

        return (client, server);
    }
}

/// <summary>In memory transport on the client for standalone mode.</summary>
public sealed class LocalClientNetChannel : IClientNetChannel
{
    private readonly Queue<INetMessage> _outbound;
    private readonly Queue<INetMessage> _inbound;

    /// <inheritdoc />
    public ConnectionState State { get; set; }

    /// <inheritdoc />
    public int RoundTripTimeMs => 0;

    /// <inheritdoc />
    public event Action<INetMessage>? MessageReceived;

    /// <inheritdoc />
    public event Action? Connected;

    /// <inheritdoc />
    public event Action<string>? Disconnected;

    internal LocalClientNetChannel(Queue<INetMessage> outbound, Queue<INetMessage> inbound)
    {
        _outbound = outbound;
        _inbound = inbound;
        State = ConnectionState.Disconnected;
    }

    /// <inheritdoc />
    public void Connect()
    {
        State = ConnectionState.Connected;
        Connected?.Invoke();
    }

    /// <inheritdoc />
    public void Send(INetMessage message, byte channel, DeliveryMethod delivery)
    {
        _outbound.Enqueue(message);
    }

    /// <inheritdoc />
    public void Send(INetMessage message)
    {
        _outbound.Enqueue(message);
    }

    /// <inheritdoc />
    public void Disconnect(string reason)
    {
        State = ConnectionState.Disconnected;
        Disconnected?.Invoke(reason);
    }

    /// <inheritdoc />
    public void PollEvents()
    {
        while (_inbound.Count > 0)
        {
            MessageReceived?.Invoke(_inbound.Dequeue());
        }
    }
}

/// <summary>In memory transport on the server for standalone mode.</summary>
public sealed class LocalServerNetChannel : IServerNetChannel
{
    private readonly Queue<INetMessage> _inbound;
    private readonly Queue<INetMessage> _outbound;

    /// <inheritdoc />
    public Guid ClientId { get; }

    /// <inheritdoc />
    public bool IsLocal => true;

    /// <inheritdoc />
    public ConnectionState State { get; set; }

    /// <inheritdoc />
    public int RoundTripTimeMs => 0;

    internal LocalServerNetChannel(Guid clientId, Queue<INetMessage> inbound, Queue<INetMessage> outbound)
    {
        ClientId = clientId;
        _inbound = inbound;
        _outbound = outbound;
        State = ConnectionState.Connected;
    }

    /// <inheritdoc />
    public void Send(INetMessage message, byte channel, DeliveryMethod delivery)
    {
        _outbound.Enqueue(message);
    }

    /// <inheritdoc />
    public void Send(INetMessage message)
    {
        _outbound.Enqueue(message);
    }

    /// <inheritdoc />
    public void Disconnect(string reason)
    {
        State = ConnectionState.Disconnected;
    }

    /// <summary>Drains inbound client messages. Call before ticking the server host.</summary>
    /// <param name="handler">Invoked once per queued message.</param>
    public void DrainMessages(Action<INetMessage> handler)
    {
        while (_inbound.Count > 0)
        {
            handler(_inbound.Dequeue());
        }
    }
}
