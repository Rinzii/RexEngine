namespace Rex.Shared.Net;

/// <summary>Rough lifecycle for a channel after connect through gameplay.</summary>
public enum ConnectionState
{
    Connecting,
    Connected,

    /// <summary>Handshake done, not yet spawned in world.</summary>
    Authenticated,
    InGame,
    Disconnecting,
    Disconnected
}
