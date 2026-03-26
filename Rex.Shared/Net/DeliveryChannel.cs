using LiteNetLib;

namespace Rex.Shared.Net;

/// <summary>
/// Fixed LiteNetLib channel IDs used by Rex networking.
/// </summary>
public static class DeliveryChannel
{
    /// <summary>
    /// Reliable ordered lane for handshake and other ordered messages.
    /// </summary>
    public const byte Reliable = 0;

    /// <summary><see cref="DeliveryMethod.ReliableOrdered"/> for <see cref="Reliable"/>.</summary>
    public static readonly DeliveryMethod ReliableMethod = DeliveryMethod.ReliableOrdered;

    /// <summary>
    /// Sequenced lane for snapshots.
    /// </summary>
    public const byte Snapshot = 1;

    /// <summary><see cref="DeliveryMethod.Sequenced"/> for <see cref="Snapshot"/>.</summary>
    public static readonly DeliveryMethod SnapshotMethod = DeliveryMethod.Sequenced;

    /// <summary>
    /// Reliable unordered lane for commands and similar traffic.
    /// </summary>
    public const byte ReliableUnordered = 2;

    /// <summary><see cref="DeliveryMethod.ReliableUnordered"/> for <see cref="ReliableUnordered"/>.</summary>
    public static readonly DeliveryMethod ReliableUnorderedMethod = DeliveryMethod.ReliableUnordered;

    /// <summary>
    /// Best-effort lane for input.
    /// </summary>
    public const byte Unreliable = 3;

    /// <summary><see cref="DeliveryMethod.Unreliable"/> for <see cref="Unreliable"/>.</summary>
    public static readonly DeliveryMethod UnreliableMethod = DeliveryMethod.Unreliable;

    /// <summary>
    /// Separate lane so bulk transfers do not stall game state traffic.
    /// </summary>
    public const byte Transfer = 4;

    /// <summary><see cref="DeliveryMethod.ReliableOrdered"/> for <see cref="Transfer"/>.</summary>
    public static readonly DeliveryMethod TransferMethod = DeliveryMethod.ReliableOrdered;
}
