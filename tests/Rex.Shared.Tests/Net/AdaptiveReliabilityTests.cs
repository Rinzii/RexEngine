using LiteNetLib;
using LiteNetLib.Utils;
using Rex.Shared.Net;

namespace Rex.Shared.Tests.Net;

public sealed class AdaptiveReliabilityTests
{
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
    public void Entity_small_payload_uses_group_defaults()
    {
        var message = new PayloadEntityMessage(0, 64);
        var expected = MessageGroup.Entity.GetDeliveryInfo();

        var actual = AdaptiveReliability.GetAdaptiveDelivery(message);

        Assert.Equal(expected.Channel, actual.Channel);
        Assert.Equal(expected.Delivery, actual.Delivery);
    }

    [Fact]
    public void Entity_large_payload_uses_reliable_ordered()
    {
        var message = new PayloadEntityMessage(0, AdaptiveReliability.ReliableThreshold + 128);

        var (channel, delivery) = AdaptiveReliability.GetAdaptiveDelivery(message);

        Assert.Equal(DeliveryChannel.Reliable, channel);
        Assert.Equal(DeliveryMethod.ReliableOrdered, delivery);
    }

    [Fact]
    public void Non_entity_ignores_payload_size()
    {
        var message = new PayloadCoreMessage(AdaptiveReliability.ReliableThreshold + 500);
        var expected = MessageGroup.Core.GetDeliveryInfo();

        var actual = AdaptiveReliability.GetAdaptiveDelivery(message);

        Assert.Equal(expected.Channel, actual.Channel);
        Assert.Equal(expected.Delivery, actual.Delivery);
    }
}
