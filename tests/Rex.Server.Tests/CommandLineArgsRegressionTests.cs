using System.Linq;
using Rex.Shared.Net;

namespace Rex.Sandbox.Server.Tests;

// Locks dedicated server argv parsing for every flag and error path.
public sealed class CommandLineArgsRegressionTests
{
    private static readonly string[] ConfigFileAndDataDir =
        ["--config-file", "game.yml", "--data-dir", "/var/rex"];

    private static readonly string[] ConfigFileOnly = ["--config-file"];
    private static readonly string[] DataDirOnly = ["--data-dir"];
    private static readonly string[] PortMaxTick = ["--port", "30000", "--max-players", "8", "--tick-rate", "30"];
    private static readonly string[] PortOnly = ["--port"];
    private static readonly string[] PortX = ["--port", "x"];
    private static readonly string[] CvarTimeout = ["--cvar", "net.timeout=30"];
    private static readonly string[] CvarOnly = ["--cvar"];
    private static readonly string[] CvarNoEquals = ["--cvar", "noequals"];
    private static readonly string[] LogLevelRexDebug = ["--logLevel", "Rex.Server=Debug"];
    private static readonly string[] LogLevelOnly = ["--logLevel"];
    private static readonly string[] LogLevelVerbose = ["--logLevel", "verbose"];
    private static readonly string[] PlusEchoQuit = ["+echo", "+quit"];
    private static readonly string[] ExpectedExecEchoQuit = ["echo", "quit"];
    private static readonly string[] Port27015UnknownFlag = ["--port", "27015", "--unknown-flag"];

    [Fact]
    public void Regression_config_file_and_data_dir_parse()
    {
        var ok = CommandLineArgs.TryParse(
            ConfigFileAndDataDir,
            out var parsed,
            out _);

        Assert.True(ok);
        Assert.Equal("game.yml", parsed!.ConfigFile);
        Assert.Equal("/var/rex", parsed.DataDir);
    }

    [Fact]
    public void Regression_config_file_missing_value_fails()
    {
        var ok = CommandLineArgs.TryParse(ConfigFileOnly, out _, out var error);

        Assert.False(ok);
        Assert.Equal("Missing value for --config-file.", error);
    }

    [Fact]
    public void Regression_data_dir_missing_value_fails()
    {
        var ok = CommandLineArgs.TryParse(DataDirOnly, out _, out var error);

        Assert.False(ok);
        Assert.Equal("Missing value for --data-dir.", error);
    }

    [Fact]
    public void Regression_port_max_players_and_tick_rate_together()
    {
        var ok = CommandLineArgs.TryParse(
            PortMaxTick,
            out var parsed,
            out _);

        Assert.True(ok);
        Assert.Equal(30000, parsed!.Port);
        Assert.Equal(8, parsed.MaxPlayers);
        Assert.Equal(30, parsed.TickRate);
    }

    [Fact]
    public void Regression_port_missing_value_fails()
    {
        var ok = CommandLineArgs.TryParse(PortOnly, out _, out var error);

        Assert.False(ok);
        Assert.Equal("Missing or invalid value for --port.", error);
    }

    [Fact]
    public void Regression_port_non_integer_fails()
    {
        var ok = CommandLineArgs.TryParse(PortX, out _, out var error);

        Assert.False(ok);
        Assert.Equal("Missing or invalid value for --port.", error);
    }

    [Fact]
    public void Regression_cvar_key_value_parses()
    {
        var ok = CommandLineArgs.TryParse(CvarTimeout, out var parsed, out _);

        Assert.True(ok);
        Assert.Single(parsed!.CVars);
        Assert.Equal(("net.timeout", "30"), parsed.CVars.First());
    }

    [Fact]
    public void Regression_cvar_missing_value_fails()
    {
        var ok = CommandLineArgs.TryParse(CvarOnly, out _, out var error);

        Assert.False(ok);
        Assert.Equal("Missing value for --cvar.", error);
    }

    [Fact]
    public void Regression_cvar_without_equals_fails()
    {
        var ok = CommandLineArgs.TryParse(CvarNoEquals, out _, out var error);

        Assert.False(ok);
        Assert.Equal("Expected key=value after --cvar.", error);
    }

    [Fact]
    public void Regression_log_level_key_value_parses()
    {
        var ok = CommandLineArgs.TryParse(
            LogLevelRexDebug,
            out var parsed,
            out _);

        Assert.True(ok);
        Assert.Single(parsed!.LogLevels);
        Assert.Equal(("Rex.Server", "Debug"), parsed.LogLevels.First());
    }

    [Fact]
    public void Regression_log_level_missing_value_fails()
    {
        var ok = CommandLineArgs.TryParse(LogLevelOnly, out _, out var error);

        Assert.False(ok);
        Assert.Equal("Missing value for --logLevel.", error);
    }

    [Fact]
    public void Regression_log_level_without_equals_fails()
    {
        var ok = CommandLineArgs.TryParse(LogLevelVerbose, out _, out var error);

        Assert.False(ok);
        Assert.Equal("Expected key=value after --logLevel.", error);
    }

    [Fact]
    public void Regression_plus_prefix_collects_exec_commands()
    {
        var ok = CommandLineArgs.TryParse(PlusEchoQuit, out var parsed, out _);

        Assert.True(ok);
        Assert.Equal(ExpectedExecEchoQuit, parsed!.ExecCommands);
    }

    [Fact]
    public void Regression_unknown_switch_is_unrecognized()
    {
        var ok = CommandLineArgs.TryParse(Port27015UnknownFlag, out var parsed, out _);

        Assert.True(ok);
        Assert.Single(parsed!.UnrecognizedArguments);
        Assert.Equal("--unknown-flag", parsed.UnrecognizedArguments[0]);
    }
}
