using LiteNetLib;

namespace Rex.Shared.Net;

/// <summary>Outbound abstraction for a client connection. Hides LiteNetLib versus in memory transports.</summary>
public interface IClientNetChannel
{
    /// <summary>Current transport and session state for this client.</summary>
    ConnectionState State { get; set; }

    /// <summary>Ping in ms when connected or zero when unknown.</summary>
    int RoundTripTimeMs { get; }

    /// <summary>Sends on an explicit LiteNetLib channel and delivery mode.</summary>
    /// <param name="message">Payload to write.</param>
    /// <param name="channel">LiteNetLib channel id.</param>
    /// <param name="delivery">Delivery method.</param>
    void Send(INetMessage message, byte channel, DeliveryMethod delivery);

    /// <summary>Sends using defaults from <see cref="INetMessage.Group"/>.</summary>
    /// <param name="message">Payload to write.</param>
    void Send(INetMessage message);

    /// <summary>Starts the transport connect handshake.</summary>
    void Connect();

    /// <summary>Drops the session and notifies the peer.</summary>
    /// <param name="reason">Opaque string forwarded to the remote host.</param>
    void Disconnect(string reason);

    /// <summary>Pumps inbound data and connection callbacks.</summary>
    void PollEvents();

    /// <summary>Raised for each inbound message in order.</summary>
    event Action<INetMessage>? MessageReceived;

    /// <summary>Raised when the session becomes active.</summary>
    event Action? Connected;

    /// <summary>Raised when the link closes including the reason text.</summary>
    event Action<string>? Disconnected;
}
