using System.Diagnostics.CodeAnalysis;
using Rex.Shared.Net;

namespace Rex.Client;

internal sealed class CommandLineArgs
{
    public bool Headless { get; }
    public bool ListenServer { get; }
    public string? ConnectAddress { get; }
    public int Port { get; }

    public static bool TryParse(IReadOnlyList<string> args, [NotNullWhen(true)] out CommandLineArgs? parsed)
    {
        parsed = null;
        var headless = false;
        var listenServer = false;
        string? connectAddress = null;
        int port = ProtocolConstants.DefaultPort;

        using var enumerator = args.GetEnumerator();

        while (enumerator.MoveNext())
        {
            var arg = enumerator.Current;
            if (arg == "--headless")
            {
                headless = true;
            }
            else if (arg == "--listen")
            {
                listenServer = true;
            }
            else if (arg == "--connect")
            {
                if (!enumerator.MoveNext())
                {
                    Console.WriteLine("Missing connect address!");
                    return false;
                }

                connectAddress = enumerator.Current;
            }
            else if (arg == "--port")
            {
                if (!enumerator.MoveNext() || !int.TryParse(enumerator.Current, out port))
                {
                    Console.WriteLine("Missing or invalid port!");
                    return false;
                }
            }
        }

        parsed = new CommandLineArgs(headless, listenServer, connectAddress, port);

        return true;
    }

    private CommandLineArgs(bool headless, bool listenServer, string? connectAddress, int port)
    {
        Headless = headless;
        ListenServer = listenServer;
        ConnectAddress = connectAddress;
        Port = port;
    }
}
