using LiteNetLib;
using LiteNetLib.Utils;
using Rex.Shared.Net;
using Rex.Shared.Net.Messages;
using Rex.Shared.Net.Transfer;
using Rex.Shared.Tests.Net;

namespace Rex.Shared.Tests.Regression;

// Critical wire and in-memory transport invariants.
public sealed class NetRegressionTests
{
    public NetRegressionTests()
    {
        NetTestBootstrap.EnsureRegistered();
    }

    [Fact]
    public void Regression_connect_address_embedded_port_overrides_default()
    {
        var ok = ConnectEndpointParser.TryParse("192.168.0.2:28001", 27015, out var host, out var port);

        Assert.True(ok);
        Assert.Equal("192.168.0.2", host);
        Assert.Equal(28001, port);
    }

    [Fact]
    public void Regression_message_group_entity_uses_snapshot_sequenced()
    {
        var (channel, delivery) = MessageGroup.Entity.GetDeliveryInfo();

        Assert.Equal(DeliveryChannel.Snapshot, channel);
        Assert.Equal(DeliveryMethod.Sequenced, delivery);
    }

    [Fact]
    public void Regression_net_guid_round_trip_non_empty()
    {
        var value = Guid.Parse("a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11");
        var writer = new NetDataWriter();
        writer.PutGuid(value);

        var reader = new NetDataReader();
        reader.SetSource(writer.Data, 0, writer.Length);

        Assert.Equal(value, reader.ReadGuid());
    }

    [Fact]
    public void Regression_disconnect_message_registry_round_trip()
    {
        var original = new DisconnectMessage("reason");
        var decoded = RoundTrip(original);
        var typed = Assert.IsType<DisconnectMessage>(decoded);
        Assert.Equal(original.Reason, typed.Reason);
    }

    [Fact]
    public void Regression_adaptive_reliability_entity_at_threshold_stays_group_defaults()
    {
        var message = new PayloadEntityMessage(AdaptiveReliability.ReliableThreshold);
        var expected = MessageGroup.Entity.GetDeliveryInfo();
        var actual = AdaptiveReliability.GetAdaptiveDelivery(message);

        Assert.Equal(expected.Channel, actual.Channel);
        Assert.Equal(expected.Delivery, actual.Delivery);
    }

    [Fact]
    public void Regression_compression_below_threshold_returns_same_buffer()
    {
        var data = new byte[NetCompression.CompressionThreshold - 1];
        Array.Fill(data, (byte)3);

        var (outData, isCompressed) = NetCompression.Compress(data);

        Assert.False(isCompressed);
        Assert.Same(data, outData);
    }

    [Fact]
    public void Regression_local_channel_pair_create_exposes_client_id_on_server()
    {
        var id = Guid.NewGuid();
        var (_, server) = LocalNetChannelPair.Create(id);

        Assert.Equal(id, server.ClientId);
    }

    [Fact]
    public void Regression_net_statistics_sent_totals_match_records()
    {
        var stats = new RexNetStatistics();
        stats.RecordSent(1, 10);
        stats.RecordSent(1, 20);

        Assert.Equal(30, stats.BytesSent);
        Assert.Equal(2, stats.MessagesSent);
    }

    private static INetMessage RoundTrip(INetMessage original)
    {
        var writer = new NetDataWriter();
        original.Serialize(writer);
        var reader = new NetDataReader();
        reader.SetSource(writer.Data, 0, writer.Length);
        return NetMessageRegistry.Deserialize(reader);
    }

    private sealed class PayloadEntityMessage(int bodyBytes) : INetMessage
    {
        public ushort MessageId => 0;

        public MessageGroup Group => MessageGroup.Entity;

        public void Serialize(NetDataWriter writer)
        {
            for (var i = 0; i < bodyBytes; i++)
            {
                writer.Put((byte)0xCD);
            }
        }
    }
}
