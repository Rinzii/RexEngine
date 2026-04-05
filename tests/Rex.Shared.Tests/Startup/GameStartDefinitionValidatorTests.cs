using Rex.Shared.Startup;

namespace Rex.Shared.Tests.Startup;

public sealed class GameStartDefinitionValidatorTests
{
    [Fact]
    public void Validate_client_definition_accepts_valid_values()
    {
        var definition = new GameClientStartDefinition(
            new GameRuntimeIdentity("TestGame", "Test.Shared", "Test.Client", "Test.Server"),
            "127.0.0.1",
            27015,
            60,
            new GameWindowDefinition("Test Window", 1280, 720),
            new ListenServerDefinition("TEST_SERVER_DLL", "Test.Server.dll", "TEST_READY"));

        GameStartDefinitionValidator.Validate(definition);
    }

    [Fact]
    public void Validate_client_definition_rejects_invalid_port()
    {
        var definition = new GameClientStartDefinition(
            new GameRuntimeIdentity("TestGame", "Test.Shared", "Test.Client", "Test.Server"),
            "127.0.0.1",
            70000,
            60,
            new GameWindowDefinition("Test Window", 1280, 720),
            new ListenServerDefinition("TEST_SERVER_DLL", "Test.Server.dll", "TEST_READY"));

        Assert.Throws<ArgumentOutOfRangeException>(() => GameStartDefinitionValidator.Validate(definition));
    }

    [Fact]
    public void Validate_server_definition_rejects_non_positive_max_players()
    {
        var definition = new GameServerStartDefinition(
            new GameRuntimeIdentity("TestGame", "Test.Shared", "Test.Client", "Test.Server"),
            "Test Dedicated Server",
            "TEST_READY",
            27015,
            60,
            0);

        Assert.Throws<ArgumentOutOfRangeException>(() => GameStartDefinitionValidator.Validate(definition));
    }
}
