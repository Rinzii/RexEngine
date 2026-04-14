using Rex.Shared.Components.Registration;
using Rex.Shared.Entities;
using Rex.Shared.Entities.Queries;
using Rex.Shared.Entities.World;
using Rex.Shared.Prototypes;
using Rex.Shared.Serialization.Manager;
using EcsComponent = Rex.Shared.Components.IComponent;

namespace Rex.Shared.GameObjects;

/// <summary>
/// Gameplay-facing entity manager layered over the shared ECS world, systems and prototype spawning.
/// </summary>
public sealed class EntityManager
{
    /// <summary>
    /// Creates a gameplay-facing entity manager over one component registry.
    /// </summary>
    /// <param name="registry">Shared component registry for the new world.</param>
    /// <param name="prototypeManager">Optional prototype manager used for authored entity spawning.</param>
    /// <param name="serializationManager">Optional serialization manager required when prototype spawning is enabled.</param>
    public EntityManager(ComponentRegistry registry, PrototypeManager? prototypeManager = null,
        ISerializationManager? serializationManager = null)
    {
        ArgumentNullException.ThrowIfNull(registry);

        EntityPrototypeSpawner? prototypeSpawner = prototypeManager == null
            ? null
            : new EntityPrototypeSpawner(
                prototypeManager,
                serializationManager
                    ?? throw new InvalidOperationException("A serialization manager is required when prototype spawning is enabled."));

        World = new EcsWorld(registry);
        Systems = new EntitySystemManager(World, prototypeSpawner);
        // Event dispatch re-enters the same system manager that performs ECS mutations and deferred playback.
        EventBus = new EntityEventBus(Systems);
    }

    /// <summary>Gets the storage world owned by this manager.</summary>
    public EcsWorld World { get; }

    /// <summary>Gets the gameplay-facing system manager.</summary>
    public EntitySystemManager Systems { get; }

    /// <summary>Gets the directed and broadcast local event bus.</summary>
    public EntityEventBus EventBus { get; }

    /// <summary>Initializes all registered systems.</summary>
    public void InitializeSystems()
    {
        Systems.Initialize();
    }

    /// <summary>Shuts all registered systems down.</summary>
    public void ShutdownSystems()
    {
        Systems.Shutdown();
    }

    /// <summary>Runs one update tick for all registered systems.</summary>
    /// <param name="frameTime">Frame time in seconds.</param>
    public void Update(float frameTime)
    {
        Systems.Update(frameTime);
    }

    /// <summary>Runs one render-frame update for all registered systems.</summary>
    /// <param name="frameTime">Frame time in seconds.</param>
    public void FrameUpdate(float frameTime)
    {
        Systems.FrameUpdate(frameTime);
    }

    /// <summary>Registers one gameplay-facing entity system.</summary>
    public TSystem AddSystem<TSystem>()
        where TSystem : EntitySystem, new()
    {
        return Systems.AddSystem<TSystem>();
    }

    /// <summary>Creates one empty deferred command buffer.</summary>
    public EntityCommandBuffer CreateCommandBuffer()
    {
        return Systems.CreateCommandBuffer();
    }

    /// <summary>Queues one deferred command buffer for playback.</summary>
    public void EnqueueCommandBuffer(EntityCommandBuffer commandBuffer)
    {
        Systems.EnqueueCommandBuffer(commandBuffer);
    }

    /// <summary>Flushes every deferred gameplay-facing command buffer immediately.</summary>
    public void FlushDeferred()
    {
        Systems.FlushDeferred();
    }

    /// <summary>Gets one registered gameplay-facing entity system.</summary>
    public TSystem System<TSystem>()
        where TSystem : EntitySystem
    {
        return Systems.GetEntitySystem<TSystem>();
    }

    /// <summary>Attempts to get one registered gameplay-facing entity system.</summary>
    public bool TrySystem<TSystem>(out TSystem? system)
        where TSystem : EntitySystem
    {
        return Systems.TryGetEntitySystem(out system);
    }

