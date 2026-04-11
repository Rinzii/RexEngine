using Rex.Shared.Net;

namespace Rex.Sandbox.Client.Tests;

// Locks client argv behavior for modes flags port and error strings.
public sealed class CommandLineArgsRegressionTests
{
    private static readonly string[] s_connectOnly = ["--connect"];
    private static readonly string[] s_headlessConnect10 = ["--headless", "--connect", "10.0.0.1"];
    private static readonly string[] s_connectHStandalone = ["--connect", "h", "--standalone"];
    private static readonly string[] s_listenConnectHost = ["--listen", "--connect", "host.example"];
    private static readonly string[] s_connectXFrobnicate = ["--connect", "x", "--frobnicate"];
    private static readonly string[] s_port12345ConnectLocal = ["--port", "12345", "--connect", "127.0.0.1"];
    private static readonly string[] s_listenPort28000 = ["--listen", "--port", "28000"];
    private static readonly string[] s_connectLocalHost = ["--connect", "127.0.0.1"];

    [Fact]
    public void Regression_connect_without_host_fails()
    {
        bool ok = CommandLineArgs.TryParse(s_connectOnly, out _, out string? error);

        Assert.False(ok);
        Assert.Equal("Missing value for --connect.", error);
    }

    [Fact]
    public void Regression_headless_persists_with_client_mode()
    {
        bool ok = CommandLineArgs.TryParse(
            s_headlessConnect10,
            out CommandLineArgs? parsed,
            out _);

        Assert.True(ok);
        Assert.True(parsed!.Headless);
        Assert.Equal(NetMode.Client, parsed.Mode);
    }

    [Fact]
    public void Regression_standalone_flag_overrides_connect_for_mode()
    {
        bool ok = CommandLineArgs.TryParse(
            s_connectHStandalone,
            out CommandLineArgs? parsed,
            out _);

        Assert.True(ok);
        Assert.Equal(NetMode.Standalone, parsed!.Mode);
        Assert.Equal("h", parsed.ConnectAddress);
    }

    [Fact]
    public void Regression_connect_overrides_listen_when_standalone_not_set()
    {
        bool ok = CommandLineArgs.TryParse(
            s_listenConnectHost,
            out CommandLineArgs? parsed,
            out _);

        Assert.True(ok);
        Assert.Equal(NetMode.Client, parsed!.Mode);
        Assert.Equal("host.example", parsed.ConnectAddress);
    }

    [Fact]
    public void Regression_unknown_token_is_unrecognized()
    {
        bool ok = CommandLineArgs.TryParse(s_connectXFrobnicate, out CommandLineArgs? parsed, out _);

        Assert.True(ok);
        _ = Assert.Single(parsed!.UnrecognizedArguments);
        Assert.Equal("--frobnicate", parsed.UnrecognizedArguments[0]);
    }

    [Fact]
    public void Regression_port_before_connect_still_applies()
    {
        bool ok = CommandLineArgs.TryParse(
            s_port12345ConnectLocal,
            out CommandLineArgs? parsed,
            out _);

        Assert.True(ok);
        Assert.Equal(12345, parsed!.Port);
        Assert.Equal(NetMode.Client, parsed.Mode);
        Assert.Empty(parsed.UnrecognizedArguments);
    }

    [Fact]
    public void Regression_listen_with_port_keeps_listen_mode()
    {
        bool ok = CommandLineArgs.TryParse(s_listenPort28000, out CommandLineArgs? parsed, out _);

        Assert.True(ok);
        Assert.Equal(NetMode.ListenServer, parsed!.Mode);
        Assert.Equal(28000, parsed.Port);
    }

    [Fact]
    public void Regression_connect_only_uses_default_port()
    {
        bool ok = CommandLineArgs.TryParse(s_connectLocalHost, out CommandLineArgs? parsed, out _);

        Assert.True(ok);
        Assert.Equal(ProtocolConstants.DefaultPort, parsed!.Port);
    }
}
