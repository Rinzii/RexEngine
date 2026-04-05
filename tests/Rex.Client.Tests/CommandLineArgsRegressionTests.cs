using Rex.Shared.Net;

namespace Rex.Sandbox.Client.Tests;

// Locks client argv behavior for modes flags port and error strings.
public sealed class CommandLineArgsRegressionTests
{
    private static readonly string[] ConnectOnly = ["--connect"];
    private static readonly string[] HeadlessConnect10 = ["--headless", "--connect", "10.0.0.1"];
    private static readonly string[] ConnectHStandalone = ["--connect", "h", "--standalone"];
    private static readonly string[] ListenConnectHost = ["--listen", "--connect", "host.example"];
    private static readonly string[] ConnectXFrobnicate = ["--connect", "x", "--frobnicate"];
    private static readonly string[] Port12345ConnectLocal = ["--port", "12345", "--connect", "127.0.0.1"];
    private static readonly string[] ListenPort28000 = ["--listen", "--port", "28000"];
    private static readonly string[] ConnectLocalHost = ["--connect", "127.0.0.1"];

    [Fact]
    public void Regression_connect_without_host_fails()
    {
        var ok = CommandLineArgs.TryParse(ConnectOnly, out _, out var error);

        Assert.False(ok);
        Assert.Equal("Missing value for --connect.", error);
    }

    [Fact]
    public void Regression_headless_persists_with_client_mode()
    {
        var ok = CommandLineArgs.TryParse(
            HeadlessConnect10,
            out var parsed,
            out _);

        Assert.True(ok);
        Assert.True(parsed!.Headless);
        Assert.Equal(NetMode.Client, parsed.Mode);
    }

    [Fact]
    public void Regression_standalone_flag_overrides_connect_for_mode()
    {
        var ok = CommandLineArgs.TryParse(
            ConnectHStandalone,
            out var parsed,
            out _);

        Assert.True(ok);
        Assert.Equal(NetMode.Standalone, parsed!.Mode);
        Assert.Equal("h", parsed.ConnectAddress);
    }

    [Fact]
    public void Regression_connect_overrides_listen_when_standalone_not_set()
    {
        var ok = CommandLineArgs.TryParse(
            ListenConnectHost,
            out var parsed,
            out _);

        Assert.True(ok);
        Assert.Equal(NetMode.Client, parsed!.Mode);
        Assert.Equal("host.example", parsed.ConnectAddress);
    }

    [Fact]
    public void Regression_unknown_token_is_unrecognized()
    {
        var ok = CommandLineArgs.TryParse(ConnectXFrobnicate, out var parsed, out _);

        Assert.True(ok);
        Assert.Single(parsed!.UnrecognizedArguments);
        Assert.Equal("--frobnicate", parsed.UnrecognizedArguments[0]);
    }

    [Fact]
    public void Regression_port_before_connect_still_applies()
    {
        var ok = CommandLineArgs.TryParse(
            Port12345ConnectLocal,
            out var parsed,
            out _);

        Assert.True(ok);
        Assert.Equal(12345, parsed!.Port);
        Assert.Equal(NetMode.Client, parsed.Mode);
        Assert.Empty(parsed.UnrecognizedArguments);
    }

    [Fact]
    public void Regression_listen_with_port_keeps_listen_mode()
    {
        var ok = CommandLineArgs.TryParse(ListenPort28000, out var parsed, out _);

        Assert.True(ok);
        Assert.Equal(NetMode.ListenServer, parsed!.Mode);
        Assert.Equal(28000, parsed.Port);
    }

    [Fact]
    public void Regression_connect_only_uses_default_port()
    {
        var ok = CommandLineArgs.TryParse(ConnectLocalHost, out var parsed, out _);

        Assert.True(ok);
        Assert.Equal(ProtocolConstants.DefaultPort, parsed!.Port);
    }
}
