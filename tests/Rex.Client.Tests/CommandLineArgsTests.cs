using Rex.Shared.Net;

namespace Rex.Sandbox.Client.Tests;

// Client process command line parsing.
public sealed class CommandLineArgsTests
{
    [Fact]
    // No args mean standalone mode and default port.
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
    // Connect flag switches to client mode with host.
    public void TryParse_connect_sets_client_mode()
    {
        var ok = CommandLineArgs.TryParse(new[] { "--connect", "127.0.0.1" }, out var parsed, out _);

        Assert.True(ok);
        Assert.Equal(NetMode.Client, parsed!.Mode);
        Assert.Equal("127.0.0.1", parsed.ConnectAddress);
    }

    [Fact]
    // --port after --connect sets Port and leaves nothing unrecognized.
    public void TryParse_connect_and_port_parses_port()
    {
        var ok = CommandLineArgs.TryParse(
            new[] { "--connect", "127.0.0.1", "--port", "27015" },
            out var parsed,
            out _);

        Assert.True(ok);
        Assert.Equal(NetMode.Client, parsed!.Mode);
        Assert.Equal("127.0.0.1", parsed.ConnectAddress);
        Assert.Equal(27015, parsed.Port);
        Assert.Empty(parsed.UnrecognizedArguments);
    }

    [Fact]
    // Trailing --port without a value is a parse error.
    public void TryParse_port_without_value_fails()
    {
        var ok = CommandLineArgs.TryParse(new[] { "--connect", "h", "--port" }, out _, out var error);

        Assert.False(ok);
        Assert.Equal("Missing value for --port.", error);
    }

    [Fact]
    // Non-numeric --port value is a parse error.
    public void TryParse_port_non_integer_fails()
    {
        var ok = CommandLineArgs.TryParse(
            new[] { "--listen", "--port", "not-a-port" },
            out _,
            out var error);

        Assert.False(ok);
        Assert.Equal("Invalid value for --port.", error);
    }

    [Fact]
    // Listen flag selects listen server mode.
    public void TryParse_listen_sets_listen_server_mode()
    {
        var ok = CommandLineArgs.TryParse(new[] { "--listen" }, out var parsed, out _);

        Assert.True(ok);
        Assert.Equal(NetMode.ListenServer, parsed!.Mode);
    }
}
