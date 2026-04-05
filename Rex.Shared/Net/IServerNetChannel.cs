using LiteNetLib;

namespace Rex.Shared.Net;

/// <summary>Server outbound link for one connected client.</summary>
public interface IServerNetChannel
{
    /// <summary>Stable id assigned to the remote peer.</summary>
    Guid ClientId { get; }

    /// <summary>True when the channel uses queues inside the same process.</summary>
    bool IsLocal { get; }

    /// <summary>Current transport and session state.</summary>
    ConnectionState State { get; set; }

    /// <summary>Last ping in milliseconds or zero when unknown.</summary>
    int RoundTripTimeMs { get; }

    /// <summary>Sends on an explicit LiteNetLib channel and delivery mode.</summary>
    /// <param name="message">Payload to write.</param>
    /// <param name="channel">LiteNetLib channel id.</param>
    /// <param name="delivery">Delivery method.</param>
    void Send(INetMessage message, byte channel, DeliveryMethod delivery);

    /// <summary>Sends using defaults from <see cref="INetMessage.Group"/>.</summary>
    /// <param name="message">Payload to write.</param>
    void Send(INetMessage message);

    /// <summary>Drops the session and notifies the peer.</summary>
    /// <param name="reason">Opaque string forwarded to the remote client.</param>
    void Disconnect(string reason);
}
