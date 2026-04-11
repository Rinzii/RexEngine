using LiteNetLib;
using LiteNetLib.Utils;
using Rex.Shared.Net;

namespace Rex.Shared.Tests.Net;

// Picks delivery based on group and serialized payload size.
public sealed class AdaptiveReliabilityTests
{
    [Fact]
    // Small Entity group messages keep the default channel and method.
    public void Entity_small_payload_uses_group_defaults()
    {
        PayloadEntityMessage message = new(0, 64);
        (byte channel, DeliveryMethod delivery) = MessageGroup.Entity.GetDeliveryInfo();

        (byte actualChannel, DeliveryMethod actualDelivery) = AdaptiveReliability.GetAdaptiveDelivery(message);

        Assert.Equal(channel, actualChannel);
        Assert.Equal(delivery, actualDelivery);
    }

    [Fact]
    // Large Entity payloads upgrade to reliable ordered on the reliable channel.
    public void Entity_large_payload_uses_reliable_ordered()
    {
        PayloadEntityMessage message = new(0, AdaptiveReliability.ReliableThreshold + 128);

        (byte channel, DeliveryMethod delivery) = AdaptiveReliability.GetAdaptiveDelivery(message);

        Assert.Equal(DeliveryChannel.Reliable, channel);
        Assert.Equal(DeliveryMethod.ReliableOrdered, delivery);
    }

    [Fact]
    // Groups other than Entity never upgrade based on size.
    public void Non_entity_ignores_payload_size()
    {
        PayloadCoreMessage message = new(AdaptiveReliability.ReliableThreshold + 500);
        (byte channel, DeliveryMethod delivery) = MessageGroup.Core.GetDeliveryInfo();

        (byte actualChannel, DeliveryMethod actualDelivery) = AdaptiveReliability.GetAdaptiveDelivery(message);

        Assert.Equal(channel, actualChannel);
        Assert.Equal(delivery, actualDelivery);
    }

    [Fact]
    // Serialized length equal to ReliableThreshold keeps Entity group defaults.
    public void Entity_payload_at_threshold_uses_group_defaults()
    {
        PayloadEntityMessage message = new(0, AdaptiveReliability.ReliableThreshold);
        (byte channel, DeliveryMethod delivery) = MessageGroup.Entity.GetDeliveryInfo();

        (byte actualChannel, DeliveryMethod actualDelivery) = AdaptiveReliability.GetAdaptiveDelivery(message);

        Assert.Equal(channel, actualChannel);
        Assert.Equal(delivery, actualDelivery);
    }

    [Fact]
    // Serialized length ReliableThreshold plus one upgrades to reliable ordered.
    public void Entity_payload_one_byte_over_threshold_uses_reliable_ordered()
    {
        PayloadEntityMessage message = new(0, AdaptiveReliability.ReliableThreshold + 1);

        (byte channel, DeliveryMethod delivery) = AdaptiveReliability.GetAdaptiveDelivery(message);

        Assert.Equal(DeliveryChannel.Reliable, channel);
        Assert.Equal(DeliveryMethod.ReliableOrdered, delivery);
    }

    // Fake Entity group message with a chosen serialized byte length.
    private sealed class PayloadEntityMessage(ushort messageId, int bodyBytes) : INetMessage
    {
        public ushort MessageId => messageId;

        public MessageGroup Group => MessageGroup.Entity;

        public void Serialize(NetDataWriter writer)
        {
            for (int i = 0; i < bodyBytes; i++)
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
            for (int i = 0; i < bodyBytes; i++)
            {
                writer.Put((byte)0x01);
            }
        }
    }
}
