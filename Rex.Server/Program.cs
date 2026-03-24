using System;

namespace Rex.Server;

internal static class Program
{
    private static bool _hasStarted;
    internal static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
    }

    internal static void Start(string[] args)
    {
        if (_hasStarted)
        {
            throw new InvalidOperationException("Server attempted to start again!");
        }

        _hasStarted = true;

        if (!CommandLineArgs.TryParse(args, out var parsed))
        {
            return;
        }
    }

    private static void ParsedMain(CommandLineArgs args)
    {
        
    }
}