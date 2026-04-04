using Rex.Shared.Net;

namespace Rex.Sandbox.Server.Tests;

// Dedicated server command line parsing.
public sealed class CommandLineArgsTests
{
    [Fact]
    // No args pick protocol defaults for port tick rate and max players.
    public void TryParse_empty_uses_defaults()
    {
        var ok = CommandLineArgs.TryParse(Array.Empty<string>(), out var parsed, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(parsed);
        Assert.Equal(ProtocolConstants.DefaultPort, parsed!.Port);
        Assert.Equal(ProtocolConstants.DefaultMaxPlayers, parsed.MaxPlayers);
        Assert.Equal(ProtocolConstants.DefaultTickRate, parsed.TickRate);
    }

    [Fact]
    // Port flag overrides the default listen port.
    public void TryParse_port_override()
    {
        var ok = CommandLineArgs.TryParse(new[] { "--port", "40000" }, out var parsed, out _);

        Assert.True(ok);
        Assert.Equal(40000, parsed!.Port);
    }
}
