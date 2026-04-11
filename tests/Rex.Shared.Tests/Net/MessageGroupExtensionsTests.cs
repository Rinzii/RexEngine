using LiteNetLib;
using Rex.Shared.Net;

namespace Rex.Shared.Tests.Net;

// Default channel and delivery for each MessageGroup.
public sealed class MessageGroupExtensionsTests
{
    public static TheoryData<MessageGroup, byte, DeliveryMethod> DeliveryCases => new()
    {
        { MessageGroup.Core, DeliveryChannel.Reliable, DeliveryMethod.ReliableOrdered },
        { MessageGroup.Entity, DeliveryChannel.Snapshot, DeliveryMethod.Sequenced },
        { MessageGroup.EntityEvent, DeliveryChannel.Reliable, DeliveryMethod.ReliableOrdered },
        { MessageGroup.Input, DeliveryChannel.Unreliable, DeliveryMethod.Unreliable },
        { MessageGroup.Command, DeliveryChannel.ReliableUnordered, DeliveryMethod.ReliableUnordered },
        { MessageGroup.Transfer, DeliveryChannel.Transfer, DeliveryMethod.ReliableOrdered },
    };

    [Theory]
    [MemberData(nameof(DeliveryCases))]
    // Each group maps to the expected LiteNetLib channel and method.
    public void GetDeliveryInfo_matches_fixed_channel_and_method(
        MessageGroup group,
        byte expectedChannel,
        DeliveryMethod expectedDelivery)
    {
        (byte channel, DeliveryMethod delivery) = group.GetDeliveryInfo();

        Assert.Equal(expectedChannel, channel);
        Assert.Equal(expectedDelivery, delivery);
    }
}
