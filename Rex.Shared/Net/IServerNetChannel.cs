using LiteNetLib;

namespace Rex.Shared.Net;

public interface IServerNetChannel
{
    int ClientId { get; }
    bool IsLocal { get; }
    ConnectionState State { get; set; }
    int RoundTripTimeMs { get; }

    void Send(INetMessage message, byte channel, DeliveryMethod delivery);

    /// <summary>
    /// Auto-selects channel and delivery from the message's group.
    /// </summary>
    void Send(INetMessage message);

    void Disconnect(string reason);
}
