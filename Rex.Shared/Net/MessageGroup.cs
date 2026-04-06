using LiteNetLib;

namespace Rex.Shared.Net;

/// <summary>Transport behavior group so callers share delivery defaults without sharing gameplay policy.</summary>
public enum MessageGroup
{
    /// <summary>Session setup, capability exchange and other control traffic.</summary>
    Core,

    /// <summary>High rate replication such as snapshots or deltas.</summary>
    Entity,

    /// <summary>Reliable lifecycle events such as create or remove notices.</summary>
    EntityEvent,

    /// <summary>High frequency streams such as input or controls.</summary>
    Input,

    /// <summary>Reliable messages without ordering guarantees.</summary>
    Command,

    /// <summary>Large payloads on a dedicated lane.</summary>
    Transfer
}

/// <summary>Default LiteNetLib channel and delivery for each <see cref="MessageGroup"/>.</summary>
public static class MessageGroupExtensions
{
    /// <summary>Default LiteNetLib channel and delivery for <paramref name="group"/>.</summary>
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
