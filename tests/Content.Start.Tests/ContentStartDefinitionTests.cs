using Content.Shared;
using Rex.Shared.Startup;

namespace Content.Start.Tests;

public sealed class ContentStartDefinitionTests
{
    [Fact]
    public void CreateClientStartDefinition_maps_content_metadata_and_defaults()
    {
        GameClientStartDefinition definition = ContentGameInfo.CreateClientStartDefinition();

        Assert.Equal(ContentGameInfo.GameName, definition.Identity.GameName);
        Assert.Equal(ContentGameInfo.SharedProject, definition.Identity.SharedProject);
        Assert.Equal(ContentGameInfo.ClientProject, definition.Identity.ClientProject);
        Assert.Equal(ContentGameInfo.ServerProject, definition.Identity.ServerProject);
        Assert.Equal(ContentGameInfo.DefaultHost, definition.DefaultHost);
        Assert.Equal(ContentGameInfo.DefaultWindowTitle, definition.Window.Title);
        Assert.Equal(ContentGameInfo.DefaultWindowWidth, definition.Window.Width);
        Assert.Equal(ContentGameInfo.DefaultWindowHeight, definition.Window.Height);
        Assert.Equal("REX_CONTENT_SERVER_DLL", definition.ListenServer.ServerAssemblyEnvironmentVariable);
        Assert.Equal("Content.Server.dll", definition.ListenServer.ServerAssemblyFileName);
        Assert.Equal(ContentGameInfo.ListenServerReadyLine, definition.ListenServer.ReadyLine);
    }

    [Fact]
    public void CreateServerStartDefinition_maps_content_server_identity()
    {
        GameServerStartDefinition definition = ContentGameInfo.CreateServerStartDefinition();
        ContentSessionSettings defaults = ContentGameInfo.CreateDefaultSessionSettings();

        Assert.Equal(ContentGameInfo.GameName, definition.Identity.GameName);
        Assert.Equal(ContentGameInfo.ServerProject, definition.Identity.ServerProject);
        Assert.Equal(ContentGameInfo.DedicatedServerName, definition.DedicatedServerName);
        Assert.Equal(ContentGameInfo.ListenServerReadyLine, definition.ReadyLine);
        Assert.Equal(defaults.Port, definition.DefaultPort);
        Assert.Equal(defaults.TickRate, definition.TickRate);
        Assert.Equal(defaults.MaxPlayers, definition.MaxPlayers);
    }
}
