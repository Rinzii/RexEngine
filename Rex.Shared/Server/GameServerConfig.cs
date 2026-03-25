using Rex.Shared.Net;

namespace Rex.Shared.Server;

/// <summary>
/// Networking and session settings for <see cref="GameServer"/>.
/// </summary>
public sealed class GameServerConfig
{
    /// <summary>
    /// Gets the server tick rate.
    /// </summary>
    public int TickRate { get; init; } = ProtocolConstants.DefaultTickRate;

    /// <summary>
    /// Gets the maximum number of connected players.
    /// </summary>
    public int MaxPlayers { get; init; } = ProtocolConstants.DefaultMaxPlayers;

    /// <summary>
    /// Gets the port that accepts remote clients.
    /// </summary>
    public int Port { get; init; } = ProtocolConstants.DefaultPort;

    /// <summary>
    /// Gets the display name advertised by the server.
    /// </summary>
    public string ServerName { get; init; } = "RexEngine Server";

    /// <summary>
    /// Gets the LiteNetLib accept key.
    /// </summary>
    public string ConnectionKey { get; init; } = ProtocolConstants.ConnectionKey;
}
