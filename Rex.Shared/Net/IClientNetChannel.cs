using LiteNetLib;

namespace Rex.Shared.Net;

/// <summary>Outbound client-side connection abstraction. Hides LiteNetLib versus in-memory transports.</summary>
public interface IClientNetChannel
{
    ConnectionState State { get; set; }

    /// <summary>Ping in ms when connected, or 0 if unknown.</summary>
    int RoundTripTimeMs { get; }

    void Send(INetMessage message, byte channel, DeliveryMethod delivery);

    /// <summary>Sends using <see cref="INetMessage.Group"/> defaults.</summary>
    void Send(INetMessage message);

    void Connect();
    void Disconnect(string reason);

    /// <summary>Must be called regularly so receives and connection events fire.</summary>
    void PollEvents();

    event Action<INetMessage> MessageReceived;
    event Action Connected;
    event Action<string> Disconnected;
}
