namespace Rex.Shared.Net;

/// <summary>
/// Wire defaults from the engine that are safe for reusable runtime infrastructure.
/// Connection keys and listen markers for each consumer belong outside the engine layer.
/// </summary>
public static class ProtocolConstants
{
    /// <summary>Bump when packet layouts or handshake rules change.</summary>
    public const ushort ProtocolVersion = 1;

    /// <summary>Generic fallback LiteNetLib connection key for reusable runtime consumers.</summary>
    public const string ConnectionKey = "RexRuntime";

    /// <summary>Milliseconds to wait before giving up on handshake traffic.</summary>
    public const int HandshakeTimeoutMs = 5000;

    /// <summary>LiteNetLib update cadence in milliseconds.</summary>
    public const int TransportUpdateTimeMs = 15;

    /// <summary>Interval between LiteNetLib ping and keepalive packets in milliseconds.</summary>
    public const int TransportPingIntervalMs = 1000;

    /// <summary>Idle disconnect timeout used by LiteNetLib peers in milliseconds.</summary>
    public const int TransportDisconnectTimeoutMs = 10000;

    /// <summary>Delay between client connection attempts in milliseconds.</summary>
    public const int TransportReconnectDelayMs = 500;

    /// <summary>Maximum client connect attempts before LiteNetLib reports failure.</summary>
    public const int TransportMaxConnectAttempts = 10;

    /// <summary>UDP port used when no override is supplied.</summary>
    public const int DefaultPort = 27015;

    /// <summary>Simulation rate when no override is supplied.</summary>
    public const int DefaultTickRate = 60;

    /// <summary>Player cap when no override is supplied.</summary>
    public const int DefaultMaxPlayers = 16;

    /// <summary>
    /// Generic ready sentinel for host integrations that ship with the engine. Concrete consumers can provide their own sentinel instead.
    /// </summary>
    public const string ListenProcessReadyLine = "RexRuntime listen-ready v1";
}
