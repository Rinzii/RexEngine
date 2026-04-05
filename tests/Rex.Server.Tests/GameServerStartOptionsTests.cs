using Rex.Server.Startup;
using Rex.Shared.Startup;

namespace Rex.Sandbox.Server.Tests;

public sealed class GameServerStartOptionsTests
{
    private static readonly GameServerStartDefinition Definition = new(
        new GameRuntimeIdentity("TestGame", "Test.Shared", "Test.Client", "Test.Server"),
        "Test Dedicated Server",
        "TEST_READY",
        27015,
        60,
        32);

    [Fact]
    public void TryParse_empty_uses_definition_defaults()
    {
        var ok = ServerStartOptions.TryParse(Array.Empty<string>(), Definition, out var parsed, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(parsed);
        Assert.Equal(Definition.DefaultPort, parsed!.Port);
        Assert.Equal(Definition.TickRate, parsed.TickRate);
        Assert.Equal(Definition.MaxPlayers, parsed.MaxPlayers);
    }

    [Fact]
    public void TryParse_overrides_all_values()
    {
        var ok = ServerStartOptions.TryParse(
            ["--port", "29000", "--tick-rate", "30", "--max-players", "12"],
            Definition,
            out var parsed,
            out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(parsed);
        Assert.Equal(29000, parsed!.Port);
        Assert.Equal(30, parsed.TickRate);
        Assert.Equal(12, parsed.MaxPlayers);
    }

    [Fact]
    public void TryParse_missing_tick_rate_value_fails()
    {
        var ok = ServerStartOptions.TryParse(["--tick-rate"], Definition, out _, out var error);

        Assert.False(ok);
        Assert.Equal("Missing or invalid value for --tick-rate.", error);
    }

    [Fact]
    public void TryParse_invalid_max_players_value_fails()
    {
        var ok = ServerStartOptions.TryParse(["--max-players", "many"], Definition, out _, out var error);

        Assert.False(ok);
        Assert.Equal("Missing or invalid value for --max-players.", error);
    }

    [Fact]
    public void TryParse_zero_tick_rate_fails()
    {
        var ok = ServerStartOptions.TryParse(["--tick-rate", "0"], Definition, out _, out var error);

        Assert.False(ok);
        Assert.Equal("Missing or invalid value for --tick-rate.", error);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("70000")]
    public void TryParse_out_of_range_port_fails(string port)
    {
        var ok = ServerStartOptions.TryParse(["--port", port], Definition, out _, out var error);

        Assert.False(ok);
        Assert.Equal("Missing or invalid value for --port.", error);
    }
}
