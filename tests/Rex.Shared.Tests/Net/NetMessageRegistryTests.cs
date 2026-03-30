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
}
