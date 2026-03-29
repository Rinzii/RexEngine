using Rex.Shared.Net;

namespace Rex.Client.Tests;

public sealed class CommandLineArgsTests
{
    [Fact]
    public void TryParse_empty_defaults_to_standalone_and_default_port()
    {
        var ok = CommandLineArgs.TryParse(Array.Empty<string>(), out var parsed, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(parsed);
        Assert.Equal(NetMode.Standalone, parsed!.Mode);
        Assert.Equal(ProtocolConstants.DefaultPort, parsed.Port);
        Assert.False(parsed.Headless);
    }

    [Fact]
    public void TryParse_connect_sets_client_mode()
    {
        var ok = CommandLineArgs.TryParse(new[] { "--connect", "127.0.0.1" }, out var parsed, out _);

        Assert.True(ok);
        Assert.Equal(NetMode.Client, parsed!.Mode);
        Assert.Equal("127.0.0.1", parsed.ConnectAddress);
    }

    [Fact]
    public void TryParse_listen_sets_listen_server_mode()
    {
        var ok = CommandLineArgs.TryParse(new[] { "--listen" }, out var parsed, out _);

        Assert.True(ok);
        Assert.Equal(NetMode.ListenServer, parsed!.Mode);
    }
}
