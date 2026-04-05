using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rex.Server.Net;
using Rex.Server.Runtime;
using Rex.Shared.Startup;

namespace Rex.Server.Startup;

/// <summary>Dedicated server entry in the engine.</summary>
public static class GameServerStart
{
    /// <summary>Parses argv, builds services and runs <see cref="ServerRuntimeHost"/> until shutdown.</summary>
    /// <param name="args">Raw command line arguments.</param>
    /// <param name="definition">Ports, names and defaults from the game.</param>
    /// <returns>0 after a normal exit. 1 when argument parsing fails or startup throws.</returns>
    public static int Start(string[] args, GameServerStartDefinition definition)
    {
        GameStartDefinitionValidator.Validate(definition);

        if (!ServerStartOptions.TryParse(args, definition, out var options, out var error))
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        using var loggerFactory = ConsoleStartupSupport.CreateLoggerFactory();
        var services = new ServiceCollection();
        services.AddSingleton(definition);
        services.AddSingleton(options);
        services.AddSingleton(loggerFactory);
        services.AddSingleton<ILogger>(sp =>
            sp.GetRequiredService<ILoggerFactory>().CreateLogger("Rex.Server.Startup"));
        services.AddSingleton(new ServerRuntimeOptions { TickRate = options.TickRate });
        services.AddSingleton<ServerRuntimeHost>();
        using var serviceProvider = services.BuildServiceProvider();

        var logger = serviceProvider.GetRequiredService<ILogger>();
        using var runtime = serviceProvider.GetRequiredService<ServerRuntimeHost>();
        using var cts = new CancellationTokenSource();
        using var shutdownHook = new ConsoleShutdownHook(cts, runtime.Stop);

        var engineAssemblyName = typeof(RemoteServerNetChannel).Assembly.GetName().Name ?? "Rex.Server";
        GameServerStartLog.ServerBootstrapStarting(logger, definition.Identity.GameName, definition.Identity.ServerProject);
        GameServerStartLog.EngineRuntimeAssembly(logger, engineAssemblyName);
        GameServerStartLog.ServerIdentity(logger, definition.DedicatedServerName);
        GameServerStartLog.ServerSettings(logger, options.Port, options.TickRate, options.MaxPlayers);
        Console.Out.WriteLine(definition.ReadyLine);

        try
        {
            runtime.Run(cts.Token);
            GameServerStartLog.ServerBootstrapCompleted(logger, definition.Identity.GameName, runtime.Clock.CurrentTick);
            return 0;
        }
        catch (Exception ex)
        {
            GameServerStartLog.StartupFailed(logger, ex);
            return 1;
        }
    }

}

internal static partial class GameServerStartLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "{Game} dedicated server bootstrap starting from {Project}.")]
    public static partial void ServerBootstrapStarting(ILogger logger, string game, string project);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "Authoritative game code lives outside the engine while engine server runtime code comes from {Assembly}.")]
    public static partial void EngineRuntimeAssembly(ILogger logger, string assembly);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information,
        Message = "Dedicated server identity: {ServerName}.")]
    public static partial void ServerIdentity(ILogger logger, string serverName);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information,
        Message = "Server settings: UDP {Port}, {TickRate} Hz, max players {MaxPlayers}.")]
    public static partial void ServerSettings(ILogger logger, int port, int tickRate, int maxPlayers);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information,
        Message = "{Game} server bootstrap completed at simulation tick {Tick}.")]
    public static partial void ServerBootstrapCompleted(ILogger logger, string game, uint tick);

    [LoggerMessage(EventId = 6, Level = LogLevel.Error,
        Message = "Server startup failed.")]
    public static partial void StartupFailed(ILogger logger, Exception ex);
}
