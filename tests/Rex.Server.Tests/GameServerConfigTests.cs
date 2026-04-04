using Rex.Shared.Net;
using Rex.Sandbox.Shared.Net;
using Rex.Sandbox.Server.Simulation;

namespace Rex.Sandbox.Server.Tests;

// Server runtime config defaults.
public sealed class GameServerConfigTests
{
    [Fact]
    // New config matches ProtocolConstants and default server name.
    public void Defaults_match_protocol_constants()
    {
        var cfg = new GameServerConfig();

        Assert.Equal(ProtocolConstants.DefaultTickRate, cfg.TickRate);
        Assert.Equal(ProtocolConstants.DefaultMaxPlayers, cfg.MaxPlayers);
        Assert.Equal(ProtocolConstants.DefaultPort, cfg.Port);
        Assert.Equal(SandboxProtocolConstants.ConnectionKey, cfg.ConnectionKey);
        Assert.Equal("Rex Sandbox Server", cfg.ServerName);
    }
}
