using LiteNetLib;

namespace Rex.Shared.Net;

public interface IClientNetChannel
{
    ConnectionState State { get; set; }
    int RoundTripTimeMs { get; }

    void Send(INetMessage message, byte channel, DeliveryMethod delivery);

    /// <summary>
    /// Auto-selects channel and delivery from the message's group.
    /// </summary>
    void Send(INetMessage message);

    void Connect(string host, int port, string connectionKey);
    void Disconnect(string reason);

    /// <summary>
    /// Polls LiteNetLib (remote) or drains the in-process queue (local).
    /// </summary>
    void PollEvents();

    event Action<INetMessage> MessageReceived;
    event Action Connected;
    event Action<string> Disconnected;
}
