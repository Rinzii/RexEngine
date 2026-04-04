namespace Rex.Shared.Net;

/// <summary>Generic lifecycle for a channel after transport connect through consumer-defined session activation.</summary>
public enum ConnectionState
{
    Connecting,
    Connected,

    /// <summary>Handshake or admission checks succeeded, but the consumer has not yet marked the session fully active.</summary>
    Authenticated,

    /// <summary>Session is fully active for the consumer's runtime traffic.</summary>
    InGame,
    Disconnecting,
    Disconnected
}
