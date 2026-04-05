using Rex.Shared.Startup;

namespace Rex.Server.Startup;

/// <summary>Parsed dedicated server options from the command line.</summary>
public sealed record ServerStartOptions(int Port, int TickRate, int MaxPlayers)
{
    /// <summary>Parses argv using <paramref name="definition"/> as defaults.</summary>
    /// <param name="args">Tokens after the executable name.</param>
    /// <param name="definition">Baseline port, tick rate and player cap from the game.</param>
    /// <param name="options">Filled when parsing succeeds.</param>
    /// <param name="error">Plain text when parsing fails.</param>
    public static bool TryParse(
        IReadOnlyList<string> args,
        GameServerStartDefinition definition,
        out ServerStartOptions options,
        out string? error)
    {
        var port = definition.DefaultPort;
        var tickRate = definition.TickRate;
        var maxPlayers = definition.MaxPlayers;

        using var enumerator = args.GetEnumerator();
        while (enumerator.MoveNext())
        {
            switch (enumerator.Current)
            {
                case "--port" when !enumerator.MoveNext() || !int.TryParse(enumerator.Current, out port) || port is <= 0 or > 65535:
                    options = null!;
                    error = "Missing or invalid value for --port.";
                    return false;
                case "--tick-rate" when !enumerator.MoveNext() || !int.TryParse(enumerator.Current, out tickRate) || tickRate <= 0:
                    options = null!;
                    error = "Missing or invalid value for --tick-rate.";
                    return false;
                case "--max-players" when !enumerator.MoveNext() || !int.TryParse(enumerator.Current, out maxPlayers) || maxPlayers <= 0:
                    options = null!;
                    error = "Missing or invalid value for --max-players.";
                    return false;
            }
        }

        options = new ServerStartOptions(port, tickRate, maxPlayers);
        error = null;
        return true;
    }
}
