using LiteNetLib;

namespace Rex.Shared.Net;

/// <summary>
/// Client-side transport consumed by the client runtime.
/// </summary>
public interface IClientNetChannel
{
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
    /// Opens the transport.
    /// </summary>
    void Connect();

    /// <summary>
    /// Closes the connection.
    /// </summary>
    void Disconnect(string reason);

    /// <summary>
    /// Pumps transport events for this frame.
    /// </summary>
    void PollEvents();

    /// <summary>
    /// Raised when a message arrives from the server.
    /// </summary>
    event Action<INetMessage> MessageReceived;

    /// <summary>
    /// Raised after the transport is connected.
    /// </summary>
    event Action Connected;

    /// <summary>
    /// Raised when the connection closes.
    /// </summary>
    event Action<string> Disconnected;
}
