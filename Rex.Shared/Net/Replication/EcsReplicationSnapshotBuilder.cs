using Rex.Shared.Components.Registration;
using Rex.Shared.Entities;
using Rex.Shared.Entities.World;

namespace Rex.Shared.Net.Replication;

/// <summary>
/// Builds ECS-native replication payloads from one world and one replicated component profile.
/// </summary>
public sealed class EcsReplicationSnapshotBuilder
{
    private readonly ComponentRegistry _registry;
    private readonly int[] _replicatedComponentIds;

    /// <summary>
    /// Creates one ECS replication snapshot builder for a fixed replicated component profile.
    /// </summary>
    public EcsReplicationSnapshotBuilder(ComponentRegistry registry, IEnumerable<Type> replicatedComponentTypes)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(replicatedComponentTypes);

        _registry = registry;
        // Fixed id list defines the on-wire column order for every entity in this profile.
        _replicatedComponentIds = replicatedComponentTypes
            .Select(type => registry.GetRegistration(type).Id)
            .Distinct()
            .OrderBy(static id => id)
            .ToArray();
    }

    /// <summary>
    /// Builds replicated ECS payloads for the supplied network-visible entities.
    /// </summary>
    public IReadOnlyList<ReplicatedEntityState> Build(EcsWorld world, IEnumerable<KeyValuePair<int, EntityId>> entities)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(entities);

        List<ReplicatedEntityState> replicatedEntities = [];
        foreach (KeyValuePair<int, EntityId> pair in entities.OrderBy(static pair => pair.Key))
        {
            if (!world.Exists(pair.Value))
            {
                continue;
            }

            List<ReplicatedComponentState> components = [];
            for (int i = 0; i < _replicatedComponentIds.Length; i++)
            {
                int componentId = _replicatedComponentIds[i];
                // Archetype might omit optional components. Skip rather than emit empty payloads.
                if (!world.Has(pair.Value, componentId))
                {
                    continue;
                }

                ComponentRegistration registration = _registry.GetRegistration(componentId);
                byte[] payload = registration.Serializer.SerializeBoxed(world.GetBoxedComponent(pair.Value, componentId));
                components.Add(new ReplicatedComponentState(componentId, payload));
            }

            replicatedEntities.Add(new ReplicatedEntityState(pair.Key, components));
        }

        return replicatedEntities;
    }
}
