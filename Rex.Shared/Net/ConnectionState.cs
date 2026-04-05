namespace Rex.Shared.Net;

/// <summary>Generic lifecycle for a channel after transport connect through session activation defined by the consumer.</summary>
public enum ConnectionState
{
    /// <summary>Handshake is underway.</summary>
    Connecting,

    /// <summary>Transport link is up.</summary>
    Connected,

    /// <summary>Handshake succeeded but the game has not marked the player active yet.</summary>
    Authenticated,

    /// <summary>Session is active for gameplay messages.</summary>
    InGame,

    /// <summary>Graceful shutdown is underway.</summary>
    Disconnecting,

    /// <summary>No active peer.</summary>
    Disconnected
}
