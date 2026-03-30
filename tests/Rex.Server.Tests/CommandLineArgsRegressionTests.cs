using System.Linq;
using Rex.Shared.Net;

namespace Rex.Server.Tests;

// Locks dedicated server argv parsing for every flag and error path.
public sealed class CommandLineArgsRegressionTests
{
    [Fact]
    public void Regression_config_file_and_data_dir_parse()
    {
        var ok = CommandLineArgs.TryParse(
            new[] { "--config-file", "game.yml", "--data-dir", "/var/rex" },
            out var parsed,
            out _);

        Assert.True(ok);
        Assert.Equal("game.yml", parsed!.ConfigFile);
        Assert.Equal("/var/rex", parsed.DataDir);
    }

    [Fact]
    public void Regression_config_file_missing_value_fails()
    {
        var ok = CommandLineArgs.TryParse(new[] { "--config-file" }, out _, out var error);

        Assert.False(ok);
        Assert.Equal("Missing value for --config-file.", error);
    }

    [Fact]
    public void Regression_data_dir_missing_value_fails()
    {
        var ok = CommandLineArgs.TryParse(new[] { "--data-dir" }, out _, out var error);

        Assert.False(ok);
        Assert.Equal("Missing value for --data-dir.", error);
    }

    [Fact]
    public void Regression_port_max_players_and_tick_rate_together()
    {
        var ok = CommandLineArgs.TryParse(
            new[] { "--port", "30000", "--max-players", "8", "--tick-rate", "30" },
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
        var ok = CommandLineArgs.TryParse(new[] { "--port" }, out _, out var error);

        Assert.False(ok);
        Assert.Equal("Missing or invalid value for --port.", error);
    }

    [Fact]
    public void Regression_port_non_integer_fails()
    {
        var ok = CommandLineArgs.TryParse(new[] { "--port", "x" }, out _, out var error);

        Assert.False(ok);
        Assert.Equal("Missing or invalid value for --port.", error);
    }

    [Fact]
    public void Regression_cvar_key_value_parses()
    {
        var ok = CommandLineArgs.TryParse(new[] { "--cvar", "net.timeout=30" }, out var parsed, out _);

        Assert.True(ok);
        Assert.Single(parsed!.CVars);
        Assert.Equal(("net.timeout", "30"), parsed.CVars.First());
    }

    [Fact]
    public void Regression_cvar_missing_value_fails()
    {
        var ok = CommandLineArgs.TryParse(new[] { "--cvar" }, out _, out var error);

        Assert.False(ok);
        Assert.Equal("Missing value for --cvar.", error);
    }

    [Fact]
    public void Regression_cvar_without_equals_fails()
    {
        var ok = CommandLineArgs.TryParse(new[] { "--cvar", "noequals" }, out _, out var error);

        Assert.False(ok);
        Assert.Equal("Expected key=value after --cvar.", error);
    }

    [Fact]
    public void Regression_log_level_key_value_parses()
    {
        var ok = CommandLineArgs.TryParse(
            new[] { "--logLevel", "Rex.Server=Debug" },
            out var parsed,
            out _);

        Assert.True(ok);
        Assert.Single(parsed!.LogLevels);
        Assert.Equal(("Rex.Server", "Debug"), parsed.LogLevels.First());
    }

    [Fact]
    public void Regression_log_level_missing_value_fails()
    {
        var ok = CommandLineArgs.TryParse(new[] { "--logLevel" }, out _, out var error);

        Assert.False(ok);
        Assert.Equal("Missing value for --logLevel.", error);
    }

    [Fact]
    public void Regression_log_level_without_equals_fails()
    {
        var ok = CommandLineArgs.TryParse(new[] { "--logLevel", "verbose" }, out _, out var error);

        Assert.False(ok);
        Assert.Equal("Expected key=value after --logLevel.", error);
    }

    [Fact]
    public void Regression_plus_prefix_collects_exec_commands()
    {
        var ok = CommandLineArgs.TryParse(new[] { "+echo", "+quit" }, out var parsed, out _);

        Assert.True(ok);
        Assert.Equal(new[] { "echo", "quit" }, parsed!.ExecCommands);
    }

    [Fact]
    public void Regression_unknown_switch_is_unrecognized()
    {
        var ok = CommandLineArgs.TryParse(new[] { "--port", "27015", "--unknown-flag" }, out var parsed, out _);

        Assert.True(ok);
        Assert.Single(parsed!.UnrecognizedArguments);
        Assert.Equal("--unknown-flag", parsed.UnrecognizedArguments[0]);
    }
}
