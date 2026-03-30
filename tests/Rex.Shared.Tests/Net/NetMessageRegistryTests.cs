using LiteNetLib.Utils;
using Rex.Shared.Net;
using Rex.Shared.Net.Messages;

namespace Rex.Shared.Tests.Net;

// NetMessageRegistry dispatch after NetMessages.RegisterAll.
public sealed class NetMessageRegistryTests
{
    public NetMessageRegistryTests()
    {
        NetTestBootstrap.EnsureRegistered();
    }

    [Fact]
    // Serialize then Deserialize returns the same ConnectRequest fields.
    public void Deserialize_ConnectRequest_round_trips()
    {
        var original = new ConnectRequestMessage(42, "tester");
        var writer = new NetDataWriter();
        original.Serialize(writer);

        var reader = new NetDataReader();
        reader.SetSource(writer.Data, 0, writer.Length);

        var decoded = NetMessageRegistry.Deserialize(reader);
        var typed = Assert.IsType<ConnectRequestMessage>(decoded);

        Assert.Equal(original.ProtocolVersion, typed.ProtocolVersion);
        Assert.Equal(original.PlayerName, typed.PlayerName);
    }

    [Fact]
    // Unknown id throws and mentions the id in the message.
    public void Deserialize_unknown_message_id_throws()
    {
        const ushort unknownId = 60000;
        var writer = new NetDataWriter();
        writer.Put(unknownId);
        writer.Put((byte)0);

        var reader = new NetDataReader();
        reader.SetSource(writer.Data, 0, writer.Length);

        var ex = Assert.Throws<InvalidOperationException>(() => NetMessageRegistry.Deserialize(reader));

        Assert.Contains("60000", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    // Second Register for the same message id wins on Deserialize.
    public void Register_same_id_replaces_deserializer()
    {
        ushort customId = 59997;
        NetMessageRegistry.Register(customId, _ => new DisconnectMessage("first"));
        NetMessageRegistry.Register(customId, reader =>
        {
            _ = reader.GetString();
            return new DisconnectMessage("replaced");
        });

        var writer = new NetDataWriter();
        NetMessageRegistry.WriteHeader(writer, customId);
        writer.Put(string.Empty);

        var reader = new NetDataReader();
        reader.SetSource(writer.Data, 0, writer.Length);

        var decoded = NetMessageRegistry.Deserialize(reader);
        var typed = Assert.IsType<DisconnectMessage>(decoded);

        Assert.Equal("replaced", typed.Reason);
    }

    [Fact]
    // NetMessages.RegisterAll stays safe when invoked again.
    public void NetMessages_RegisterAll_twice_still_deserializes_ConnectRequest()
    {
        NetMessages.RegisterAll();
        NetMessages.RegisterAll();

        var original = new ConnectRequestMessage(7, "dup");
        var writer = new NetDataWriter();
        original.Serialize(writer);
        var reader = new NetDataReader();
        reader.SetSource(writer.Data, 0, writer.Length);

        var decoded = Assert.IsType<ConnectRequestMessage>(NetMessageRegistry.Deserialize(reader));

        Assert.Equal(7, decoded.ProtocolVersion);
        Assert.Equal("dup", decoded.PlayerName);
    }
}
