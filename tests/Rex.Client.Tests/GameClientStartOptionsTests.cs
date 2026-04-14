using Rex.Client.Startup;
using Rex.Shared.Net;
using Rex.Shared.Startup;

namespace Rex.Sandbox.Client.Tests;

public sealed class GameClientStartOptionsTests
{
    private static readonly GameClientStartDefinition s_definition = new(
        new GameRuntimeIdentity("TestGame", "Test.Shared", "Test.Client", "Test.Server"),
        "127.0.0.1",
        27015,
        60,
        new GameWindowDefinition("Test Game", 1280, 720),
        new ListenServerDefinition("TEST_SERVER_DLL", "Test.Server.dll", "TEST_READY"));

    [Fact]
    public void TryParse_empty_defaults_to_standalone()
    {
        bool ok = ClientStartOptions.TryParse(Array.Empty<string>(), s_definition, out ClientStartOptions? parsed,
            out string? error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(parsed);
        Assert.False(parsed!.Headless);
        Assert.Equal(NetMode.Standalone, parsed.Mode);
        Assert.Equal(s_definition.DefaultHost, parsed.ConnectAddress);
        Assert.Equal(s_definition.DefaultPort, parsed.Port);
    }

    [Fact]
    public void TryParse_listen_and_headless_preserve_runtime_flags()
    {
        bool ok = ClientStartOptions.TryParse(
            ["--headless", "--listen", "--port", "28000"],
            s_definition,
            out ClientStartOptions? parsed,
            out string? error);

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
        bool ok = ClientStartOptions.TryParse(
            ["--connect", "host.example", "--standalone"],
            s_definition,
            out ClientStartOptions? parsed,
            out _);

        Assert.True(ok);
        Assert.Equal(NetMode.Standalone, parsed!.Mode);
        Assert.Equal("host.example", parsed.ConnectAddress);
    }

    [Fact]
    public void TryParse_missing_connect_value_fails()
    {
        bool ok = ClientStartOptions.TryParse(["--connect"], s_definition, out _, out string? error);

        Assert.False(ok);
        Assert.Equal("Missing value for --connect.", error);
    }

    [Fact]
    public void TryParse_invalid_port_value_fails()
    {
        bool ok = ClientStartOptions.TryParse(["--port", "bad"], s_definition, out _, out string? error);

        Assert.False(ok);
        Assert.Equal("Invalid value for --port.", error);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("70000")]
    public void TryParse_out_of_range_port_fails(string port)
    {
        bool ok = ClientStartOptions.TryParse(["--port", port], s_definition, out _, out string? error);

        Assert.False(ok);
        Assert.Equal("Invalid value for --port.", error);
    }
}
