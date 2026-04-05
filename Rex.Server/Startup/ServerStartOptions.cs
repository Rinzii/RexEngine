using Rex.Shared.Startup;

namespace Rex.Server.Startup;

/// <summary>
/// Parsed server startup options for a game executable.
/// </summary>
public sealed record ServerStartOptions(int Port, int TickRate, int MaxPlayers)
{
    /// <summary>
    /// Parses the server startup command line.
    /// </summary>
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
                    options = default!;
                    error = "Missing or invalid value for --port.";
                    return false;
                case "--tick-rate" when !enumerator.MoveNext() || !int.TryParse(enumerator.Current, out tickRate) || tickRate <= 0:
                    options = default!;
                    error = "Missing or invalid value for --tick-rate.";
                    return false;
                case "--max-players" when !enumerator.MoveNext() || !int.TryParse(enumerator.Current, out maxPlayers) || maxPlayers <= 0:
                    options = default!;
                    error = "Missing or invalid value for --max-players.";
                    return false;
            }
        }

        options = new ServerStartOptions(port, tickRate, maxPlayers);
        error = null;
        return true;
    }
}
