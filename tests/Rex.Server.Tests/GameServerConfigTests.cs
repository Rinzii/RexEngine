using Rex.Shared.Net;
using Rex.Server.Simulation;

namespace Rex.Server.Tests;

public sealed class GameServerConfigTests
{
    [Fact]
    public void Defaults_match_protocol_constants()
    {
        var cfg = new GameServerConfig();

        Assert.Equal(ProtocolConstants.DefaultTickRate, cfg.TickRate);
        Assert.Equal(ProtocolConstants.DefaultMaxPlayers, cfg.MaxPlayers);
        Assert.Equal(ProtocolConstants.DefaultPort, cfg.Port);
        Assert.Equal(ProtocolConstants.ConnectionKey, cfg.ConnectionKey);
        Assert.Equal("RexEngine Server", cfg.ServerName);
    }
}
