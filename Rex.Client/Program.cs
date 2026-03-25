using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;
using Rex.Client.Net;
using Rex.Shared.Net;
using Rex.Shared.Timing;

namespace Rex.Client;

internal static class Program
{
    private static void Main(string[] args)
    {
        if (!CommandLineArgs.TryParse(args, out var parsed))
        {
            return;
        }

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var logger = loggerFactory.CreateLogger("Rex.Client");

        if (parsed.ListenServer)
        {
            RunListenServer(parsed, loggerFactory, logger);
        }
        else if (parsed.ConnectAddress != null)
        {
            RunRemoteClient(parsed, loggerFactory, logger);
        }
        else
        {
            logger.LogError("No mode specified. Use --listen to host or --connect <ip> to join.");
        }
    }

    private static void RunListenServer(CommandLineArgs args, ILoggerFactory loggerFactory, ILogger logger)
    {
        using var serverProcess = StartListenServerProcess(args.Port, logger);
        if (serverProcess == null)
            return;

        try
        {
            RunRemoteClient(args, loggerFactory, logger, "127.0.0.1", args.Port);
        }
        finally
        {
            StopListenServerProcess(serverProcess, logger);
        }
    }

    private static void RunRemoteClient(CommandLineArgs args, ILoggerFactory loggerFactory, ILogger logger)
    {
        // Parse host:port from connect address.
        var address = args.ConnectAddress!;
        var host = address;
        var port = args.Port;

        var colonIndex = address.LastIndexOf(':');
        if (colonIndex > 0 && int.TryParse(address[(colonIndex + 1)..], out var parsedPort))
        {
            host = address[..colonIndex];
            port = parsedPort;
        }

        RunRemoteClient(args, loggerFactory, logger, host, port);
    }

    private static void RunRemoteClient(CommandLineArgs args, ILoggerFactory loggerFactory, ILogger logger, string host, int port)
    {
        var client = new GameClient(loggerFactory);

        var gameLoop = new GameLoop(ProtocolConstants.DefaultTickRate)
        {
            YieldBetweenFrames = !ShouldRender(args)
        };

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            logger.LogInformation("Shutdown signal received.");
            gameLoop.Stop();
        };

        client.Connect(host, port);

        gameLoop.OnTick = () =>
        {
            client.Tick(gameLoop.Clock.CurrentTick);
        };

        gameLoop.OnRender = alpha =>
        {
            if (!ShouldRender(args))
                return;

            // Future: render using SDL with interpolated state.
            // var entities = client.WorldState.GetInterpolatedState(alpha);
        };

        logger.LogInformation("Connecting to {Host}:{Port}. Press Ctrl+C to stop.", host, port);
        gameLoop.Run();

        client.Disconnect();
    }

    private static Process? StartListenServerProcess(int port, ILogger logger)
    {
        var serverAssemblyPath = ResolveServerAssemblyPath();
        if (serverAssemblyPath == null)
        {
            logger.LogError("Could not find the Rex.Server assembly to launch listen server mode.");
            return null;
        }

        var readySignal = new ManualResetEventSlim();
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(serverAssemblyPath)!
            },
            EnableRaisingEvents = true
        };

        process.StartInfo.ArgumentList.Add(serverAssemblyPath);
        process.StartInfo.ArgumentList.Add("--port");
        process.StartInfo.ArgumentList.Add(port.ToString());

        process.OutputDataReceived += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data))
                return;

            logger.LogInformation("[Server] {Message}", e.Data);
            if (e.Data.Contains("Dedicated server running.", StringComparison.Ordinal))
            {
                readySignal.Set();
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                logger.LogError("[Server] {Message}", e.Data);
            }
        };

        if (!process.Start())
        {
            logger.LogError("Failed to start the Rex.Server process.");
            process.Dispose();
            return null;
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (readySignal.Wait(TimeSpan.FromSeconds(10)))
            return process;

        if (process.HasExited)
        {
            logger.LogError("Listen server exited before it finished startup.");
        }
        else
        {
            logger.LogError("Timed out waiting for listen server startup.");
        }

        StopListenServerProcess(process, logger);
        process.Dispose();
        return null;
    }

    private static void StopListenServerProcess(Process process, ILogger logger)
    {
        if (process.HasExited)
            return;

        logger.LogInformation("Stopping listen server process.");
        process.Kill(true);
        process.WaitForExit(5000);
    }

    private static string? ResolveServerAssemblyPath()
    {
        var siblingServerPath = Path.Combine(AppContext.BaseDirectory, "Rex.Server.dll");
        if (File.Exists(siblingServerPath))
            return siblingServerPath;

        var outputDirectory = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var targetFramework = outputDirectory.Name;
        var configurationDirectory = outputDirectory.Parent?.Name;
        var repoRoot = outputDirectory.Parent?.Parent?.Parent?.Parent;

        if (configurationDirectory == null || repoRoot == null)
            return null;

        var repoServerPath = Path.Combine(repoRoot.FullName, "Rex.Server", "bin", configurationDirectory, targetFramework, "Rex.Server.dll");
        return File.Exists(repoServerPath) ? repoServerPath : null;
    }

    private static bool ShouldRender(CommandLineArgs args)
    {
        return !args.Headless;
    }
}
