using LiteNetLib;

namespace Rex.Shared.Net;

/// <summary>
/// In-process client transport paired with <see cref="LocalServerNetChannel"/>.
/// Used by listen server mode so the host client can skip socket transport.
/// </summary>
public sealed class LocalClientNetChannel : IClientNetChannel
{
    private readonly LocalServerNetChannel _serverChannel;

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

    /// <summary>
    /// Creates the client side of a local channel pair.
    /// </summary>
    public LocalClientNetChannel(LocalServerNetChannel serverChannel)
    {
        _serverChannel = serverChannel;
        State = ConnectionState.Connected;
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
        _serverChannel.EnqueueFromClient(message);
    }

    /// <inheritdoc />
    public void Send(INetMessage message)
    {
        _serverChannel.EnqueueFromClient(message);
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
        while (_serverChannel.TryDequeueToClient(out var message))
        {
            if (message != null)
                MessageReceived?.Invoke(message);
        }
    }
}
