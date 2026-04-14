using LiteNetLib.Utils;
using Rex.Sandbox.Shared.Net.Messages;
using Rex.Shared.Net;
using Rex.Shared.Net.Messages;

namespace Rex.Sandbox.Shared.Tests.Net;

// NetMessageRegistry dispatch after SandboxNetMessages.RegisterAll.
public sealed class SandboxMessageRegistryTests
{
    public SandboxMessageRegistryTests()
    {
        SandboxNetTestBootstrap.EnsureRegistered();
    }

    [Fact]
    public void Deserialize_ConnectRequest_round_trips()
    {
        var original = new ConnectRequestMessage(42, "tester");
        var writer = new NetDataWriter();
        original.Serialize(writer);

        var reader = new NetDataReader();
        reader.SetSource(writer.Data, 0, writer.Length);

        ConnectRequestMessage typed = Assert.IsType<ConnectRequestMessage>(NetMessageRegistry.Deserialize(reader));
        Assert.Equal(original.ProtocolVersion, typed.ProtocolVersion);
        Assert.Equal(original.PlayerName, typed.PlayerName);
    }

    [Fact]
    public void SandboxNetMessages_RegisterAll_twice_still_deserializes_ConnectRequest()
    {
        SandboxNetMessages.RegisterAll();
        SandboxNetMessages.RegisterAll();

        var original = new ConnectRequestMessage(7, "dup");
        var writer = new NetDataWriter();
        original.Serialize(writer);
        var reader = new NetDataReader();
        reader.SetSource(writer.Data, 0, writer.Length);

        ConnectRequestMessage decoded = Assert.IsType<ConnectRequestMessage>(NetMessageRegistry.Deserialize(reader));
        Assert.Equal(7, decoded.ProtocolVersion);
        Assert.Equal("dup", decoded.PlayerName);
    }

    [Fact]
    public void CoreAndSandboxMessages_share_registry_without_conflict()
    {
        DisconnectMessage original = new("bye");
        NetDataWriter writer = new();
        original.Serialize(writer);
        NetDataReader reader = new();
        reader.SetSource(writer.Data, 0, writer.Length);

        DisconnectMessage decoded = Assert.IsType<DisconnectMessage>(NetMessageRegistry.Deserialize(reader));
        Assert.Equal("bye", decoded.Reason);
    }

    [Fact]
    public void Deserialize_RequestFullState_round_trips()
    {
        RequestFullStateMessage original = new(123u);
        NetDataWriter writer = new();
        original.Serialize(writer);

        NetDataReader reader = new();
        reader.SetSource(writer.Data, 0, writer.Length);

        RequestFullStateMessage decoded = Assert.IsType<RequestFullStateMessage>(NetMessageRegistry.Deserialize(reader));
        Assert.Equal(123u, decoded.LastAppliedServerTick);
    }

    [Fact]
    public void StateAck_and_request_full_state_use_distinct_wire_ids()
    {
        Assert.NotEqual(StateAckMessage.Id, RequestFullStateMessage.Id);
    }

    [Fact]
    public void Deserialize_StateAck_after_sandbox_registration_still_returns_state_ack()
    {
        StateAckMessage original = new(321u);
        NetDataWriter writer = new();
        original.Serialize(writer);

        NetDataReader reader = new();
        reader.SetSource(writer.Data, 0, writer.Length);

        StateAckMessage decoded = Assert.IsType<StateAckMessage>(NetMessageRegistry.Deserialize(reader));
        Assert.Equal(321u, decoded.AcknowledgedTick);
    }
}
