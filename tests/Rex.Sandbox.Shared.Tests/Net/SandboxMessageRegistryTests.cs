using LiteNetLib.Utils;
using Rex.Shared.Net;
using Rex.Shared.Net.Messages;
using Rex.Sandbox.Shared.Net.Messages;

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

        var typed = Assert.IsType<ConnectRequestMessage>(NetMessageRegistry.Deserialize(reader));
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

        var decoded = Assert.IsType<ConnectRequestMessage>(NetMessageRegistry.Deserialize(reader));
        Assert.Equal(7, decoded.ProtocolVersion);
        Assert.Equal("dup", decoded.PlayerName);
    }

    [Fact]
    public void CoreAndSandboxMessages_share_registry_without_conflict()
    {
        var original = new DisconnectMessage("bye");
        var writer = new NetDataWriter();
        original.Serialize(writer);
        var reader = new NetDataReader();
        reader.SetSource(writer.Data, 0, writer.Length);

        var decoded = Assert.IsType<DisconnectMessage>(NetMessageRegistry.Deserialize(reader));
        Assert.Equal("bye", decoded.Reason);
    }
}
