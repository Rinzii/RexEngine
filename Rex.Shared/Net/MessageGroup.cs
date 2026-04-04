using LiteNetLib;

namespace Rex.Shared.Net;

/// <summary>
/// Groups messages by transport behavior so consumers can share delivery defaults without sharing gameplay policy.
/// </summary>
public enum MessageGroup
{
    /// <summary>
    /// Session setup, capability exchange, and other control-plane traffic.
    /// </summary>
    Core,

    /// <summary>
    /// High-rate state replication traffic such as snapshots or delta state.
    /// </summary>
    Entity,

    /// <summary>
    /// Reliable state lifecycle events such as create/remove notifications.
    /// </summary>
    EntityEvent,

    /// <summary>
    /// High-frequency command streams such as input or control updates.
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
