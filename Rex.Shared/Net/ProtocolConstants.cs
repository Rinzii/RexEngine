namespace Rex.Shared.Net;

/// <summary>
/// Shared protocol settings used by the client and server.
/// </summary>
public static class ProtocolConstants
{
    /// <summary>
    /// Wire protocol version expected by both sides.
    /// </summary>
    public const ushort ProtocolVersion = 1;

    /// <summary>
    /// LiteNetLib connection key used during the initial accept step.
    /// </summary>
    public const string ConnectionKey = "RexEngine";

    /// <summary>
    /// Default timeout for handshake logic.
    /// </summary>
    public const int HandshakeTimeoutMs = 5000;

    /// <summary>
    /// Default server port.
    /// </summary>
    public const int DefaultPort = 27015;

    /// <summary>
    /// Default server tick rate.
    /// </summary>
    public const int DefaultTickRate = 60;

    /// <summary>
    /// Default player cap.
    /// </summary>
    public const int DefaultMaxPlayers = 16;
}
