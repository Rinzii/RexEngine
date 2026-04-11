using Microsoft.Extensions.Logging.Abstractions;
using Rex.Shared.Net;

namespace Rex.Sandbox.Client.Tests;

public sealed class ClientAppModeTests
{
    [Fact]
    public void Standalone_mode_initializes_local_world()
    {
        using ClientApp app = new(NetMode.Standalone, NullLoggerFactory.Instance)
        {
            Headless = true
        };

        app.InitializeStandaloneForTesting();

        Assert.NotNull(app.World);
        Assert.Null(app.Client);
        Assert.NotEmpty(app.World.Entities);
    }

    [Fact]
    public void Listen_server_mode_stays_on_networked_client_path()
    {
        using ClientApp app = new(NetMode.ListenServer, NullLoggerFactory.Instance)
        {
            Headless = true
        };

        app.InitializeNetworkedForTesting();

        Assert.Null(app.World);
        Assert.NotNull(app.Client);
    }
}
