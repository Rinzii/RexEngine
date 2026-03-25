namespace Rex.Shared.Net;

/// <summary>
/// High level connection state shared by the networking layer.
/// </summary>
public enum ConnectionState
{
    /// <summary>
    /// The connection is being established.
    /// </summary>
    Connecting,

    /// <summary>
    /// The transport is connected but gameplay is not ready yet.
    /// </summary>
    Connected,

    /// <summary>
    /// The client passed protocol checks.
    /// </summary>
    Authenticated,

    /// <summary>
    /// Gameplay traffic is flowing.
    /// </summary>
    InGame,

    /// <summary>
    /// Disconnect is in progress.
    /// </summary>
    Disconnecting,

    /// <summary>
    /// The connection is fully closed.
    /// </summary>
    Disconnected
}
