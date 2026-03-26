using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;
using Rex.Shared.Net;

namespace Rex.Client;

/// <summary>Entry point: parses args, optional listen-server child process, then <see cref="ClientApp"/>.</summary>
internal static class Program
{
    private static void Main(string[] args)
    {
        if (!CommandLineArgs.TryParse(args, out var parsed))
            return;

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var logger = loggerFactory.CreateLogger("Rex.Client");

        if (parsed.Mode == NetMode.ListenServer)
            RunWithListenServer(parsed, loggerFactory, logger);
        else
            RunApp(parsed, loggerFactory, logger);
    }

    private static void RunApp(CommandLineArgs args, ILoggerFactory loggerFactory, ILogger logger)
    {
        using var app = new ClientApp(args.Mode, loggerFactory)
        {
            Headless = args.Headless
        };

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            logger.LogInformation("Shutdown signal received.");
            // ReSharper disable once AccessToDisposedClosure
            app.Stop();
        };

        // Parse host:port when connecting to a remote server.
        string? host = null;
        var port = args.Port;

        if (args.ConnectAddress != null)
        {
            host = args.ConnectAddress;
            var colonIndex = host.LastIndexOf(':');
            if (colonIndex > 0 && int.TryParse(host[(colonIndex + 1)..], out var parsedPort))
            {
                host = host[..colonIndex];
                port = parsedPort;
            }
        }

        app.Run(host, port);
    }

    private static void RunWithListenServer(CommandLineArgs args, ILoggerFactory loggerFactory, ILogger logger)
    {
        using var serverProcess = StartListenServerProcess(args.Port, logger);
        if (serverProcess == null)
            return;

        try
        {
            var clientArgs = new CommandLineArgs(
                args.Headless,
                NetMode.Client,
                "127.0.0.1",
                args.Port
            );

            RunApp(clientArgs, loggerFactory, logger);
        }
        finally
        {
            StopListenServerProcess(serverProcess, logger);
        }
    }

    private static Process? StartListenServerProcess(int port, ILogger logger)
    {
        var serverAssemblyPath = ResolveServerAssemblyPath();
        if (serverAssemblyPath == null)
        {
            logger.LogError("Could not find Rex.Server assembly for listen server mode.");
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
                readySignal.Set();
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                logger.LogError("[Server] {Message}", e.Data);
        };

        if (!process.Start())
        {
            logger.LogError("Failed to start Rex.Server process.");
            process.Dispose();
            return null;
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (readySignal.Wait(TimeSpan.FromSeconds(10)))
            return process;

        if (process.HasExited)
            logger.LogError("Listen server exited before startup completed.");
        else
            logger.LogError("Timed out waiting for listen server startup.");

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
        var siblingPath = Path.Combine(AppContext.BaseDirectory, "Rex.Server.dll");
        if (File.Exists(siblingPath))
            return siblingPath;

        var outputDir =
            new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar));
        var tfm = outputDir.Name;
        var config = outputDir.Parent?.Name;
        // bin/{Config}/{Tfm}/ then up to repo root for dev layout when DLL isn't copied next to client.
        var repoRoot = outputDir.Parent?.Parent?.Parent?.Parent;

        if (config == null || repoRoot == null)
            return null;

        var repoPath = Path.Combine(repoRoot.FullName, "Rex.Server", "bin", config, tfm, "Rex.Server.dll");
        return File.Exists(repoPath) ? repoPath : null;
    }
}