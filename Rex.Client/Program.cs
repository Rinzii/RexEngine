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
            if (Environment.GetEnvironmentVariable("REX_CLIENT_LOG_LEVEL") is { } logLevelStr &&
                Enum.TryParse<LogLevel>(logLevelStr, true, out var logLevel))
            {
                builder.SetMinimumLevel(logLevel);
            }
            else
            {
                builder.SetMinimumLevel(LogLevel.Warning);
            }
        });

        var logger = loggerFactory.CreateLogger("Rex.Client");

        if (!CommandLineArgs.TryParse(args, out var parsed, out var parseError))
        {
            logger.CliParseFailed(parseError);
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
            // ReSharper disable AccessToDisposedClosure
            cts.Cancel();
            app.Stop();
            // ReSharper restore AccessToDisposedClosure
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
                []);

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

        // Setup our signal to use a ManualResetEventSlim so we can wait for the server to signal it's ready
        // Then create the process.
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

        // Add our arguments to the process start info
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

            // The server signals it's ready by writing a specific line to stdout, so watch for that line to know when we can connect.
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

        // Wait for the server to signal it's ready before returning
        // Set a timeout of 10 seconds in case the server fails to start or signal
        if (readySignal.Wait(TimeSpan.FromSeconds(10)))
        {
            return process;
        }

        // If we timed out, check if the process has exited to provide a more specific error message
        if (process.HasExited)
        {
            logger.ListenServerExitedEarly();
        }
        else
        {
            logger.ListenServerStartupTimeout();
        }

        // Kill the process and dispose of it
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

        // Walk from bin/{Config}/{Tfm}/ back to the repo root in dev builds when the DLL is not copied next to the client.
        // TODO: IanP: This might change later as there is a high likely hood we will later change the output location of builds.
        var repoRoot = outputDir.Parent?.Parent?.Parent?.Parent;

        if (config == null || repoRoot == null)
        {
            return null;
        }

        var repoPath = Path.Combine(repoRoot.FullName, "Rex.Server", "bin", config, tfm, "Rex.Server.dll");
        return File.Exists(repoPath) ? repoPath : null;
    }
}
