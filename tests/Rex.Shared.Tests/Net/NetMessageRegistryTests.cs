using LiteNetLib.Utils;
using Rex.Shared.Net;
using Rex.Shared.Net.Messages;

namespace Rex.Shared.Tests.Net;

// NetMessageRegistry dispatch after CoreNetMessages.RegisterAll.
public sealed class NetMessageRegistryTests
{
    public NetMessageRegistryTests()
    {
        NetTestBootstrap.EnsureRegistered();
    }

    [Fact]
    // Serialize then Deserialize returns the same DisconnectMessage fields.
    public void Deserialize_DisconnectMessage_round_trips()
    {
        var original = new DisconnectMessage("tester");
        var writer = new NetDataWriter();
        original.Serialize(writer);

        var reader = new NetDataReader();
        reader.SetSource(writer.Data, 0, writer.Length);

        INetMessage decoded = NetMessageRegistry.Deserialize(reader);
        DisconnectMessage typed = Assert.IsType<DisconnectMessage>(decoded);

        Assert.Equal(original.Reason, typed.Reason);
    }

    [Fact]
    // Unknown id throws and mentions the id in the message.
    public void Deserialize_unknown_message_id_throws()
    {
        const ushort UnknownId = 60000;
        var writer = new NetDataWriter();
        writer.Put(UnknownId);
        writer.Put((byte)0);

        var reader = new NetDataReader();
        reader.SetSource(writer.Data, 0, writer.Length);

        InvalidOperationException ex =
            Assert.Throws<InvalidOperationException>(() => NetMessageRegistry.Deserialize(reader));

        Assert.Contains("60000", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    // Duplicate registrations fail fast so wire id collisions cannot silently override deserializers.
    public void Register_same_id_throws()
    {
        ushort customId = 59997;
        NetMessageRegistry.Register(customId, _ => new DisconnectMessage("first"));

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            NetMessageRegistry.Register(customId, _ => new DisconnectMessage("second")));

        Assert.Contains(customId.ToString(), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    // CoreNetMessages.RegisterAll stays safe when invoked again.
    public void CoreNetMessages_RegisterAll_twice_still_deserializes_DisconnectMessage()
    {
        CoreNetMessages.RegisterAll();
        CoreNetMessages.RegisterAll();

        var original = new DisconnectMessage("dup");
        var writer = new NetDataWriter();
        original.Serialize(writer);
        var reader = new NetDataReader();
        reader.SetSource(writer.Data, 0, writer.Length);

        DisconnectMessage decoded = Assert.IsType<DisconnectMessage>(NetMessageRegistry.Deserialize(reader));

        Assert.Equal("dup", decoded.Reason);
    }
}