    /// <summary>Creates one empty entity.</summary>
    public EntityId CreateEntity() => Systems.CreateEntity();

    /// <summary>Creates one entity from an authored prototype.</summary>
    /// <param name="prototypeId">Prototype id to spawn.</param>
    /// <returns>The spawned entity.</returns>
    public EntityId SpawnEntity(string prototypeId)
    {
        return Systems.SpawnEntity(prototypeId);
    }

    /// <summary>Queues one entity creation for deferred playback.</summary>
    public void QueueCreateEntity(Action<EntityId>? onCreated = null)
    {
        Systems.QueueCreateEntity(onCreated);
    }

    /// <summary>Queues one prototype-backed entity spawn for deferred playback.</summary>
    public void QueueSpawnEntity(string prototypeId, Action<EntityId>? onSpawned = null)
    {
        Systems.QueueSpawnEntity(prototypeId, onSpawned);
    }

    /// <summary>Destroys one entity.</summary>
    public bool DeleteEntity(EntityId entity) => Systems.DestroyEntity(entity);

    /// <summary>Queues one entity deletion for deferred playback.</summary>
    public void QueueDeleteEntity(EntityId entity)
    {
        Systems.QueueDeleteEntity(entity);
    }

    /// <summary>Checks whether one entity is alive.</summary>
    public bool Exists(EntityId entity) => World.Exists(entity);

    /// <summary>Checks whether one shared singleton component exists.</summary>
    public bool HasSingleton<TComponent>()
        where TComponent : struct, EcsComponent
    {
        return Systems.HasSingleton<TComponent>();
    }

    /// <summary>Reads one shared singleton component value.</summary>
    public TComponent GetSingleton<TComponent>()
        where TComponent : struct, EcsComponent
    {
        return Systems.GetSingleton<TComponent>();
    }

    /// <summary>Gets a read-only reference to one shared singleton component.</summary>
    public ref readonly TComponent GetSingletonRef<TComponent>()
        where TComponent : struct, EcsComponent
    {
        return ref Systems.GetSingletonRef<TComponent>();
    }

    /// <summary>Gets a writable reference to one shared singleton component.</summary>
    public ref TComponent GetMutableSingletonRef<TComponent>()
        where TComponent : struct, EcsComponent
    {
        return ref Systems.GetMutableSingletonRef<TComponent>();
    }

    /// <summary>Adds one shared singleton component.</summary>
    public void AddSingleton<TComponent>(in TComponent component)
        where TComponent : struct, EcsComponent
    {
        Systems.AddSingleton(component);
    }

    /// <summary>Overwrites one shared singleton component in place.</summary>
    public void SetSingleton<TComponent>(in TComponent component)
        where TComponent : struct, EcsComponent
    {
        Systems.SetSingleton(component);
    }

    /// <summary>Removes one shared singleton component.</summary>
    public bool RemoveSingleton<TComponent>()
        where TComponent : struct, EcsComponent
    {
        return Systems.RemoveSingleton<TComponent>();
    }

    /// <summary>Adds one component to an entity.</summary>
    public void AddComponent<TComponent>(EntityId entity, in TComponent component)
        where TComponent : struct, EcsComponent
    {
        Systems.AddComponent(entity, component);
    }

    /// <summary>Queues one component add for deferred playback.</summary>
    public void QueueAddComponent<TComponent>(EntityId entity, in TComponent component)
        where TComponent : struct, EcsComponent
    {
        Systems.QueueAddComponent(entity, component);
    }

    /// <summary>Queues one boxed component add for deferred playback.</summary>
    public void QueueAddComponent(EntityId entity, Type componentType, object component)
    {
        Systems.QueueAddComponent(entity, componentType, component);
    }

    /// <summary>Adds one boxed component to an entity.</summary>
    public void AddComponent(EntityId entity, Type componentType, object component)
    {
        Systems.AddComponent(entity, componentType, component);
    }

