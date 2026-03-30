using Rex.Shared.Net;
using Rex.Shared.Net.Messages;

namespace Rex.Shared.Tests.Net;

// In-memory client and server channel pair for tests.
public sealed class LocalNetChannelPairTests
{
    [Fact]
    // Create wires server ClientId and marks the channel local.
    public void Create_pairs_queues_and_server_has_client_id()
    {
        var clientId = Guid.NewGuid();

        var (client, server) = LocalNetChannelPair.Create(clientId);

        Assert.Equal(clientId, server.ClientId);
        Assert.True(server.IsLocal);
    }

    [Fact]
    // Client Send reaches server DrainMessages with the same instance.
    public void Client_send_is_visible_to_server_DrainMessages()
    {
        var (client, server) = LocalNetChannelPair.Create(Guid.NewGuid());
        var sent = new ConnectRequestMessage(3, "player");
        client.Connect();
        client.Send(sent);

        INetMessage? received = null;
        server.DrainMessages(m => received = m);

        Assert.Same(sent, received);
    }

    [Fact]
    // Server Send raises client MessageReceived after PollEvents.
    public void Server_send_is_delivered_on_client_PollEvents()
    {
        var (client, server) = LocalNetChannelPair.Create(Guid.NewGuid());
        client.Connect();

        var outbound = new ConnectResponseMessage(true, Guid.NewGuid(), 60);
        INetMessage? received = null;
        client.MessageReceived += m => received = m;

        server.Send(outbound);
        client.PollEvents();

        Assert.Same(outbound, received);
    }

    [Fact]
    // Connect raises Connected and moves state to Connected.
    public void Client_Connect_raises_Connected_and_sets_state()
    {
        var (client, _) = LocalNetChannelPair.Create(Guid.NewGuid());
        var raised = false;
        client.Connected += () => raised = true;

        Assert.Equal(ConnectionState.Disconnected, client.State);
        client.Connect();

        Assert.True(raised);
        Assert.Equal(ConnectionState.Connected, client.State);
    }

    [Fact]
    // Disconnect raises Disconnected with the reason and resets state.
    public void Client_Disconnect_raises_Disconnected_and_resets_state()
    {
        var (client, _) = LocalNetChannelPair.Create(Guid.NewGuid());
        client.Connect();

        string? reason = null;
        client.Disconnected += r => reason = r;

        client.Disconnect("bye");

        Assert.Equal("bye", reason);
        Assert.Equal(ConnectionState.Disconnected, client.State);
    }
}
