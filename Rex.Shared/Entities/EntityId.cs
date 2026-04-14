namespace Rex.Shared.Entities;

/// <summary>
/// Stable entity handle composed of a one-based slot and a generation.
/// Stale handles become invalid once the slot is recycled with a newer generation.
/// </summary>
public readonly struct EntityId : IEquatable<EntityId>
{
    /// <summary>Invalid entity handle value.</summary>
    public static EntityId Invalid => default;

    /// <summary>Creates an entity handle from a one-based slot and generation.</summary>
    /// <param name="slot">One-based slot number.</param>
    /// <param name="generation">Generation for the slot.</param>
    public EntityId(int slot, int generation)
    {
        if (slot < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(slot), slot, "Slot must be non-negative.");
        }

        if (generation < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(generation), generation, "Generation must be non-negative.");
        }

        Slot = slot;
        Generation = generation;
    }

    /// <summary>One-based slot number. Zero is reserved for <see cref="Invalid"/>.</summary>
    public int Slot { get; }

    /// <summary>Generation for this slot. Zero is reserved for <see cref="Invalid"/>.</summary>
    public int Generation { get; }

    /// <summary>True when this handle references a live slot/generation pair.</summary>
    public bool IsValid => Slot > 0 && Generation > 0;

    internal int SlotIndex => Slot - 1;

    internal static EntityId FromSlotIndex(int slotIndex, int generation) => new(slotIndex + 1, generation);

    /// <summary>Checks whether two entity handles reference the same slot and generation.</summary>
    /// <param name="other">Other entity handle to compare.</param>
    /// <returns><see langword="true"/> when the handles are equal.</returns>
    public bool Equals(EntityId other) => Slot == other.Slot && Generation == other.Generation;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is EntityId other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Slot, Generation);

    /// <summary>Checks whether two entity handles are equal.</summary>
    /// <param name="left">Left entity handle.</param>
    /// <param name="right">Right entity handle.</param>
    /// <returns><see langword="true"/> when the handles are equal.</returns>
    public static bool operator ==(EntityId left, EntityId right)
    {
        return left.Equals(right);
    }

    /// <summary>Checks whether two entity handles are not equal.</summary>
    /// <param name="left">Left entity handle.</param>
    /// <param name="right">Right entity handle.</param>
    /// <returns><see langword="true"/> when the handles are not equal.</returns>
    public static bool operator !=(EntityId left, EntityId right)
    {
        return !left.Equals(right);
    }

    /// <inheritdoc />
    public override string ToString() => IsValid ? $"Entity({Slot}:{Generation})" : "Entity(Invalid)";
}