    /// <summary>Removes one component from an entity.</summary>
    public bool RemoveComponent<TComponent>(EntityId entity)
        where TComponent : struct, EcsComponent
    {
        return Systems.RemoveComponent<TComponent>(entity);
    }

    /// <summary>Queues one component removal for deferred playback.</summary>
    public void QueueRemoveComponent<TComponent>(EntityId entity)
        where TComponent : struct, EcsComponent
    {
        Systems.QueueRemoveComponent<TComponent>(entity);
    }

    /// <summary>Queues one boxed component removal for deferred playback.</summary>
    public void QueueRemoveComponent(EntityId entity, Type componentType)
    {
        Systems.QueueRemoveComponent(entity, componentType);
    }

    /// <summary>Checks whether an entity has one component type.</summary>
    public bool HasComponent<TComponent>(EntityId entity)
        where TComponent : struct, EcsComponent
    {
        return World.Has<TComponent>(entity);
    }

    /// <summary>Attempts to read one component value from an entity.</summary>
    public bool TryGetComponent<TComponent>(EntityId entity, out TComponent component)
        where TComponent : struct, EcsComponent
    {
        return World.TryGet(entity, out component);
    }

    /// <summary>Reads one component value from an entity.</summary>
    public TComponent GetComponent<TComponent>(EntityId entity)
        where TComponent : struct, EcsComponent
    {
        return World.Get<TComponent>(entity);
    }

    /// <summary>Gets a read-only reference to one component.</summary>
    public ref readonly TComponent GetComponentRef<TComponent>(EntityId entity)
        where TComponent : struct, EcsComponent
    {
        return ref World.GetRef<TComponent>(entity);
    }

    /// <summary>Gets a writable reference to one component.</summary>
    public ref TComponent GetMutableComponentRef<TComponent>(EntityId entity)
        where TComponent : struct, EcsComponent
    {
        return ref World.GetMutableRef<TComponent>(entity);
    }

    /// <summary>Overwrites one existing component value in place.</summary>
    public void SetComponent<TComponent>(EntityId entity, in TComponent component)
        where TComponent : struct, EcsComponent
    {
        World.Set(entity, component);
    }

    /// <summary>Overwrites one existing boxed component value in place.</summary>
    public void SetComponent(EntityId entity, Type componentType, object component)
    {
        Systems.SetComponent(entity, componentType, component);
    }

    /// <summary>Queues one in-place component overwrite for deferred playback.</summary>
    public void QueueSetComponent<TComponent>(EntityId entity, in TComponent component)
        where TComponent : struct, EcsComponent
    {
        Systems.QueueSetComponent(entity, component);
    }

    /// <summary>Queues one boxed in-place component overwrite for deferred playback.</summary>
    public void QueueSetComponent(EntityId entity, Type componentType, object component)
    {
        Systems.QueueSetComponent(entity, componentType, component);
    }

    /// <summary>Creates one allocation-free query over a single component type.</summary>
    public ComponentQuery<TComponent> Query<TComponent>()
        where TComponent : struct, EcsComponent
    {
        return World.Query<TComponent>();
    }

    /// <summary>Creates one allocation-free query over two component types.</summary>
    public ComponentQuery<TComponent1, TComponent2> Query<TComponent1, TComponent2>()
        where TComponent1 : struct, EcsComponent
        where TComponent2 : struct, EcsComponent
    {
        return World.Query<TComponent1, TComponent2>();
    }

    /// <summary>Creates one allocation-free query over three component types.</summary>
    public ComponentQuery<TComponent1, TComponent2, TComponent3> Query<TComponent1, TComponent2, TComponent3>()
        where TComponent1 : struct, EcsComponent
        where TComponent2 : struct, EcsComponent
        where TComponent3 : struct, EcsComponent
    {
        return World.Query<TComponent1, TComponent2, TComponent3>();
    }
}
