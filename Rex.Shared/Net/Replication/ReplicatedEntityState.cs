namespace Rex.Shared.Net.Replication;

/// <summary>
/// Replicated ECS component payloads for one network-visible entity.
/// </summary>
public sealed class ReplicatedEntityState
{
    /// <summary>
    /// Creates one replicated entity payload.
    /// </summary>
    public ReplicatedEntityState(int entityId, IReadOnlyList<ReplicatedComponentState> components)
    {
        if (entityId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(entityId), entityId, "Replicated entity ids must be positive.");
        }

        ArgumentNullException.ThrowIfNull(components);
        EntityId = entityId;
        Components = components;
    }

    /// <summary>Gets the stable replicated entity id.</summary>
    public int EntityId { get; }

    /// <summary>Gets the replicated component payloads for this entity.</summary>
    public IReadOnlyList<ReplicatedComponentState> Components { get; }
}
