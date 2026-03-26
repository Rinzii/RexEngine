namespace Rex.Shared.Net;

/// <summary>Wire and session defaults shared by client and server.</summary>
public static class ProtocolConstants
{
    /// <summary>Bump when packet layouts or handshake rules change.</summary>
    public const ushort ProtocolVersion = 1;

    /// <summary>LiteNetLib connection key. Client and server must match.</summary>
    public const string ConnectionKey = "RexEngine";

    public const int HandshakeTimeoutMs = 5000;
    public const int DefaultPort = 27015;
    public const int DefaultTickRate = 60;
    public const int DefaultMaxPlayers = 16;
}