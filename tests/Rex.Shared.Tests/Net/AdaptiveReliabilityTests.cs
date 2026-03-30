using LiteNetLib;
using LiteNetLib.Utils;
using Rex.Shared.Net;

namespace Rex.Shared.Tests.Net;

// Picks delivery based on group and serialized payload size.
public sealed class AdaptiveReliabilityTests
{
    // Fake Entity group message with a chosen serialized byte length.
    private sealed class PayloadEntityMessage(ushort messageId, int bodyBytes) : INetMessage
    {
        public ushort MessageId => messageId;

        public MessageGroup Group => MessageGroup.Entity;

        public void Serialize(NetDataWriter writer)
        {
            for (var i = 0; i < bodyBytes; i++)
            {
                writer.Put((byte)0xCD);
            }
        }
    }

    // Fake Core group message with a chosen serialized byte length.
    private sealed class PayloadCoreMessage(int bodyBytes) : INetMessage
    {
        public ushort MessageId => 0;

        public MessageGroup Group => MessageGroup.Core;

        public void Serialize(NetDataWriter writer)
        {
            for (var i = 0; i < bodyBytes; i++)
            {
                writer.Put((byte)0x01);
            }
        }
    }

    [Fact]
    // Small Entity group messages keep the default channel and method.
    public void Entity_small_payload_uses_group_defaults()
    {
        var message = new PayloadEntityMessage(0, 64);
        var expected = MessageGroup.Entity.GetDeliveryInfo();

        var actual = AdaptiveReliability.GetAdaptiveDelivery(message);

        Assert.Equal(expected.Channel, actual.Channel);
        Assert.Equal(expected.Delivery, actual.Delivery);
    }

    [Fact]
    // Large Entity payloads upgrade to reliable ordered on the reliable channel.
    public void Entity_large_payload_uses_reliable_ordered()
    {
        var message = new PayloadEntityMessage(0, AdaptiveReliability.ReliableThreshold + 128);

        var (channel, delivery) = AdaptiveReliability.GetAdaptiveDelivery(message);

        Assert.Equal(DeliveryChannel.Reliable, channel);
        Assert.Equal(DeliveryMethod.ReliableOrdered, delivery);
    }

    [Fact]
    // Non-entity groups never upgrade based on size.
    public void Non_entity_ignores_payload_size()
    {
        var message = new PayloadCoreMessage(AdaptiveReliability.ReliableThreshold + 500);
        var expected = MessageGroup.Core.GetDeliveryInfo();

        var actual = AdaptiveReliability.GetAdaptiveDelivery(message);

        Assert.Equal(expected.Channel, actual.Channel);
        Assert.Equal(expected.Delivery, actual.Delivery);
    }

    [Fact]
    // Serialized length equal to ReliableThreshold keeps Entity group defaults.
    public void Entity_payload_at_threshold_uses_group_defaults()
    {
        var message = new PayloadEntityMessage(0, AdaptiveReliability.ReliableThreshold);
        var expected = MessageGroup.Entity.GetDeliveryInfo();

        var actual = AdaptiveReliability.GetAdaptiveDelivery(message);

        Assert.Equal(expected.Channel, actual.Channel);
        Assert.Equal(expected.Delivery, actual.Delivery);
    }

    [Fact]
    // Serialized length ReliableThreshold plus one upgrades to reliable ordered.
    public void Entity_payload_one_byte_over_threshold_uses_reliable_ordered()
    {
        var message = new PayloadEntityMessage(0, AdaptiveReliability.ReliableThreshold + 1);

        var (channel, delivery) = AdaptiveReliability.GetAdaptiveDelivery(message);

        Assert.Equal(DeliveryChannel.Reliable, channel);
        Assert.Equal(DeliveryMethod.ReliableOrdered, delivery);
    }
}
