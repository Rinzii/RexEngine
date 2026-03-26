using LiteNetLib;
using LiteNetLib.Utils;

namespace Rex.Shared.Net;

/// <summary>Picks channel and delivery for a message. Large entity snapshots use reliable ordered to avoid MTU drops.</summary>
public static class AdaptiveReliability
{
    /// <summary>Serialized size above this uses reliable ordered instead of sequenced snapshot delivery.</summary>
    public const int ReliableThreshold = 1200;

    /// <summary>Uses the group's defaults except for <see cref="MessageGroup.Entity"/>, where payload size is checked.</summary>
    public static (byte Channel, DeliveryMethod Delivery) GetAdaptiveDelivery(INetMessage message)
    {
        if (message.Group != MessageGroup.Entity)
            return message.Group.GetDeliveryInfo();

        // Measure serialized size without sending (same bytes as on the wire after the header).
        var writer = new NetDataWriter();
        message.Serialize(writer);

        return writer.Length > ReliableThreshold
            ? (DeliveryChannel.Reliable, DeliveryMethod.ReliableOrdered)
            : message.Group.GetDeliveryInfo();
    }
}
