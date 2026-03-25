using LiteNetLib;
using LiteNetLib.Utils;
using Rex.Shared.Net;

namespace Rex.Server.Net;

/// <summary>
/// Chooses a safer delivery mode for large snapshot packets.
/// </summary>
public static class AdaptiveReliability
{
    /// <summary>
    /// Approximate MTU budget after headers.
    /// Bigger snapshot payloads switch to reliable delivery.
    /// </summary>
    // ReSharper disable once MemberCanBePrivate.Global
    public const int ReliableThreshold = 1200;

    /// <summary>
    /// Returns the channel and delivery mode for a message.
    /// Snapshot traffic above <see cref="ReliableThreshold"/> bytes moves to reliable ordered delivery.
    /// </summary>
    public static (byte Channel, DeliveryMethod Delivery) GetAdaptiveDelivery(INetMessage message)
    {
        if (message.Group != MessageGroup.Entity)
            return message.Group.GetDeliveryInfo();

        var writer = new NetDataWriter();
        message.Serialize(writer);

        return writer.Length > ReliableThreshold
            ? (DeliveryChannel.Reliable, DeliveryMethod.ReliableOrdered)
            : message.Group.GetDeliveryInfo();
    }
}
