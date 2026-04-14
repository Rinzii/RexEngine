using Rex.Shared.Net;

namespace Rex.Sandbox.Client.Tests;

// Client process command line parsing.
public sealed class CommandLineArgsTests
{
    private static readonly string[] s_connectLocalHost = ["--connect", "127.0.0.1"];
    private static readonly string[] s_connectLocalHostPort27015 = ["--connect", "127.0.0.1", "--port", "27015"];
    private static readonly string[] s_connectHPortTrailing = ["--connect", "h", "--port"];
    private static readonly string[] s_listenPortInvalid = ["--listen", "--port", "not-a-port"];
    private static readonly string[] s_listenOnly = ["--listen"];

    [Fact]
    // No args mean standalone mode and default port.
    public void TryParse_empty_defaults_to_standalone_and_default_port()
    {
        bool ok = CommandLineArgs.TryParse(Array.Empty<string>(), out CommandLineArgs? parsed, out string? error);

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
        bool ok = CommandLineArgs.TryParse(s_connectLocalHost, out CommandLineArgs? parsed, out _);

        Assert.True(ok);
        Assert.Equal(NetMode.Client, parsed!.Mode);
        Assert.Equal("127.0.0.1", parsed.ConnectAddress);
    }

    [Fact]
    // --port after --connect sets Port and leaves nothing unrecognized.
    public void TryParse_connect_and_port_parses_port()
    {
        bool ok = CommandLineArgs.TryParse(
            s_connectLocalHostPort27015,
            out CommandLineArgs? parsed,
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
        bool ok = CommandLineArgs.TryParse(s_connectHPortTrailing, out _, out string? error);

        Assert.False(ok);
        Assert.Equal("Missing value for --port.", error);
    }

    [Fact]
    // A non-numeric --port value is a parse error.
    public void TryParse_port_non_integer_fails()
    {
        bool ok = CommandLineArgs.TryParse(
            s_listenPortInvalid,
            out _,
            out string? error);

        Assert.False(ok);
        Assert.Equal("Invalid value for --port.", error);
    }

    [Fact]
    // Listen flag selects listen server mode.
    public void TryParse_listen_sets_listen_server_mode()
    {
        bool ok = CommandLineArgs.TryParse(s_listenOnly, out CommandLineArgs? parsed, out _);

        Assert.True(ok);
        Assert.Equal(NetMode.ListenServer, parsed!.Mode);
    }
}
