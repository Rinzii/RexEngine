using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;
using Rex.Client.Logging;
using Rex.Shared.Net;

namespace Rex.Client;

/// <summary>Parses command-line args and runs <see cref="ClientApp"/>. May start a listen-server child process.</summary>
internal static class Program
{
    private static void Main(string[] args)
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var logger = loggerFactory.CreateLogger("Rex.Client");

        if (!CommandLineArgs.TryParse(args, out var parsed, out var parseError))
        {
            logger.CliParseFailed(parseError ?? "Invalid arguments.");
            return;
        }

        foreach (var arg in parsed.UnrecognizedArguments)
        {
            logger.UnrecognizedCliArgument(arg);
        }

        if (parsed.Mode == NetMode.ListenServer)
        {
            RunWithListenServer(parsed, loggerFactory, logger);
        }
        else
        {
            RunApp(parsed, loggerFactory, logger);
        }
    }

    private static void RunApp(CommandLineArgs args, ILoggerFactory loggerFactory, ILogger logger)
    {
        using var app = new ClientApp(args.Mode, loggerFactory)
        {
            Headless = args.Headless
        };

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            logger.ShutdownSignalReceived();
            // ReSharper disable once AccessToDisposedClosure
            cts.Cancel();
            // ReSharper disable once AccessToDisposedClosure
            app.Stop();
        };

        string? host = null;
        var port = args.Port;

        if (args.ConnectAddress != null)
        {
            if (!ConnectEndpointParser.TryParse(args.ConnectAddress, args.Port, out var parsedHost, out var parsedPort))
            {
                logger.InvalidConnectAddress(args.ConnectAddress);
                return;
            }

            host = parsedHost;
            port = parsedPort;
        }

        app.Run(host, port, cts.Token);
    }

    private static void RunWithListenServer(CommandLineArgs args, ILoggerFactory loggerFactory, ILogger logger)
    {
        using var serverProcess = StartListenServerProcess(args.Port, logger);
        if (serverProcess == null)
        {
            return;
        }

        try
        {
            var clientArgs = new CommandLineArgs(
                args.Headless,
                NetMode.Client,
                "127.0.0.1",
                args.Port,
                Array.Empty<string>());

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
            logger.ListenServerAssemblyNotFound();
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
            {
                return;
            }

            logger.ListenServerOutput(e.Data);

            if (e.Data.Contains(ProtocolConstants.ListenProcessReadyLine, StringComparison.Ordinal))
            {
                readySignal.Set();
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                logger.ListenServerError(e.Data);
            }
        };

        if (!process.Start())
        {
            logger.ListenServerStartFailed();
            process.Dispose();
            return null;
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (readySignal.Wait(TimeSpan.FromSeconds(10)))
        {
            return process;
        }

        if (process.HasExited)
        {
            logger.ListenServerExitedEarly();
        }
        else
        {
            logger.ListenServerStartupTimeout();
        }

        StopListenServerProcess(process, logger);
        process.Dispose();
        return null;
    }

    private static void StopListenServerProcess(Process process, ILogger logger)
    {
        if (process.HasExited)
        {
            return;
        }

        logger.StoppingListenServer();
        process.Kill(true);
        process.WaitForExit(5000);
    }

    private static string? ResolveServerAssemblyPath()
    {
        var siblingPath = Path.Combine(AppContext.BaseDirectory, "Rex.Server.dll");
        if (File.Exists(siblingPath))
        {
            return siblingPath;
        }

        var outputDir =
            new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar));
        var tfm = outputDir.Name;
        var config = outputDir.Parent?.Name;

        // bin/{Config}/{Tfm}/ then up to repo root for dev layout when DLL isn't copied next to client.
        var repoRoot = outputDir.Parent?.Parent?.Parent?.Parent;

        if (config == null || repoRoot == null)
        {
            return null;
        }

        var repoPath = Path.Combine(repoRoot.FullName, "Rex.Server", "bin", config, tfm, "Rex.Server.dll");
        return File.Exists(repoPath) ? repoPath : null;
    }
}
