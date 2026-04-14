using Rex.Shared.Entities;

namespace Rex.Shared.Serialization.Components;

/// <summary>
/// Deterministic serialized snapshot of one ECS world.
/// </summary>
public sealed class SerializedWorld
{
    /// <summary>Creates a serialized world snapshot.</summary>
    /// <param name="entities">Deterministically ordered serialized entities.</param>
    public SerializedWorld(IReadOnlyList<SerializedEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        Entities = entities;
    }

    /// <summary>Gets the serialized entities in the snapshot.</summary>
    public IReadOnlyList<SerializedEntity> Entities { get; }
}

/// <summary>
/// Deterministic serialized representation of one entity and its ordered component payloads.
/// </summary>
public sealed class SerializedEntity
{
    /// <summary>Creates a serialized entity snapshot.</summary>
    /// <param name="entityId">Serialized entity id.</param>
    /// <param name="components">Ordered serialized component payloads keyed by component id.</param>
    public SerializedEntity(EntityId entityId, IReadOnlyList<KeyValuePair<int, byte[]>> components)
    {
        if (!entityId.IsValid)
        {
            throw new ArgumentException("Serialized entity ids must be valid.", nameof(entityId));
        }

        ArgumentNullException.ThrowIfNull(components);
        EntityId = entityId;
        Components = components;
    }

    /// <summary>Gets the serialized entity id.</summary>
    public EntityId EntityId { get; }

    /// <summary>Gets the ordered serialized component payloads keyed by component id.</summary>
    public IReadOnlyList<KeyValuePair<int, byte[]>> Components { get; }
}
