using Rex.Shared.Net;
using Rex.Shared.Startup;

namespace Rex.Client.Startup;

/// <summary>
/// Parsed client startup options for a game executable.
/// </summary>
public sealed record ClientStartOptions(bool Headless, NetMode Mode, string ConnectAddress, int Port)
{
    /// <summary>
    /// Parses the client startup command line.
    /// </summary>
    /// <param name="args">Tokens after the executable name.</param>
    /// <param name="definition">Baseline host, port and mode defaults from the game.</param>
    /// <param name="options">Filled when parsing succeeds.</param>
    /// <param name="error">Human readable failure text when parsing fails.</param>
    public static bool TryParse(
        IReadOnlyList<string> args,
        GameClientStartDefinition definition,
        out ClientStartOptions options,
        out string? error)
    {
        bool headless = false;
        bool listen = false;
        bool standalone = false;
        string? connectAddress = null;
        int port = definition.DefaultPort;

        using IEnumerator<string> enumerator = args.GetEnumerator();
        while (enumerator.MoveNext())
        {
            switch (enumerator.Current)
            {
                case "--headless":
                    headless = true;
                    break;
                case "--listen":
                    listen = true;
                    break;
                case "--standalone":
                    standalone = true;
                    break;
                case "--connect" when !enumerator.MoveNext():
                    options = null!;
                    error = "Missing value for --connect.";
                    return false;
                case "--connect":
                    connectAddress = enumerator.Current;
                    break;
                case "--port" when !enumerator.MoveNext():
                    options = null!;
                    error = "Missing value for --port.";
                    return false;
                case "--port" when !int.TryParse(enumerator.Current, out port) || port is <= 0 or > 65535:
                    options = null!;
                    error = "Invalid value for --port.";
                    return false;
                default:
                    continue;
            }
        }

        NetMode mode = standalone
            ? NetMode.Standalone
            : listen
                ? NetMode.ListenServer
                : connectAddress is null
                    ? NetMode.Standalone
                    : NetMode.Client;

        options = new ClientStartOptions(headless, mode, connectAddress ?? definition.DefaultHost, port);
        error = null;
        return true;
    }
}
