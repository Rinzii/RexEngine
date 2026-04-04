namespace Rex.Shared.Net;

/// <summary>
/// Engine-owned wire defaults that are safe for reusable runtime infrastructure.
/// Consumer-specific connection keys or listen-process sentinels should live outside the engine layer.
/// </summary>
public static class ProtocolConstants
{
    /// <summary>Bump when packet layouts or handshake rules change.</summary>
    public const ushort ProtocolVersion = 1;

    /// <summary>Generic fallback LiteNetLib connection key for reusable runtime consumers.</summary>
    public const string ConnectionKey = "RexRuntime";

    public const int HandshakeTimeoutMs = 5000;
    public const int DefaultPort = 27015;
    public const int DefaultTickRate = 60;
    public const int DefaultMaxPlayers = 16;

    /// <summary>
    /// Generic ready sentinel for engine-owned host integrations. Concrete consumers can provide their own sentinel instead.
    /// </summary>
    public const string ListenProcessReadyLine = "RexRuntime listen-ready v1";
}
