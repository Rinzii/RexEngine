using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rex.Client.Graphics;
using Rex.Client.Net;
using Rex.Client.Runtime;
using Rex.Shared.Net;
using Rex.Shared.Startup;

namespace Rex.Client.Startup;

/// <summary>
/// Engine entry for game client startup.
/// </summary>
public static class GameClientStart
{
    /// <summary>
    /// Parses command line arguments, wires dependency injection and blocks inside <see cref="ClientRuntimeHost"/> until shutdown.
    /// </summary>
    /// <param name="args">Raw command line arguments.</param>
    /// <param name="definition">Window, networking and identity defaults from the game.</param>
    /// <returns>0 after a normal exit. 1 when argument parsing fails or startup throws.</returns>
    public static int Start(string[] args, GameClientStartDefinition definition)
    {
        GameStartDefinitionValidator.Validate(definition);

        if (!ClientStartOptions.TryParse(args, definition, out ClientStartOptions options, out string? error))
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        using ILoggerFactory loggerFactory = ConsoleStartupSupport.CreateLoggerFactory();
        var services = new ServiceCollection();
        _ = services.AddSingleton(definition);
        _ = services.AddSingleton(options);
        _ = services.AddSingleton(loggerFactory);
        _ = services.AddSingleton(sp =>
            sp.GetRequiredService<ILoggerFactory>().CreateLogger("Rex.Client.Startup"));
        _ = services.AddSingleton(new ClientRuntimeOptions
        {
            TickRate = definition.TickRate,
            Headless = options.Headless,
            WindowTitle = definition.Window.Title,
            WindowWidth = definition.Window.Width,
            WindowHeight = definition.Window.Height
        });
        // Window backend is registered in DI so ClientRuntimeHost depends only on IGameWindow.
        _ = services.AddSingleton<IGameWindow, WindowCreator>();
        _ = services.AddSingleton<ClientRuntimeHost>();
        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        ILogger logger = serviceProvider.GetRequiredService<ILogger>();
        using ClientRuntimeHost runtime = serviceProvider.GetRequiredService<ClientRuntimeHost>();
        runtime.Window = serviceProvider.GetService<IGameWindow>();
        using var cts = new CancellationTokenSource();
        using var shutdownHook = new ConsoleShutdownHook(cts, runtime.Stop);

        ReadyChildProcess? listenServer = null;
        try
        {
            if (options.Mode == NetMode.ListenServer)
            {
                listenServer = StartListenServerProcess(definition, options.Port, logger);
                if (listenServer == null)
                {
                    return 1;
                }
            }

            string engineAssemblyName = typeof(RemoteClientNetChannel).Assembly.GetName().Name ?? "Rex.Client";
            GameClientStartLog.ClientBootstrapStarting(logger, definition.Identity.GameName, options.Mode,
                definition.Identity.ClientProject);
            GameClientStartLog.EngineRuntimeAssembly(logger, engineAssemblyName);
            GameClientStartLog.SharedRuntimeDefaults(logger, definition.Identity.SharedProject, definition.TickRate,
                options.Port);

            if (options.Mode == NetMode.Standalone)
            {
                GameClientStartLog.StandaloneSelected(logger);
            }
            else if (TryResolveRemoteEndpoint(options, definition, logger, out string host, out int port))
            {
                GameClientStartLog.RemoteEndpoint(logger, host, port, ProtocolConstants.ConnectionKey);
            }
            else
            {
                return 1;
            }

            if (options.Headless)
            {
                GameClientStartLog.HeadlessEnabled(logger);
            }

            try
            {
                runtime.Run(cts.Token);
                GameClientStartLog.ClientBootstrapCompleted(logger, definition.Identity.GameName,
                    runtime.Clock.CurrentTick);
                return 0;
            }
            catch (Exception ex)
            {
                GameClientStartLog.StartupFailed(logger, ex);
                return 1;
            }
        }
        finally
        {
            if (listenServer != null)
            {
                StopListenServerProcess(listenServer.Process, logger);
                listenServer.Dispose();
            }
        }
    }

    private static bool TryResolveRemoteEndpoint(
        ClientStartOptions options,
        GameClientStartDefinition definition,
        ILogger logger,
        out string host,
        out int port)
    {
        if (options.Mode == NetMode.ListenServer)
        {
            host = definition.ListenServer.LocalHost;
            port = options.Port;
            return true;
        }

        if (ConnectEndpointParser.TryParse(options.ConnectAddress, options.Port, out host, out port))
        {
            return true;
        }

        GameClientStartLog.InvalidConnectAddress(logger, options.ConnectAddress);
        return false;
    }

