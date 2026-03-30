using Rex.Shared.Net;
using Rex.Server.Simulation;

namespace Rex.Server.Tests;

// Locks init defaults and overrides on GameServerConfig.
public sealed class GameServerConfigRegressionTests
{
    [Fact]
    public void Regression_partial_init_keeps_other_protocol_defaults()
    {
        var cfg = new GameServerConfig { Port = 19999 };

        Assert.Equal(19999, cfg.Port);
        Assert.Equal(ProtocolConstants.DefaultTickRate, cfg.TickRate);
        Assert.Equal(ProtocolConstants.DefaultMaxPlayers, cfg.MaxPlayers);
        Assert.Equal(ProtocolConstants.ConnectionKey, cfg.ConnectionKey);
        Assert.Equal("RexEngine Server", cfg.ServerName);
    }
}
