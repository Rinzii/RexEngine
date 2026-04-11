using Rex.Shared.Components.BuiltIn;
using Rex.Shared.Entities;
using Rex.Shared.Entities.World;
using Rex.Shared.GameObjects;
using Rex.Shared.Serialization.Manager;

namespace Rex.Shared.Prototypes;

/// <summary>
/// Spawns ECS entities from loaded entity prototypes.
/// </summary>
public sealed class EntityPrototypeSpawner
{
    private readonly PrototypeManager _prototypeManager;
    private readonly ISerializationManager _serializationManager;

    /// <summary>
    /// Creates an entity prototype spawner.
    /// </summary>
    /// <param name="prototypeManager">Prototype manager used to resolve entity prototypes.</param>
    /// <param name="serializationManager">Serialization manager used to hydrate component payloads.</param>
    public EntityPrototypeSpawner(PrototypeManager prototypeManager, ISerializationManager serializationManager)
    {
        _prototypeManager = prototypeManager;
        _serializationManager = serializationManager;
    }

    /// <summary>
    /// Creates one ECS entity from an entity prototype.
    /// </summary>
    /// <param name="world">World to spawn into.</param>
    /// <param name="prototypeId">Prototype id to spawn.</param>
    /// <returns>The spawned entity.</returns>
    public EntityId Spawn(EcsWorld world, string prototypeId)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentException.ThrowIfNullOrWhiteSpace(prototypeId);

        EntityPrototype prototype = _prototypeManager.Index<EntityPrototype>(prototypeId);
        ThrowIfAbstract(prototype);
        EntityId entity = world.CreateEntity();
        EnsureGameplayComponents(world, entity, prototype);

        // Deterministic component order keeps hydration stable for the same prototype bytes across runs.
        foreach ((string componentName, MappingDataNode componentNode) in prototype.Components.OrderBy(static entry => entry.Key, StringComparer.Ordinal))
        {
            if (!world.Registry.TryGetComponentType(componentName, out Type componentType))
            {
                throw new InvalidOperationException(
                    $"Entity prototype '{prototypeId}' references unregistered component '{componentName}'.");
            }

            object component = _serializationManager.Read(componentType, componentNode)
                ?? throw new InvalidOperationException(
                    $"Entity prototype '{prototypeId}' deserialized component '{componentName}' as null.");

            ApplyComponent(world, entity, componentType, component);
        }

        return entity;
    }

    /// <summary>
    /// Creates one gameplay-facing entity from an entity prototype and raises component lifecycle events.
    /// </summary>
    /// <param name="manager">Entity manager to spawn into.</param>
    /// <param name="prototypeId">Prototype id to spawn.</param>
    /// <returns>The spawned entity.</returns>
    public EntityId Spawn(EntityManager manager, string prototypeId)
    {
        ArgumentNullException.ThrowIfNull(manager);
        return Spawn(manager.Systems, prototypeId);
    }

    /// <summary>
    /// Creates one ECS entity from an entity prototype and raises component lifecycle events through one system manager.
    /// </summary>
    /// <param name="manager">System manager to spawn through.</param>
    /// <param name="prototypeId">Prototype id to spawn.</param>
    /// <returns>The spawned entity.</returns>
    public EntityId Spawn(EntitySystemManager manager, string prototypeId)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentException.ThrowIfNullOrWhiteSpace(prototypeId);

        EntityPrototype prototype = _prototypeManager.Index<EntityPrototype>(prototypeId);
        ThrowIfAbstract(prototype);
        EntityId entity = manager.CreateBareEntity();
        manager.InitializeGameplayEntity(entity, prototype.Id, prototype.Name, prototype.Description);

        // Same ordering contract as the raw world spawn path.
        foreach ((string componentName, MappingDataNode componentNode) in prototype.Components.OrderBy(static entry => entry.Key, StringComparer.Ordinal))
        {
            if (!manager.World.Registry.TryGetComponentType(componentName, out Type componentType))
            {
                throw new InvalidOperationException(
                    $"Entity prototype '{prototypeId}' references unregistered component '{componentName}'.");
            }

            object component = _serializationManager.Read(componentType, componentNode)
                ?? throw new InvalidOperationException(
                    $"Entity prototype '{prototypeId}' deserialized component '{componentName}' as null.");

            ApplyComponent(manager, entity, componentType, component);
        }

        return entity;
    }

    private static void ThrowIfAbstract(EntityPrototype prototype)
    {
        if (prototype.Abstract)
        {
            throw new InvalidOperationException(
                $"Entity prototype '{prototype.Id}' is abstract and cannot be spawned.");
        }
    }

    private static void EnsureGameplayComponents(EcsWorld world, EntityId entity, EntityPrototype prototype)
    {
        if (!world.Has<TransformComponent>(entity))
        {
            world.Add(entity, new TransformComponent());
        }

        string resolvedName = ResolveEntityName(entity, prototype.Id, prototype.Name);
        if (!world.Has<MetaDataComponent>(entity))
        {
            world.Add(entity, new MetaDataComponent
            {
                PrototypeId = prototype.Id,
                EntityName = resolvedName,
                EntityDescription = prototype.Description
            });

            return;
        }

        MetaDataComponent metadata = world.Get<MetaDataComponent>(entity);
        bool changed = false;

        if (string.IsNullOrWhiteSpace(metadata.PrototypeId))
        {
            metadata.PrototypeId = prototype.Id;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(metadata.EntityName))
        {
            metadata.EntityName = resolvedName;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(metadata.EntityDescription) && !string.IsNullOrWhiteSpace(prototype.Description))
        {
            metadata.EntityDescription = prototype.Description;
            changed = true;
        }

        if (changed)
        {
            world.Set(entity, metadata);
        }
    }

    private static void ApplyComponent(EcsWorld world, EntityId entity, Type componentType, object component)
    {
        if (world.Has(entity, componentType))
        {
            world.SetBoxed(entity, componentType, component);
            return;
        }

        world.AddBoxed(entity, componentType, component);
    }

    private static void ApplyComponent(EntitySystemManager manager, EntityId entity, Type componentType, object component)
    {
        if (manager.World.Has(entity, componentType))
        {
            manager.SetComponent(entity, componentType, component);
            return;
        }

        manager.AddComponent(entity, componentType, component);
    }

    private static string ResolveEntityName(EntityId entity, string prototypeId, string? prototypeName)
    {
        if (!string.IsNullOrWhiteSpace(prototypeName))
        {
            return prototypeName;
        }

        if (!string.IsNullOrWhiteSpace(prototypeId))
        {
            return prototypeId;
        }

        return $"Entity {entity.Slot}";
    }
}