    private static ReadyChildProcess? StartListenServerProcess(
        GameClientStartDefinition definition,
        int port,
        ILogger logger)
    {
        string? serverAssemblyPath = RuntimeAssemblyLocator.ResolveServerAssemblyPath(
            definition.ListenServer.ServerAssemblyEnvironmentVariable,
            definition.ListenServer.ServerAssemblyFileName,
            definition.Identity.ClientProject,
            definition.Identity.ServerProject);
        if (serverAssemblyPath == null)
        {
            GameClientStartLog.ListenServerAssemblyNotFound(logger,
                definition.ListenServer.ServerAssemblyEnvironmentVariable);
            return null;
        }

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(serverAssemblyPath)!
        };
        process.EnableRaisingEvents = true;

        process.StartInfo.ArgumentList.Add(serverAssemblyPath);
        process.StartInfo.ArgumentList.Add("--port");
        process.StartInfo.ArgumentList.Add(port.ToString());

        ReadyChildProcess? bridge = null;
        try
        {
            bridge = new ReadyChildProcess(process, logger, definition.ListenServer.ReadyLine);
            if (!process.Start())
            {
                GameClientStartLog.ListenServerStartFailed(logger, definition.Identity.ServerProject);
                bridge.Dispose();
                return null;
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (bridge.WaitUntilReady(TimeSpan.FromSeconds(definition.ListenServer.StartupTimeoutSeconds)))
            {
                return bridge;
            }

            if (process.HasExited)
            {
                GameClientStartLog.ListenServerExitedEarly(logger);
            }
            else
            {
                GameClientStartLog.ListenServerStartupTimeout(logger);
            }

            StopListenServerProcess(process, logger);
            bridge.Dispose();
            return null;
        }
        catch
        {
            bridge?.Dispose();
            throw;
        }
    }

    private static void StopListenServerProcess(Process process, ILogger logger)
    {
        if (process.HasExited)
        {
            return;
        }

        GameClientStartLog.StoppingListenServer(logger);
        process.Kill(true);
        // TODO: Should we handle the result of our waiting?
        _ = process.WaitForExit(5000);
    }
}

internal static partial class GameClientStartLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "{Game} client bootstrap starting in {Mode} mode from {Project}.")]
    public static partial void ClientBootstrapStarting(ILogger logger, string game, NetMode mode, string project);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "Game code lives outside the engine while engine runtime code is supplied by {Assembly}.")]
    public static partial void EngineRuntimeAssembly(ILogger logger, string assembly);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information,
        Message = "Shared game contracts come from {SharedProject}; default runtime is {TickRate} Hz on port {Port}.")]
    public static partial void SharedRuntimeDefaults(ILogger logger, string sharedProject, int tickRate, int port);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information,
        Message = "Configured remote endpoint: {Host}:{Port} using engine connection key {ConnectionKey}.")]
    public static partial void RemoteEndpoint(ILogger logger, string host, int port, string connectionKey);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information,
        Message = "Standalone bootstrap selected; no remote connection is required.")]
    public static partial void StandaloneSelected(ILogger logger);

    [LoggerMessage(EventId = 6, Level = LogLevel.Information,
        Message = "Headless client mode is enabled for the startup pipeline.")]
    public static partial void HeadlessEnabled(ILogger logger);

    [LoggerMessage(EventId = 7, Level = LogLevel.Information,
        Message = "{Game} client bootstrap completed at simulation tick {Tick}.")]
    public static partial void ClientBootstrapCompleted(ILogger logger, string game, uint tick);

    [LoggerMessage(EventId = 8, Level = LogLevel.Error,
        Message =
            "Invalid connect address \"{ConnectAddress}\". Use host, host:port, or bracketed IPv6 such as [::1]:port.")]
    public static partial void InvalidConnectAddress(ILogger logger, string connectAddress);

    [LoggerMessage(EventId = 9, Level = LogLevel.Error,
        Message =
            "Could not find server assembly for listen server mode. Set {EnvironmentVariable} to override the default lookup.")]
    public static partial void ListenServerAssemblyNotFound(ILogger logger, string environmentVariable);

    [LoggerMessage(EventId = 10, Level = LogLevel.Error,
        Message = "Failed to start the local {ServerProject} process.")]
    public static partial void ListenServerStartFailed(ILogger logger, string serverProject);

    [LoggerMessage(EventId = 11, Level = LogLevel.Error,
        Message = "Listen server exited before startup completed.")]
    public static partial void ListenServerExitedEarly(ILogger logger);

    [LoggerMessage(EventId = 12, Level = LogLevel.Error,
        Message = "Timed out waiting for listen server startup.")]
    public static partial void ListenServerStartupTimeout(ILogger logger);

    [LoggerMessage(EventId = 13, Level = LogLevel.Information,
        Message = "Stopping listen server process.")]
    public static partial void StoppingListenServer(ILogger logger);

    [LoggerMessage(EventId = 14, Level = LogLevel.Error,
        Message = "Client startup failed.")]
    public static partial void StartupFailed(ILogger logger, Exception ex);
}
