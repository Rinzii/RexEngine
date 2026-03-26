using LiteNetLib;

namespace Rex.Shared.Net;

/// <summary>
/// Server-side transport bound to one client session.
/// </summary>
public interface IServerNetChannel
{
    /// <summary>
    /// Gets the server-assigned client ID for this connection.
    /// </summary>
    int ClientId { get; }

    /// <summary>
    /// Gets a value that indicates whether this channel stays in-process.
    /// </summary>
    bool IsLocal { get; }

    /// <summary>
    /// Gets or sets the current connection state.
    /// </summary>
    ConnectionState State { get; set; }

    /// <summary>
    /// Gets the current round trip time in milliseconds.
    /// Local transports always return zero.
    /// </summary>
    int RoundTripTimeMs { get; }

    /// <summary>
    /// Sends a message with an explicit channel and delivery mode.
    /// </summary>
    void Send(INetMessage message, byte channel, DeliveryMethod delivery);

    /// <summary>
    /// Sends a message with the default settings for its group.
    /// </summary>
    void Send(INetMessage message);

    /// <summary>
    /// Closes the connection.
    /// </summary>
    void Disconnect(string reason);
}
