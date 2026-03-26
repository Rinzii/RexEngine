using LiteNetLib;

namespace Rex.Shared.Net;

/// <summary>
/// Groups messages by traffic pattern.
/// </summary>
public enum MessageGroup
{
    /// <summary>
    /// Connection flow and handshake traffic.
    /// </summary>
    Core,

    /// <summary>
    /// World state snapshots sent at tick rate.
    /// </summary>
    Entity,

    /// <summary>
    /// Reliable entity events such as spawns and destroys.
    /// </summary>
    EntityEvent,

    /// <summary>
    /// High frequency player input traffic.
    /// </summary>
    Input,

    /// <summary>
    /// Reliable messages where ordering does not matter.
    /// </summary>
    Command,

    /// <summary>
    /// Large transfers moved onto their own lane.
    /// </summary>
    Transfer
}

/// <summary>
/// Resolves the default LiteNetLib settings for each <see cref="MessageGroup"/>.
/// </summary>
public static class MessageGroupExtensions
{
    /// <summary>
    /// Returns the default channel and delivery mode for the group.
    /// </summary>
    public static (byte Channel, DeliveryMethod Delivery) GetDeliveryInfo(this MessageGroup group)
    {
        return group switch
        {
            MessageGroup.Core => (DeliveryChannel.Reliable, DeliveryMethod.ReliableOrdered),
            MessageGroup.Entity => (DeliveryChannel.Snapshot, DeliveryMethod.Sequenced),
            MessageGroup.EntityEvent => (DeliveryChannel.Reliable, DeliveryMethod.ReliableOrdered),
            MessageGroup.Input => (DeliveryChannel.Unreliable, DeliveryMethod.Unreliable),
            MessageGroup.Command => (DeliveryChannel.ReliableUnordered, DeliveryMethod.ReliableUnordered),
            MessageGroup.Transfer => (DeliveryChannel.Transfer, DeliveryMethod.ReliableOrdered),
            _ => (DeliveryChannel.Reliable, DeliveryMethod.ReliableOrdered)
        };
    }
}
