using LiteNetLib;

namespace Rex.Shared.Net;

/// <summary>One client's link from server-side game code. Pair with <see cref="ClientSession"/>.</summary>
public interface IServerNetChannel
{
    int ClientId { get; }

    /// <summary>True for in-process channels (no real latency).</summary>
    bool IsLocal { get; }

    ConnectionState State { get; set; }
    int RoundTripTimeMs { get; }

    void Send(INetMessage message, byte channel, DeliveryMethod delivery);
    void Send(INetMessage message);
    void Disconnect(string reason);
}