namespace Rex.Shared.Startup;

/// <summary>
/// Validates startup definitions before the engine composes services.
/// </summary>
public static class GameStartDefinitionValidator
{
    /// <summary>
    /// Validates a client startup definition.
    /// </summary>
    public static void Validate(GameClientStartDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ValidateIdentity(definition.Identity);
        ValidateNonEmpty(definition.DefaultHost, nameof(definition.DefaultHost));
        ValidatePort(definition.DefaultPort, nameof(definition.DefaultPort));
        ValidatePositive(definition.TickRate, nameof(definition.TickRate));
        ValidateWindow(definition.Window);
        ValidateListenServer(definition.ListenServer);
    }

    /// <summary>
    /// Validates a server startup definition.
    /// </summary>
    public static void Validate(GameServerStartDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ValidateIdentity(definition.Identity);
        ValidateNonEmpty(definition.DedicatedServerName, nameof(definition.DedicatedServerName));
        ValidateNonEmpty(definition.ReadyLine, nameof(definition.ReadyLine));
        ValidatePort(definition.DefaultPort, nameof(definition.DefaultPort));
        ValidatePositive(definition.TickRate, nameof(definition.TickRate));
        ValidatePositive(definition.MaxPlayers, nameof(definition.MaxPlayers));
    }

    private static void ValidateIdentity(GameRuntimeIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ValidateNonEmpty(identity.GameName, nameof(identity.GameName));
        ValidateNonEmpty(identity.SharedProject, nameof(identity.SharedProject));
        ValidateNonEmpty(identity.ClientProject, nameof(identity.ClientProject));
        ValidateNonEmpty(identity.ServerProject, nameof(identity.ServerProject));
    }

    private static void ValidateWindow(GameWindowDefinition window)
    {
        ArgumentNullException.ThrowIfNull(window);
        ValidateNonEmpty(window.Title, nameof(window.Title));
        ValidatePositive(window.Width, nameof(window.Width));
        ValidatePositive(window.Height, nameof(window.Height));
    }

    private static void ValidateListenServer(ListenServerDefinition listenServer)
    {
        ArgumentNullException.ThrowIfNull(listenServer);
        ValidateNonEmpty(listenServer.ServerAssemblyEnvironmentVariable, nameof(listenServer.ServerAssemblyEnvironmentVariable));
        ValidateNonEmpty(listenServer.ServerAssemblyFileName, nameof(listenServer.ServerAssemblyFileName));
        ValidateNonEmpty(listenServer.ReadyLine, nameof(listenServer.ReadyLine));
        ValidateNonEmpty(listenServer.LocalHost, nameof(listenServer.LocalHost));
        ValidatePositive(listenServer.StartupTimeoutSeconds, nameof(listenServer.StartupTimeoutSeconds));
    }

    private static void ValidateNonEmpty(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be null or whitespace.", paramName);
        }
    }

    private static void ValidatePositive(int value, string paramName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "Value must be greater than zero.");
        }
    }

    private static void ValidatePort(int value, string paramName)
    {
        if (value is <= 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "Port must be between 1 and 65535.");
        }
    }
}
