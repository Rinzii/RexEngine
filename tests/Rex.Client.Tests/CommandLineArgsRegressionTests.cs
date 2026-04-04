using Rex.Shared.Net;

namespace Rex.Sandbox.Client.Tests;

// Locks client argv behavior for modes flags port and error strings.
public sealed class CommandLineArgsRegressionTests
{
    [Fact]
    public void Regression_connect_without_host_fails()
    {
        var ok = CommandLineArgs.TryParse(new[] { "--connect" }, out _, out var error);

        Assert.False(ok);
        Assert.Equal("Missing value for --connect.", error);
    }

    [Fact]
    public void Regression_headless_persists_with_client_mode()
    {
        var ok = CommandLineArgs.TryParse(
            new[] { "--headless", "--connect", "10.0.0.1" },
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
            new[] { "--connect", "h", "--standalone" },
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
            new[] { "--listen", "--connect", "host.example" },
            out var parsed,
            out _);

        Assert.True(ok);
        Assert.Equal(NetMode.Client, parsed!.Mode);
        Assert.Equal("host.example", parsed.ConnectAddress);
    }

    [Fact]
    public void Regression_unknown_token_is_unrecognized()
    {
        var ok = CommandLineArgs.TryParse(new[] { "--connect", "x", "--frobnicate" }, out var parsed, out _);

        Assert.True(ok);
        Assert.Single(parsed!.UnrecognizedArguments);
        Assert.Equal("--frobnicate", parsed.UnrecognizedArguments[0]);
    }

    [Fact]
    public void Regression_port_before_connect_still_applies()
    {
        var ok = CommandLineArgs.TryParse(
            new[] { "--port", "12345", "--connect", "127.0.0.1" },
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
        var ok = CommandLineArgs.TryParse(new[] { "--listen", "--port", "28000" }, out var parsed, out _);

        Assert.True(ok);
        Assert.Equal(NetMode.ListenServer, parsed!.Mode);
        Assert.Equal(28000, parsed.Port);
    }

    [Fact]
    public void Regression_connect_only_uses_default_port()
    {
        var ok = CommandLineArgs.TryParse(new[] { "--connect", "127.0.0.1" }, out var parsed, out _);

        Assert.True(ok);
        Assert.Equal(ProtocolConstants.DefaultPort, parsed!.Port);
    }
}
