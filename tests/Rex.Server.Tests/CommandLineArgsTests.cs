using Rex.Shared.Net;

namespace Rex.Server.Tests;

public sealed class CommandLineArgsTests
{
    [Fact]
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
    public void TryParse_port_override()
    {
        var ok = CommandLineArgs.TryParse(new[] { "--port", "40000" }, out var parsed, out _);

        Assert.True(ok);
        Assert.Equal(40000, parsed!.Port);
    }
}
