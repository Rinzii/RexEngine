using Rex.Client.Startup;
using Rex.Shared.Net;
using Rex.Shared.Startup;

namespace Rex.Sandbox.Client.Tests;

public sealed class GameClientStartOptionsTests
{
    private static readonly GameClientStartDefinition Definition = new(
        new GameRuntimeIdentity("TestGame", "Test.Shared", "Test.Client", "Test.Server"),
        "127.0.0.1",
        27015,
        60,
        new GameWindowDefinition("Test Game", 1280, 720),
        new ListenServerDefinition("TEST_SERVER_DLL", "Test.Server.dll", "TEST_READY"));

    [Fact]
    public void TryParse_empty_defaults_to_standalone()
    {
        var ok = ClientStartOptions.TryParse(Array.Empty<string>(), Definition, out var parsed, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(parsed);
        Assert.False(parsed!.Headless);
        Assert.Equal(NetMode.Standalone, parsed.Mode);
        Assert.Equal(Definition.DefaultHost, parsed.ConnectAddress);
        Assert.Equal(Definition.DefaultPort, parsed.Port);
    }

    [Fact]
    public void TryParse_listen_and_headless_preserve_runtime_flags()
    {
        var ok = ClientStartOptions.TryParse(
            ["--headless", "--listen", "--port", "28000"],
            Definition,
            out var parsed,
            out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(parsed);
        Assert.True(parsed!.Headless);
        Assert.Equal(NetMode.ListenServer, parsed.Mode);
        Assert.Equal(28000, parsed.Port);
    }

    [Fact]
    public void TryParse_standalone_overrides_connect_for_mode()
    {
        var ok = ClientStartOptions.TryParse(
            ["--connect", "host.example", "--standalone"],
            Definition,
            out var parsed,
            out _);

        Assert.True(ok);
        Assert.Equal(NetMode.Standalone, parsed!.Mode);
        Assert.Equal("host.example", parsed.ConnectAddress);
    }

    [Fact]
    public void TryParse_missing_connect_value_fails()
    {
        var ok = ClientStartOptions.TryParse(["--connect"], Definition, out _, out var error);

        Assert.False(ok);
        Assert.Equal("Missing value for --connect.", error);
    }

    [Fact]
    public void TryParse_invalid_port_value_fails()
    {
        var ok = ClientStartOptions.TryParse(["--port", "bad"], Definition, out _, out var error);

        Assert.False(ok);
        Assert.Equal("Invalid value for --port.", error);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("70000")]
    public void TryParse_out_of_range_port_fails(string port)
    {
        var ok = ClientStartOptions.TryParse(["--port", port], Definition, out _, out var error);

        Assert.False(ok);
        Assert.Equal("Invalid value for --port.", error);
    }
}
