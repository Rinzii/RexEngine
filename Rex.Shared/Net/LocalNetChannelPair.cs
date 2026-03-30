using LiteNetLib;

namespace Rex.Shared.Net;

/// <summary>Builds two channels that exchange messages through paired queues (no UDP).</summary>
public static class LocalNetChannelPair
{
    /// <summary>Client and server share queues: what the client enqueues is read by the server, and the other way around.</summary>
    public static (LocalClientNetChannel Client, LocalServerNetChannel Server) Create(Guid clientId)
    {
        var serverToClient = new Queue<INetMessage>();
        var clientToServer = new Queue<INetMessage>();

        var server = new LocalServerNetChannel(clientId, clientToServer, serverToClient);
        var client = new LocalClientNetChannel(clientToServer, serverToClient);

        return (client, server);
    }
}

/// <summary>Client-side in-memory transport for standalone mode.</summary>
public sealed class LocalClientNetChannel : IClientNetChannel
{
    private readonly Queue<INetMessage> _outbound;
    private readonly Queue<INetMessage> _inbound;

    public ConnectionState State { get; set; }
    public int RoundTripTimeMs => 0;

    public event Action<INetMessage>? MessageReceived;
    public event Action? Connected;
    public event Action<string>? Disconnected;

    internal LocalClientNetChannel(Queue<INetMessage> outbound, Queue<INetMessage> inbound)
    {
        _outbound = outbound;
        _inbound = inbound;
        State = ConnectionState.Disconnected;
    }

    public void Connect()
    {
        State = ConnectionState.Connected;
        Connected?.Invoke();
    }

    public void Send(INetMessage message, byte channel, DeliveryMethod delivery)
    {
        _outbound.Enqueue(message);
    }

    public void Send(INetMessage message)
    {
        _outbound.Enqueue(message);
    }

    public void Disconnect(string reason)
    {
        State = ConnectionState.Disconnected;
        Disconnected?.Invoke(reason);
    }

    public void PollEvents()
    {
        // Drain everything the server queued for this client this frame.
        while (_inbound.Count > 0)
        {
            MessageReceived?.Invoke(_inbound.Dequeue());
        }
    }
}

/// <summary>Server-side in-memory transport for standalone mode.</summary>
public sealed class LocalServerNetChannel : IServerNetChannel
{
    private readonly Queue<INetMessage> _inbound;
    private readonly Queue<INetMessage> _outbound;

    public Guid ClientId { get; }
    public bool IsLocal => true;
    public ConnectionState State { get; set; }
    public int RoundTripTimeMs => 0;

    internal LocalServerNetChannel(Guid clientId, Queue<INetMessage> inbound, Queue<INetMessage> outbound)
    {
        ClientId = clientId;
        _inbound = inbound;
        _outbound = outbound;
        State = ConnectionState.Connected;
    }

    public void Send(INetMessage message, byte channel, DeliveryMethod delivery)
    {
        _outbound.Enqueue(message);
    }

    public void Send(INetMessage message)
    {
        _outbound.Enqueue(message);
    }

    public void Disconnect(string reason)
    {
        State = ConnectionState.Disconnected;
    }

    /// <summary>Drains inbound client messages. Call before ticking the server host.</summary>
    public void DrainMessages(Action<INetMessage> handler)
    {
        // Everything the client enqueued for this tick.
        while (_inbound.Count > 0)
        {
            handler(_inbound.Dequeue());
        }
    }
}
