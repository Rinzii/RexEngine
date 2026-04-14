namespace Rex.Sandbox.Server.Tests;

// Locks dedicated server argv parsing for every flag and error path.
public sealed class CommandLineArgsRegressionTests
{
    private static readonly string[] s_configFileAndDataDir =
        ["--config-file", "game.yml", "--data-dir", "/var/rex"];

    private static readonly string[] s_configFileOnly = ["--config-file"];
    private static readonly string[] s_dataDirOnly = ["--data-dir"];
    private static readonly string[] s_portMaxTick = ["--port", "30000", "--max-players", "8", "--tick-rate", "30"];
    private static readonly string[] s_portOnly = ["--port"];
    private static readonly string[] s_portX = ["--port", "x"];
    private static readonly string[] s_cvarTimeout = ["--cvar", "net.timeout=30"];
    private static readonly string[] s_cvarOnly = ["--cvar"];
    private static readonly string[] s_cvarNoEquals = ["--cvar", "noequals"];
    private static readonly string[] s_logLevelRexDebug = ["--logLevel", "Rex.Server=Debug"];
    private static readonly string[] s_logLevelOnly = ["--logLevel"];
    private static readonly string[] s_logLevelVerbose = ["--logLevel", "verbose"];
    private static readonly string[] s_plusEchoQuit = ["+echo", "+quit"];
    private static readonly string[] s_expectedExecEchoQuit = ["echo", "quit"];
    private static readonly string[] s_port27015UnknownFlag = ["--port", "27015", "--unknown-flag"];

    [Fact]
    public void Regression_config_file_and_data_dir_parse()
    {
        bool ok = CommandLineArgs.TryParse(
            s_configFileAndDataDir,
            out CommandLineArgs? parsed,
            out _);

        Assert.True(ok);
        Assert.Equal("game.yml", parsed!.ConfigFile);
        Assert.Equal("/var/rex", parsed.DataDir);
    }

    [Fact]
    public void Regression_config_file_missing_value_fails()
    {
        bool ok = CommandLineArgs.TryParse(s_configFileOnly, out _, out string? error);

        Assert.False(ok);
        Assert.Equal("Missing value for --config-file.", error);
    }

    [Fact]
    public void Regression_data_dir_missing_value_fails()
    {
        bool ok = CommandLineArgs.TryParse(s_dataDirOnly, out _, out string? error);

        Assert.False(ok);
        Assert.Equal("Missing value for --data-dir.", error);
    }

    [Fact]
    public void Regression_port_max_players_and_tick_rate_together()
    {
        bool ok = CommandLineArgs.TryParse(
            s_portMaxTick,
            out CommandLineArgs? parsed,
            out _);

        Assert.True(ok);
        Assert.Equal(30000, parsed!.Port);
        Assert.Equal(8, parsed.MaxPlayers);
        Assert.Equal(30, parsed.TickRate);
    }

    [Fact]
    public void Regression_port_missing_value_fails()
    {
        bool ok = CommandLineArgs.TryParse(s_portOnly, out _, out string? error);

        Assert.False(ok);
        Assert.Equal("Missing or invalid value for --port.", error);
    }

    [Fact]
    public void Regression_port_non_integer_fails()
    {
        bool ok = CommandLineArgs.TryParse(s_portX, out _, out string? error);

        Assert.False(ok);
        Assert.Equal("Missing or invalid value for --port.", error);
    }

    [Fact]
    public void Regression_cvar_key_value_parses()
    {
        bool ok = CommandLineArgs.TryParse(s_cvarTimeout, out CommandLineArgs? parsed, out _);

        Assert.True(ok);
        _ = Assert.Single(parsed!.CVars);
        Assert.Equal(("net.timeout", "30"), parsed.CVars.First());
    }

    [Fact]
    public void Regression_cvar_missing_value_fails()
    {
        bool ok = CommandLineArgs.TryParse(s_cvarOnly, out _, out string? error);

        Assert.False(ok);
        Assert.Equal("Missing value for --cvar.", error);
    }

    [Fact]
    public void Regression_cvar_without_equals_fails()
    {
        bool ok = CommandLineArgs.TryParse(s_cvarNoEquals, out _, out string? error);

        Assert.False(ok);
        Assert.Equal("Expected key=value after --cvar.", error);
    }

    [Fact]
    public void Regression_log_level_key_value_parses()
    {
        bool ok = CommandLineArgs.TryParse(
            s_logLevelRexDebug,
            out CommandLineArgs? parsed,
            out _);

        Assert.True(ok);
        _ = Assert.Single(parsed!.LogLevels);
        Assert.Equal(("Rex.Server", "Debug"), parsed.LogLevels.First());
    }

    [Fact]
    public void Regression_log_level_missing_value_fails()
    {
        bool ok = CommandLineArgs.TryParse(s_logLevelOnly, out _, out string? error);

        Assert.False(ok);
        Assert.Equal("Missing value for --logLevel.", error);
    }

    [Fact]
    public void Regression_log_level_without_equals_fails()
    {
        bool ok = CommandLineArgs.TryParse(s_logLevelVerbose, out _, out string? error);

        Assert.False(ok);
        Assert.Equal("Expected key=value after --logLevel.", error);
    }

    [Fact]
    public void Regression_plus_prefix_collects_exec_commands()
    {
        bool ok = CommandLineArgs.TryParse(s_plusEchoQuit, out CommandLineArgs? parsed, out _);

        Assert.True(ok);
        Assert.Equal(s_expectedExecEchoQuit, parsed!.ExecCommands);
    }

    [Fact]
    public void Regression_unknown_switch_is_unrecognized()
    {
        bool ok = CommandLineArgs.TryParse(s_port27015UnknownFlag, out CommandLineArgs? parsed, out _);

        Assert.True(ok);
        _ = Assert.Single(parsed!.UnrecognizedArguments);
        Assert.Equal("--unknown-flag", parsed.UnrecognizedArguments[0]);
    }
}
