using Rex.Shared.Net;

namespace Rex.Sandbox.Server.Tests;

// Dedicated server command line parsing.
public sealed class CommandLineArgsTests
{
    private static readonly string[] s_port40000 = ["--port", "40000"];

    [Fact]
    // No args pick protocol defaults for port tick rate and max players.
    public void TryParse_empty_uses_defaults()
    {
        bool ok = CommandLineArgs.TryParse(Array.Empty<string>(), out CommandLineArgs? parsed, out string? error);

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
        bool ok = CommandLineArgs.TryParse(s_port40000, out CommandLineArgs? parsed, out _);

        Assert.True(ok);
        Assert.Equal(40000, parsed!.Port);
    }
}
