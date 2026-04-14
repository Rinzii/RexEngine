using LiteNetLib;
using LiteNetLib.Utils;
using Rex.Shared.Net;
using NetConnectionState = Rex.Shared.Net.ConnectionState;

namespace Rex.Shared.Tests.Net;

// In memory client and server channel pair for tests.
public sealed class LocalNetChannelPairTests
{
    [Fact]
    // Create wires server ClientId and marks the channel local.
    public void Create_pairs_queues_and_server_has_client_id()
    {
        var clientId = Guid.NewGuid();
        LocalServerNetChannel server = LocalNetChannelPair.Create(clientId).Server;

        Assert.Equal(clientId, server.ClientId);
        Assert.True(server.IsLocal);
    }

    [Fact]
    // Client Send reaches server DrainMessages with the same instance.
    public void Client_send_is_visible_to_server_DrainMessages()
    {
        (LocalClientNetChannel client, LocalServerNetChannel server) = LocalNetChannelPair.Create(Guid.NewGuid());
        TestMessage sent = new(3, "player");
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
        (LocalClientNetChannel client, LocalServerNetChannel server) = LocalNetChannelPair.Create(Guid.NewGuid());
        client.Connect();

        TestMessage outbound = new(60, "server");
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
        LocalClientNetChannel client = LocalNetChannelPair.Create(Guid.NewGuid()).Client;
        bool raised = false;
        client.Connected += () => raised = true;

        Assert.Equal(NetConnectionState.Disconnected, client.State);
        client.Connect();

        Assert.True(raised);
        Assert.Equal(NetConnectionState.Connected, client.State);
    }

    [Fact]
    // Disconnect raises Disconnected with the reason and resets state.
    public void Client_Disconnect_raises_Disconnected_and_resets_state()
    {
        LocalClientNetChannel client = LocalNetChannelPair.Create(Guid.NewGuid()).Client;
        client.Connect();

        string? reason = null;
        client.Disconnected += r => reason = r;

        client.Disconnect("bye");

        Assert.Equal("bye", reason);
        Assert.Equal(NetConnectionState.Disconnected, client.State);
    }

    [Fact]
    // Send with channel and delivery still reaches the server queue.
    public void Client_Send_with_channel_overload_enqueues_for_server()
    {
        (LocalClientNetChannel client, LocalServerNetChannel server) = LocalNetChannelPair.Create(Guid.NewGuid());
        TestMessage sent = new(1, "c");
        client.Connect();
        client.Send(sent, 3, DeliveryMethod.Unreliable);

        INetMessage? received = null;
        server.DrainMessages(m => received = m);

        Assert.Same(sent, received);
    }

    [Fact]
    // DrainMessages invokes the handler once per queued client message in order.
    public void Server_DrainMessages_delivers_multiple_client_sends_in_order()
    {
        (LocalClientNetChannel client, LocalServerNetChannel server) = LocalNetChannelPair.Create(Guid.NewGuid());
        TestMessage first = new(1, "a");
        TestMessage second = new(1, "b");
        client.Connect();
        client.Send(first);
        client.Send(second);

        List<INetMessage> received = [];
        server.DrainMessages(received.Add);

        Assert.Equal(2, received.Count);
        Assert.Same(first, received[0]);
        Assert.Same(second, received[1]);
    }

    [Fact]
    // PollEvents raises MessageReceived for each server message in order.
    public void Client_PollEvents_delivers_multiple_server_sends_in_order()
    {
        (LocalClientNetChannel client, LocalServerNetChannel server) = LocalNetChannelPair.Create(Guid.NewGuid());
        client.Connect();

        TestMessage first = new(1, "yes");
        TestMessage second = new(2, "no");
        List<INetMessage> received = [];
        client.MessageReceived += received.Add;

        server.Send(first);
        server.Send(second, 0, DeliveryMethod.ReliableOrdered);
        client.PollEvents();

        Assert.Equal(2, received.Count);
        Assert.Same(first, received[0]);
        Assert.Same(second, received[1]);
    }

    private sealed class TestMessage : INetMessage
    {
        public TestMessage(int sequence, string payload)
        {
            Sequence = sequence;
            Payload = payload;
        }

        public int Sequence { get; }
        public string Payload { get; }
        public ushort MessageId => 999;
        public MessageGroup Group => MessageGroup.Command;

        public void Serialize(NetDataWriter writer)
        {
            throw new NotSupportedException("LocalNetChannelPair tests pass message instances directly.");
        }
    }
}
