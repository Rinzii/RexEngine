using System.Diagnostics.CodeAnalysis;

namespace Rex.Client;

internal sealed class CommandLineArgs
{
    public bool Headless { get; }

    public static bool TryParse(IReadOnlyList<string> args, [NotNullWhen(true)] out CommandLineArgs? parsed)
    {
        parsed = null;
        var headless = false;

        using var enumerator = args.GetEnumerator();

        while (enumerator.MoveNext())
        {
            var arg = enumerator.Current;
            if (arg == "--headless")
            {
                headless = true;
            }
        }

        parsed = new CommandLineArgs(
            headless
        );
        
        return true;
    }

    private CommandLineArgs(
        bool headless
    )
    {
        Headless = headless;
    }
}