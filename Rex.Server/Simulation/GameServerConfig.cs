using Rex.Shared.Net;

namespace Rex.Server.Simulation;

/// <summary>Configuration for the game server host.</summary>
public sealed class GameServerConfig
{
    /// <summary>Simulation ticks per second for <see cref="GameServerHost.Tick"/>.</summary>
    public int TickRate { get; init; } = ProtocolConstants.DefaultTickRate;

    /// <summary>Hard cap on concurrent sessions.</summary>
    public int MaxPlayers { get; init; } = ProtocolConstants.DefaultMaxPlayers;

    /// <summary>UDP listen port for LiteNetLib.</summary>
    public int Port { get; init; } = ProtocolConstants.DefaultPort;

    /// <summary>Display name for logs or server browser style UIs.</summary>
    public string ServerName { get; init; } = "RexEngine Server";

    /// <summary>Must match client <see cref="ProtocolConstants.ConnectionKey"/>.</summary>
    public string ConnectionKey { get; init; } = ProtocolConstants.ConnectionKey;
}
