using System.Collections.Concurrent;
using System.Reflection;
using Rex.Shared.Components.BuiltIn;
using Rex.Shared.Entities;
using Rex.Shared.Entities.World;
using Rex.Shared.IoC;
using Rex.Shared.Prototypes;
using EcsComponent = Rex.Shared.Components.IComponent;

namespace Rex.Shared.GameObjects;

/// <summary>
/// Creates entity systems, injects dependencies and dispatches local ECS events.
/// </summary>
public sealed class EntitySystemManager
{
    private readonly EntityPrototypeSpawner? _prototypeSpawner;
    private readonly Dictionary<Type, EntitySystem> _systems = [];
    private readonly Dictionary<DirectedSubscriptionKey, List<IDirectedSubscription>> _directedSubscriptions = [];
    private readonly Dictionary<DirectedSubscriptionKey, object> _directedRefSubscriptions = [];
    private readonly Dictionary<Type, List<IBroadcastSubscription>> _broadcastSubscriptions = [];
    private readonly Dictionary<Type, object> _broadcastRefSubscriptions = [];
    private readonly ConcurrentQueue<EntityCommandBuffer> _deferredCommandBuffers = new();
    private readonly AsyncLocal<SystemExecutionContext?> _currentExecutionContext = new();
    private EntitySystem[] _frameUpdateOrder = [];
    private SystemExecutionBatch[] _frameUpdateBatches = [];
    private EntitySystem[] _updateOrder = [];
    private SystemExecutionBatch[] _updateBatches = [];
    private bool _initialized;
    private bool _isFlushingDeferredWork;
    private int _managedExecutionDepth;
    private int _parallelBatchDepth;
    private int _subscriptionSequence;

    /// <summary>
    /// Creates a manager bound to one ECS world.
    /// </summary>
    /// <param name="world">World owned by the system manager.</param>
    /// <param name="prototypeSpawner">Optional authored-entity spawner available to the gameplay-facing system layer.</param>
    public EntitySystemManager(EcsWorld world, EntityPrototypeSpawner? prototypeSpawner = null)
    {
        World = world ?? throw new ArgumentNullException(nameof(world));
        _prototypeSpawner = prototypeSpawner;
        IoCManager.RegisterInstance(typeof(EntitySystemManager), this, overwrite: true);
    }

    /// <summary>Gets the shared ECS world.</summary>
    public EcsWorld World { get; }

    /// <summary>Gets a value indicating whether deferred gameplay-facing work is waiting for playback.</summary>
    public bool HasDeferredWork => !_deferredCommandBuffers.IsEmpty;

    /// <summary>Gets a value indicating whether this manager can spawn authored entity prototypes.</summary>
    public bool CanSpawnPrototypes => _prototypeSpawner != null;

    /// <summary>Gets a value indicating whether a system update, frame update or local event dispatch is currently running.</summary>
    public bool IsExecuting => Volatile.Read(ref _managedExecutionDepth) > 0;

    internal int UpdateBatchCount => _updateBatches.Length;

    internal int FrameUpdateBatchCount => _frameUpdateBatches.Length;

    /// <summary>
    /// Registers one entity system singleton.
    /// </summary>
    /// <typeparam name="TSystem">System type to add.</typeparam>
    /// <returns>The registered system singleton.</returns>
    public TSystem AddSystem<TSystem>()
        where TSystem : EntitySystem, new()
    {
        return (TSystem)AddSystem(typeof(TSystem));
    }

    /// <summary>
    /// Registers one entity system singleton.
    /// </summary>
    /// <param name="systemType">Concrete system type to add.</param>
    /// <returns>The registered system singleton.</returns>
    public EntitySystem AddSystem(Type systemType)
    {
        ArgumentNullException.ThrowIfNull(systemType);

        if (!typeof(EntitySystem).IsAssignableFrom(systemType) || systemType.IsAbstract)
        {
            throw new InvalidOperationException(
                $"System type '{systemType.FullName}' must be a non-abstract '{typeof(EntitySystem).FullName}'.");
        }

        if (_systems.TryGetValue(systemType, out EntitySystem? existing))
        {
            // Same concrete type always maps to one singleton instance.
            return existing;
        }

        var system = (EntitySystem)(Activator.CreateInstance(systemType)
            ?? throw new InvalidOperationException($"Failed to construct entity system '{systemType.FullName}'."));
        system.Attach(this);
        _systems.Add(systemType, system);
        IoCManager.RegisterInstance(systemType, system, overwrite: true);

        // Late AddSystem still injects dependencies and calls Initialize then rebuilds batches so the newcomer joins an already live graph.
        if (_initialized)
        {
            IoCManager.InjectDependencies(system);
            system.Initialize();
            RebuildUpdateOrder();
        }

        return system;
    }

    /// <summary>
    /// Registers every entity system found in an assembly.
    /// </summary>
    /// <param name="assembly">Assembly to scan.</param>
    public void AddSystemsFromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        // Every concrete non-abstract EntitySystem type becomes one singleton through AddSystem.
        foreach (Type type in assembly.GetTypes())
        {
            if (type.IsAbstract || !typeof(EntitySystem).IsAssignableFrom(type))
            {
                continue;
            }

            _ = AddSystem(type);
        }
    }

    /// <summary>
    /// Injects dependencies and initializes every registered system.
    /// </summary>
    public void Initialize()
    {
        if (_initialized)
        {
            throw new InvalidOperationException("Entity systems have already been initialized.");
        }

        // Inject every system first so Initialize on any system can read dependency fields on peers safely.
        foreach (EntitySystem system in _systems.Values)
        {
            IoCManager.InjectDependencies(system);
        }

        foreach (EntitySystem system in _systems.Values)
        {
            system.Initialize();
        }

        RebuildUpdateOrder();
        _initialized = true;
    }

    /// <summary>
    /// Shuts systems down in reverse registration order.
    /// </summary>
    public void Shutdown()
    {
        // Reverse topological update order when the graph ran at least once otherwise use registration map order.
        EntitySystem[] shutdownOrder = _initialized
            ? _updateOrder
            : [.. _systems.Values];

        foreach (EntitySystem system in shutdownOrder.Reverse())
        {
            system.Shutdown();
        }

        _initialized = false;
        _updateOrder = [];
        _frameUpdateOrder = [];
        _updateBatches = [];
        _frameUpdateBatches = [];
        ClearSubscriptions();
    }

    /// <summary>
    /// Runs one update tick for every registered system.
    /// </summary>
    /// <param name="frameTime">Frame time in seconds.</param>
    public void Update(float frameTime)
    {
        ExecuteBatches(_updateBatches, frameTime, isFrameUpdate: false);
        // Command buffers recorded during the update graph drain here so structural work stays ordered after systems.
        FlushDeferred();
    }

    /// <summary>
    /// Runs one render-frame update for every registered system that overrides frame update.
    /// </summary>
    /// <param name="frameTime">Frame time in seconds.</param>
    public void FrameUpdate(float frameTime)
    {
        ExecuteBatches(_frameUpdateBatches, frameTime, isFrameUpdate: true);
        // Same deferred drain as Update so render-phase systems can still enqueue structural work safely.
        FlushDeferred();
    }

    /// <summary>Creates one empty deferred command buffer.</summary>
    /// <returns>A new empty gameplay-facing command buffer.</returns>
    public EntityCommandBuffer CreateCommandBuffer()
    {
        return new EntityCommandBuffer(this);
    }

    /// <summary>Queues one command buffer for playback at the next deferred flush.</summary>
    /// <param name="commandBuffer">Command buffer to enqueue.</param>
    public void EnqueueCommandBuffer(EntityCommandBuffer commandBuffer)
    {
        ArgumentNullException.ThrowIfNull(commandBuffer);

        if (commandBuffer.IsEmpty)
        {
            return;
        }

        commandBuffer.SealForEnqueue();
        SystemExecutionContext? executionContext = _currentExecutionContext.Value;
        if (executionContext != null)
        {
            // Defer enqueue until the active system finishes so playback never nests inside another command.
            executionContext.CommandBuffers.Add(commandBuffer);
            return;
        }

        // No active system scope so this buffer joins the global queue immediately for the next FlushDeferred pass.
        _deferredCommandBuffers.Enqueue(commandBuffer);
    }

    /// <summary>Queues one entity deletion for deferred playback.</summary>
    /// <param name="entity">Entity to delete during playback.</param>
    public void QueueDeleteEntity(EntityId entity)
    {
        EntityCommandBuffer commandBuffer = CreateCommandBuffer();
        commandBuffer.DeleteEntity(entity);
        EnqueueCommandBuffer(commandBuffer);
    }

    /// <summary>Queues one entity creation for deferred playback.</summary>
    /// <param name="onCreated">Optional callback invoked with the created entity during playback.</param>
    public void QueueCreateEntity(Action<EntityId>? onCreated = null)
    {
        EntityCommandBuffer commandBuffer = CreateCommandBuffer();
        commandBuffer.CreateEntity(onCreated);
        EnqueueCommandBuffer(commandBuffer);
    }

    /// <summary>Queues one prototype-backed entity spawn for deferred playback.</summary>
    /// <param name="prototypeId">Prototype id to spawn during playback.</param>
    /// <param name="onSpawned">Optional callback invoked with the spawned entity during playback.</param>
    public void QueueSpawnEntity(string prototypeId, Action<EntityId>? onSpawned = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prototypeId);
        _ = EnsurePrototypeSpawner();
        EntityCommandBuffer commandBuffer = CreateCommandBuffer();
        commandBuffer.SpawnEntity(prototypeId, onSpawned);
        EnqueueCommandBuffer(commandBuffer);
    }

    /// <summary>Queues one component add for deferred playback.</summary>
    /// <param name="entity">Entity to update.</param>
    /// <param name="component">Component to add during playback.</param>
    /// <typeparam name="TComponent">Component type being added.</typeparam>
    public void QueueAddComponent<TComponent>(EntityId entity, in TComponent component)
        where TComponent : struct, EcsComponent
    {
        EntityCommandBuffer commandBuffer = CreateCommandBuffer();
        commandBuffer.AddComponent(entity, component);
        EnqueueCommandBuffer(commandBuffer);
    }

    /// <summary>Queues one boxed component add for deferred playback.</summary>
    /// <param name="entity">Entity to update.</param>
    /// <param name="componentType">Registered component type being added.</param>
    /// <param name="component">Component to add during playback.</param>
    public void QueueAddComponent(EntityId entity, Type componentType, object component)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        ArgumentNullException.ThrowIfNull(component);
        EntityCommandBuffer commandBuffer = CreateCommandBuffer();
        commandBuffer.AddComponent(entity, componentType, component);
        EnqueueCommandBuffer(commandBuffer);
    }

    /// <summary>Queues one component removal for deferred playback.</summary>
    /// <param name="entity">Entity to update.</param>
    /// <typeparam name="TComponent">Component type being removed.</typeparam>
    public void QueueRemoveComponent<TComponent>(EntityId entity)
        where TComponent : struct, EcsComponent
    {
        EntityCommandBuffer commandBuffer = CreateCommandBuffer();
        commandBuffer.RemoveComponent<TComponent>(entity);
        EnqueueCommandBuffer(commandBuffer);
    }

    /// <summary>Queues one boxed component removal for deferred playback.</summary>
    /// <param name="entity">Entity to update.</param>
    /// <param name="componentType">Registered component type being removed.</param>
    public void QueueRemoveComponent(EntityId entity, Type componentType)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        EntityCommandBuffer commandBuffer = CreateCommandBuffer();
        commandBuffer.RemoveComponent(entity, componentType);
        EnqueueCommandBuffer(commandBuffer);
    }

    /// <summary>Queues one in-place component overwrite for deferred playback.</summary>
    /// <param name="entity">Entity to update.</param>
    /// <param name="component">Replacement component value.</param>
    /// <typeparam name="TComponent">Component type being updated.</typeparam>
    public void QueueSetComponent<TComponent>(EntityId entity, in TComponent component)
        where TComponent : struct, EcsComponent
    {
        EntityCommandBuffer commandBuffer = CreateCommandBuffer();
        commandBuffer.SetComponent(entity, component);
        EnqueueCommandBuffer(commandBuffer);
    }

    /// <summary>Queues one boxed in-place component overwrite for deferred playback.</summary>
    /// <param name="entity">Entity to update.</param>
    /// <param name="componentType">Registered component type being updated.</param>
    /// <param name="component">Replacement component value.</param>
    public void QueueSetComponent(EntityId entity, Type componentType, object component)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        ArgumentNullException.ThrowIfNull(component);
        EntityCommandBuffer commandBuffer = CreateCommandBuffer();
        commandBuffer.SetComponent(entity, componentType, component);
        EnqueueCommandBuffer(commandBuffer);
    }

    /// <summary>Queues one directed local event and optionally broadcasts it during deferred playback.</summary>
    public void QueueLocalEvent<TEvent>(EntityId entity, TEvent args, bool broadcast = false)
        where TEvent : EntityEventArgs
    {
        ArgumentNullException.ThrowIfNull(args);
        EntityCommandBuffer commandBuffer = CreateCommandBuffer();
        commandBuffer.RaiseLocalEvent(entity, args, broadcast);
        EnqueueCommandBuffer(commandBuffer);
    }

    /// <summary>Queues one broadcast local event for deferred playback.</summary>
    public void QueueLocalEvent<TEvent>(TEvent args)
        where TEvent : EntityEventArgs
    {
        ArgumentNullException.ThrowIfNull(args);
        EntityCommandBuffer commandBuffer = CreateCommandBuffer();
        commandBuffer.RaiseLocalEvent(args);
        EnqueueCommandBuffer(commandBuffer);
    }

    /// <summary>Queues one directed struct event supplied through an in parameter and optionally broadcasts it during deferred playback.</summary>
    public void QueueLocalEvent<TEvent>(EntityId entity, in TEvent args, bool broadcast = false)
        where TEvent : struct
    {
        EntityCommandBuffer commandBuffer = CreateCommandBuffer();
        commandBuffer.RaiseLocalEvent(entity, args, broadcast);
        EnqueueCommandBuffer(commandBuffer);
    }

    /// <summary>Queues one broadcast struct event supplied through an in parameter for deferred playback.</summary>
    public void QueueLocalEvent<TEvent>(in TEvent args)
        where TEvent : struct
    {
        EntityCommandBuffer commandBuffer = CreateCommandBuffer();
        commandBuffer.RaiseLocalEvent(args);
        EnqueueCommandBuffer(commandBuffer);
    }

    /// <summary>Replays every deferred command buffer in FIFO order.</summary>
    public void FlushDeferred()
    {
        if (_isFlushingDeferredWork)
        {
            // Playback itself can enqueue follow-up buffers. Skip re-entry until the outer drain completes.
            return;
        }

        _isFlushingDeferredWork = true;
        try
        {
            while (!_deferredCommandBuffers.IsEmpty)
            {
                if (!_deferredCommandBuffers.TryDequeue(out EntityCommandBuffer? commandBuffer))
                {
                    break;
                }

                commandBuffer.Playback(this);
            }
        }
        finally
        {
            _isFlushingDeferredWork = false;
        }
    }

    /// <summary>Gets one registered entity system singleton.</summary>
    public TSystem GetEntitySystem<TSystem>()
        where TSystem : EntitySystem
    {
        if (!_systems.TryGetValue(typeof(TSystem), out EntitySystem? system))
        {
            throw new InvalidOperationException(
                $"Entity system '{typeof(TSystem).FullName}' has not been registered.");
        }

        return (TSystem)system;
    }

    /// <summary>Attempts to get one registered entity system singleton.</summary>
    public bool TryGetEntitySystem<TSystem>(out TSystem? system)
        where TSystem : EntitySystem
    {
        if (_systems.TryGetValue(typeof(TSystem), out EntitySystem? existing))
        {
            system = (TSystem)existing;
            return true;
        }

        system = null;
        return false;
    }

    /// <summary>Checks whether one shared singleton component exists.</summary>
    public bool HasSingleton<TComponent>()
        where TComponent : struct, EcsComponent
    {
        return World.HasSingleton<TComponent>();
    }

    /// <summary>Reads one shared singleton component.</summary>
    public TComponent GetSingleton<TComponent>()
        where TComponent : struct, EcsComponent
    {
        return World.GetSingleton<TComponent>();
    }

    /// <summary>Gets a read-only reference to one shared singleton component.</summary>
    public ref readonly TComponent GetSingletonRef<TComponent>()
        where TComponent : struct, EcsComponent
    {
        return ref World.GetSingletonRef<TComponent>();
    }

    /// <summary>Gets a writable reference to one shared singleton component.</summary>
    public ref TComponent GetMutableSingletonRef<TComponent>()
        where TComponent : struct, EcsComponent
    {
        return ref World.GetMutableSingletonRef<TComponent>();
    }

    /// <summary>Adds one shared singleton component.</summary>
    public void AddSingleton<TComponent>(in TComponent component)
        where TComponent : struct, EcsComponent
    {
        World.AddSingleton(component);
    }

    /// <summary>Overwrites one shared singleton component in place.</summary>
    public void SetSingleton<TComponent>(in TComponent component)
        where TComponent : struct, EcsComponent
    {
        World.SetSingleton(component);
    }

    /// <summary>Removes one shared singleton component.</summary>
    public bool RemoveSingleton<TComponent>()
        where TComponent : struct, EcsComponent
    {
        return World.RemoveSingleton<TComponent>();
    }

    /// <summary>Creates one gameplay-facing entity with the baseline shared components.</summary>
    public EntityId CreateEntity()
    {
        EnsureImmediateStructuralMutationAllowed(nameof(CreateEntity));
        EntityId entity = CreateBareEntity();
        InitializeGameplayEntity(entity, prototypeId: null, entityName: null, entityDescription: null);
        return entity;
    }

    /// <summary>Creates one entity from an authored prototype.</summary>
    /// <param name="prototypeId">Prototype id to spawn.</param>
    /// <returns>The spawned entity.</returns>
    public EntityId SpawnEntity(string prototypeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prototypeId);
        EnsureImmediateStructuralMutationAllowed(nameof(SpawnEntity));
        return EnsurePrototypeSpawner().Spawn(this, prototypeId);
    }

    internal EntityId CreateBareEntity()
    {
        return World.CreateEntity();
    }

    internal void InitializeGameplayEntity(EntityId entity, string? prototypeId, string? entityName, string? entityDescription)
    {
        // Shared by spawn and create paths so gameplay always sees transform plus metadata with merge rules on reuse.
        if (!World.Has<TransformComponent>(entity))
        {
            AddComponent(entity, new TransformComponent());
        }

        string resolvedName = ResolveEntityName(entity, prototypeId, entityName);
        if (!World.Has<MetaDataComponent>(entity))
        {
            AddComponent(entity, new MetaDataComponent
            {
                PrototypeId = prototypeId,
                EntityName = resolvedName,
                EntityDescription = entityDescription
            });

            return;
        }

        // Keep existing prototype id name and description when the row already exists. Only empty fields absorb new values.
        MetaDataComponent metadata = World.Get<MetaDataComponent>(entity);
        bool changed = false;

        if (string.IsNullOrWhiteSpace(metadata.PrototypeId) && !string.IsNullOrWhiteSpace(prototypeId))
        {
            metadata.PrototypeId = prototypeId;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(metadata.EntityName))
        {
            metadata.EntityName = resolvedName;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(metadata.EntityDescription) && !string.IsNullOrWhiteSpace(entityDescription))
        {
            metadata.EntityDescription = entityDescription;
            changed = true;
        }

        if (changed)
        {
            World.Set(entity, metadata);
        }
    }

    /// <summary>Destroys one entity and raises component shutdown/remove events first.</summary>
    public bool DestroyEntity(EntityId entity)
    {
        EnsureImmediateStructuralMutationAllowed(nameof(DestroyEntity));
        if (!World.Exists(entity))
        {
            return false;
        }

        // Teardown is event driven. Handlers may destroy the entity so each step re-checks that it still exists.
        EntityTerminatingEvent terminating = new();
        RaiseDirectedEvent(entity, ref terminating, null);
        if (!World.Exists(entity))
        {
            return true;
        }

        // Snapshot columns so shutdown and remove handlers cannot reorder or skip ids while mutating storage.
        foreach (int componentId in World.GetComponentIds(entity).ToArray())
        {
            if (!World.Exists(entity))
            {
                return true;
            }

            ComponentShutdownEvent shutdown = new();
            RaiseDirectedEvent(entity, ref shutdown, componentId);
            if (!World.Exists(entity))
            {
                return true;
            }

            ComponentRemoveEvent remove = new();
            RaiseDirectedEvent(entity, ref remove, componentId);
        }

        // True when the slot is already empty or the world finishes removal after the event pass.
        return !World.Exists(entity) || World.DestroyEntity(entity);
    }

    /// <summary>Adds one component and raises add/init/startup lifecycle events.</summary>
    public void AddComponent<TComponent>(EntityId entity, in TComponent component)
        where TComponent : struct, EcsComponent
    {
        EnsureImmediateStructuralMutationAllowed(nameof(AddComponent));
        World.Add(entity, component);
        int componentId = World.Registry.GetComponentId<TComponent>();
        ComponentAddedEvent added = new();
        RaiseDirectedEvent(entity, ref added, componentId);
        ComponentInitEvent init = new();
        RaiseDirectedEvent(entity, ref init, componentId);
        ComponentStartupEvent startup = new();
        RaiseDirectedEvent(entity, ref startup, componentId);
    }

    /// <summary>Adds one boxed component and raises add/init/startup lifecycle events.</summary>
    public void AddComponent(EntityId entity, Type componentType, object component)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        ArgumentNullException.ThrowIfNull(component);

        EnsureImmediateStructuralMutationAllowed(nameof(AddComponent));
        World.AddBoxed(entity, componentType, component);
        int componentId = World.Registry.GetRegistration(componentType).Id;
        ComponentAddedEvent added = new();
        RaiseDirectedEvent(entity, ref added, componentId);
        ComponentInitEvent init = new();
        RaiseDirectedEvent(entity, ref init, componentId);
        ComponentStartupEvent startup = new();
        RaiseDirectedEvent(entity, ref startup, componentId);
    }

    /// <summary>Removes one component and raises shutdown/remove lifecycle events first.</summary>
    public bool RemoveComponent<TComponent>(EntityId entity)
        where TComponent : struct, EcsComponent
    {
        EnsureImmediateStructuralMutationAllowed(nameof(RemoveComponent));
        if (!World.Has<TComponent>(entity))
        {
            return false;
        }

        int componentId = World.Registry.GetComponentId<TComponent>();
        ComponentShutdownEvent shutdown = new();
        RaiseDirectedEvent(entity, ref shutdown, componentId);
        ComponentRemoveEvent remove = new();
        RaiseDirectedEvent(entity, ref remove, componentId);
        return World.Remove<TComponent>(entity);
    }

    /// <summary>Removes one boxed component and raises shutdown/remove lifecycle events first.</summary>
    public bool RemoveComponent(EntityId entity, Type componentType)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        EnsureImmediateStructuralMutationAllowed(nameof(RemoveComponent));
        if (!World.Has(entity, componentType))
        {
            return false;
        }

        int componentId = World.Registry.GetRegistration(componentType).Id;
        ComponentShutdownEvent shutdown = new();
        RaiseDirectedEvent(entity, ref shutdown, componentId);
        ComponentRemoveEvent remove = new();
        RaiseDirectedEvent(entity, ref remove, componentId);
        return World.Remove(entity, componentType);
    }

    /// <summary>Overwrites one boxed component value in place.</summary>
    public void SetComponent(EntityId entity, Type componentType, object component)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        ArgumentNullException.ThrowIfNull(component);
        World.SetBoxed(entity, componentType, component);
    }

    /// <summary>Raises one directed local event and optionally broadcasts it.</summary>
    public void RaiseLocalEvent<TEvent>(EntityId entity, TEvent args, bool broadcast = false)
        where TEvent : EntityEventArgs
    {
        ArgumentNullException.ThrowIfNull(args);
        EnsureSynchronousLocalEventAllowed();
        RaiseDirectedEvent(entity, args, null);
        if (broadcast)
        {
            RaiseLocalEvent(args);
        }
    }

    /// <summary>Raises one broadcast local event.</summary>
    public void RaiseLocalEvent<TEvent>(TEvent args)
        where TEvent : EntityEventArgs
    {
        ArgumentNullException.ThrowIfNull(args);
        EnsureSynchronousLocalEventAllowed();
        if (!_broadcastSubscriptions.TryGetValue(typeof(TEvent), out List<IBroadcastSubscription>? subscriptions))
        {
            return;
        }

        BeginManagedExecution();
        try
        {
            // Copy so SubscribeBroadcast during invoke cannot mutate the live list.
            foreach (IBroadcastSubscription subscription in subscriptions.ToArray())
            {
                subscription.Invoke(args);
            }
        }
        finally
        {
            EndManagedExecution();
        }
    }

    /// <summary>Raises one directed local event with a struct payload passed by ref and optionally broadcasts it.</summary>
    public void RaiseLocalEvent<TEvent>(EntityId entity, ref TEvent args, bool broadcast = false)
        where TEvent : struct
    {
        EnsureSynchronousLocalEventAllowed();
        RaiseDirectedEvent(entity, ref args, null);
        if (broadcast)
        {
            RaiseLocalEvent(ref args);
        }
    }

    /// <summary>Raises one broadcast local event with a struct payload passed by ref.</summary>
    public void RaiseLocalEvent<TEvent>(ref TEvent args)
        where TEvent : struct
    {
        EnsureSynchronousLocalEventAllowed();
        if (!_broadcastRefSubscriptions.TryGetValue(typeof(TEvent), out object? subscriptions))
        {
            return;
        }

        BeginManagedExecution();
        try
        {
            // Copy so SubscribeBroadcast during invoke cannot mutate the live list.
            foreach (IRefBroadcastSubscription<TEvent> subscription in ((List<IRefBroadcastSubscription<TEvent>>)subscriptions).ToArray())
            {
                subscription.Invoke(ref args);
            }
        }
        finally
        {
            EndManagedExecution();
        }
    }

    internal void SubscribeDirected<TComponent, TEvent>(EntitySystem owner, EntityEventHandler<TComponent, TEvent> handler, int order)
        where TComponent : struct, EcsComponent
        where TEvent : EntityEventArgs
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(handler);

        int componentId = World.Registry.GetComponentId<TComponent>();
        DirectedSubscriptionKey key = new(componentId, typeof(TEvent));
        if (!_directedSubscriptions.TryGetValue(key, out List<IDirectedSubscription>? subscriptions))
        {
            subscriptions = [];
            _directedSubscriptions.Add(key, subscriptions);
        }

        subscriptions.Add(new DirectedSubscription<TComponent, TEvent>(order, _subscriptionSequence++, handler));
        subscriptions.Sort(static (left, right) => SubscriptionOrderComparer.Instance.Compare(left, right));
    }

    internal void SubscribeReadOnlyDirected<TComponent, TEvent>(
        EntitySystem owner,
        ReadOnlyEntityEventHandler<TComponent, TEvent> handler,
        int order)
        where TComponent : struct, EcsComponent
        where TEvent : EntityEventArgs
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(handler);

        int componentId = World.Registry.GetComponentId<TComponent>();
        DirectedSubscriptionKey key = new(componentId, typeof(TEvent));
        if (!_directedSubscriptions.TryGetValue(key, out List<IDirectedSubscription>? subscriptions))
        {
            subscriptions = [];
            _directedSubscriptions.Add(key, subscriptions);
        }

        subscriptions.Add(new ReadOnlyDirectedSubscription<TComponent, TEvent>(order, _subscriptionSequence++, handler));
        subscriptions.Sort(static (left, right) => SubscriptionOrderComparer.Instance.Compare(left, right));
    }

    internal void SubscribeBroadcast<TEvent>(EntitySystem owner, BroadcastEventHandler<TEvent> handler, int order)
        where TEvent : EntityEventArgs
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(handler);

        Type eventType = typeof(TEvent);
        if (!_broadcastSubscriptions.TryGetValue(eventType, out List<IBroadcastSubscription>? subscriptions))
        {
            subscriptions = [];
            _broadcastSubscriptions.Add(eventType, subscriptions);
        }

        subscriptions.Add(new BroadcastSubscription<TEvent>(order, _subscriptionSequence++, handler));
        subscriptions.Sort(static (left, right) => SubscriptionOrderComparer.Instance.Compare(left, right));
    }

    internal void SubscribeDirected<TComponent, TEvent>(EntitySystem owner, RefEntityEventHandler<TComponent, TEvent> handler,
        int order)
        where TComponent : struct, EcsComponent
        where TEvent : struct
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(handler);

        int componentId = World.Registry.GetComponentId<TComponent>();
        DirectedSubscriptionKey key = new(componentId, typeof(TEvent));
        if (!_directedRefSubscriptions.TryGetValue(key, out object? subscriptions))
        {
            subscriptions = new List<IRefDirectedSubscription<TEvent>>();
            _directedRefSubscriptions.Add(key, subscriptions);
        }

        var typedSubscriptions = (List<IRefDirectedSubscription<TEvent>>)subscriptions;
        typedSubscriptions.Add(new RefDirectedSubscription<TComponent, TEvent>(order, _subscriptionSequence++, handler));
        typedSubscriptions.Sort(static (left, right) => SubscriptionOrderComparer.Instance.Compare(left, right));
    }

    internal void SubscribeReadOnlyDirected<TComponent, TEvent>(
        EntitySystem owner,
        ReadOnlyRefEntityEventHandler<TComponent, TEvent> handler,
        int order)
        where TComponent : struct, EcsComponent
        where TEvent : struct
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(handler);

        int componentId = World.Registry.GetComponentId<TComponent>();
        DirectedSubscriptionKey key = new(componentId, typeof(TEvent));
        if (!_directedRefSubscriptions.TryGetValue(key, out object? subscriptions))
        {
            subscriptions = new List<IRefDirectedSubscription<TEvent>>();
            _directedRefSubscriptions.Add(key, subscriptions);
        }

        var typedSubscriptions = (List<IRefDirectedSubscription<TEvent>>)subscriptions;
        typedSubscriptions.Add(new ReadOnlyRefDirectedSubscription<TComponent, TEvent>(order, _subscriptionSequence++, handler));
        typedSubscriptions.Sort(static (left, right) => SubscriptionOrderComparer.Instance.Compare(left, right));
    }

    internal void SubscribeBroadcast<TEvent>(EntitySystem owner, RefBroadcastEventHandler<TEvent> handler, int order)
        where TEvent : struct
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(handler);

        Type eventType = typeof(TEvent);
        if (!_broadcastRefSubscriptions.TryGetValue(eventType, out object? subscriptions))
        {
            subscriptions = new List<IRefBroadcastSubscription<TEvent>>();
            _broadcastRefSubscriptions.Add(eventType, subscriptions);
        }

        var typedSubscriptions = (List<IRefBroadcastSubscription<TEvent>>)subscriptions;
        typedSubscriptions.Add(new RefBroadcastSubscription<TEvent>(order, _subscriptionSequence++, handler));
        typedSubscriptions.Sort(static (left, right) => SubscriptionOrderComparer.Instance.Compare(left, right));
    }

    private void RaiseDirectedEvent<TEvent>(EntityId entity, TEvent args, int? componentIdFilter)
        where TEvent : EntityEventArgs
    {
        ArgumentNullException.ThrowIfNull(args);

        BeginManagedExecution();
        try
        {
            if (!World.Exists(entity))
            {
                throw new InvalidOperationException($"Entity '{entity}' is not alive in this world.");
            }

            // Snapshot ids so handlers that add or remove components cannot reorder this walk mid loop.
            int[] componentIds = World.GetComponentIds(entity).ToArray();
            for (int i = 0; i < componentIds.Length; i++)
            {
                if (!World.Exists(entity))
                {
                    return;
                }

                int componentId = componentIds[i];
                if (componentIdFilter.HasValue && componentId != componentIdFilter.Value)
                {
                    continue;
                }

                if (!World.Has(entity, componentId))
                {
                    continue;
                }

                DirectedSubscriptionKey key = new(componentId, typeof(TEvent));
                if (!_directedSubscriptions.TryGetValue(key, out List<IDirectedSubscription>? subscriptions))
                {
                    continue;
                }

                // Copy so a handler that calls SubscribeDirected cannot mutate this list during enumeration.
                foreach (IDirectedSubscription subscription in subscriptions.ToArray())
                {
                    if (!World.Exists(entity))
                    {
                        return;
                    }

                    if (!World.Has(entity, componentId))
                    {
                        // Component vanished mid dispatch so skip remaining handlers for this column only.
                        break;
                    }

                    subscription.Invoke(World, entity, args);
                }
            }
        }
        finally
        {
            EndManagedExecution();
        }
    }

    private static string ResolveEntityName(EntityId entity, string? prototypeId, string? entityName)
    {
        if (!string.IsNullOrWhiteSpace(entityName))
        {
            return entityName;
        }

        if (!string.IsNullOrWhiteSpace(prototypeId))
        {
            return prototypeId;
        }

        return $"Entity {entity.Slot}";
    }

    private void RaiseDirectedEvent<TEvent>(EntityId entity, ref TEvent args, int? componentIdFilter)
        where TEvent : struct
    {
        BeginManagedExecution();
        try
        {
            if (!World.Exists(entity))
            {
                throw new InvalidOperationException($"Entity '{entity}' is not alive in this world.");
            }

            // Snapshot ids so handlers that add or remove components cannot reorder this walk mid loop.
            int[] componentIds = World.GetComponentIds(entity).ToArray();
            for (int i = 0; i < componentIds.Length; i++)
            {
                if (!World.Exists(entity))
                {
                    return;
                }

                int componentId = componentIds[i];
                if (componentIdFilter.HasValue && componentId != componentIdFilter.Value)
                {
                    continue;
                }

                if (!World.Has(entity, componentId))
                {
                    continue;
                }

                DirectedSubscriptionKey key = new(componentId, typeof(TEvent));
                if (!_directedRefSubscriptions.TryGetValue(key, out object? subscriptions))
                {
                    continue;
                }

                // Copy so SubscribeReadOnlyDirected or similar cannot corrupt this list during enumeration.
                foreach (IRefDirectedSubscription<TEvent> subscription in ((List<IRefDirectedSubscription<TEvent>>)subscriptions).ToArray())
                {
                    if (!World.Exists(entity))
                    {
                        return;
                    }

                    if (!World.Has(entity, componentId))
                    {
                        // Component vanished mid dispatch so skip remaining handlers for this column only.
                        break;
                    }

                    subscription.Invoke(World, entity, ref args);
                }
            }
        }
        finally
        {
            EndManagedExecution();
        }
    }

    private static bool NeedsFrameUpdate(EntitySystem system)
    {
        Type systemType = system.GetType();
        MethodInfo? frameUpdate = systemType.GetMethod(nameof(EntitySystem.FrameUpdate), [typeof(float)]);
        // Only a real override moves DeclaringType off EntitySystem so the base no-op stays out of the frame graph.
        return frameUpdate?.DeclaringType != typeof(EntitySystem);
    }

    private void RebuildUpdateOrder()
    {
        List<EntitySystem> registrationOrder = [.. _systems.Values];
        SystemOrderingPlan updatePlan = OrderSystems(
            registrationOrder,
            static system => (int)system.UpdatePhase,
            static system => true);
        _updateOrder = updatePlan.OrderedSystems;
        SystemOrderingPlan framePlan = OrderSystems(
            registrationOrder,
            static system => (int)system.FrameUpdatePhase,
            NeedsFrameUpdate);
        _frameUpdateOrder = framePlan.OrderedSystems;
        _updateBatches = BuildExecutionBatches(
            _updateOrder,
            static system => (int)system.UpdatePhase,
            updatePlan.DependencyClosure);
        _frameUpdateBatches = BuildExecutionBatches(
            _frameUpdateOrder,
            static system => (int)system.FrameUpdatePhase,
            framePlan.DependencyClosure);
    }

    private EntityPrototypeSpawner EnsurePrototypeSpawner()
    {
        return _prototypeSpawner
               ?? throw new InvalidOperationException("This entity-system manager was created without prototype spawning support.");
    }

    private void PublishDeferredCommandBuffers(SystemExecutionContext? executionContext)
    {
        if (executionContext == null || executionContext.CommandBuffers.Count == 0)
        {
            return;
        }

        // Preserve list order for this run. FlushDeferred dequeues FIFO from the shared queue after batch steps finish.
        for (int i = 0; i < executionContext.CommandBuffers.Count; i++)
        {
            _deferredCommandBuffers.Enqueue(executionContext.CommandBuffers[i]);
        }
    }

    private void ClearSubscriptions()
    {
        // Avoid stale handler lists if the host shuts down then initializes again on the same manager instance.
        _directedSubscriptions.Clear();
        _directedRefSubscriptions.Clear();
        _broadcastSubscriptions.Clear();
        _broadcastRefSubscriptions.Clear();
        _subscriptionSequence = 0;
    }

    private static Dictionary<Type, EntitySystem> BuildOrderingAliases(IReadOnlyCollection<EntitySystem> systems)
    {
        // Concrete types map to their live instance. Base types and interfaces map only when exactly one concrete claims them.
        var aliases = systems.ToDictionary(static system => system.GetType(), static system => system);
        Dictionary<Type, Type?> candidateTypes = [];

        foreach (EntitySystem system in systems)
        {
            Type systemType = system.GetType();
            foreach (Type alias in EnumerateOrderingAliases(systemType))
            {
                if (alias == systemType)
                {
                    continue;
                }

                if (!candidateTypes.TryGetValue(alias, out Type? existing))
                {
                    candidateTypes.Add(alias, systemType);
                    continue;
                }

                if (existing != systemType)
                {
                    // Ambiguous alias so ordering resolution cannot pick a unique system for this type token.
                    candidateTypes[alias] = null;
                }
            }
        }

        foreach ((Type alias, Type? concreteType) in candidateTypes)
        {
            if (concreteType != null)
            {
                aliases[alias] = systems.Single(system => system.GetType() == concreteType);
            }
        }

        return aliases;
    }

    private static Dictionary<EntitySystem, HashSet<EntitySystem>> BuildDirectDependencies(List<EntitySystem> includedSystems)
    {
        // Each entry lists systems that must run before this one. UpdatesBefore records the inverse edge on the peer.
        Dictionary<Type, EntitySystem> orderingAliases = BuildOrderingAliases(includedSystems);
        Dictionary<EntitySystem, HashSet<EntitySystem>> dependencies = [];

        foreach (EntitySystem system in includedSystems)
        {
            dependencies.Add(system, []);
        }

        foreach (EntitySystem system in includedSystems)
        {
            foreach (Type dependencyType in system.UpdatesAfter)
            {
                if (TryResolveOrderingSystem(dependencyType, orderingAliases, out EntitySystem dependency)
                    && dependency != system)
                {
                    _ = dependencies[system].Add(dependency);
                }
            }

            foreach (Type dependencyType in system.UpdatesBefore)
            {
                if (TryResolveOrderingSystem(dependencyType, orderingAliases, out EntitySystem dependency)
                    && dependency != system)
                {
                    _ = dependencies[dependency].Add(system);
                }
            }
        }

        return dependencies;
    }

    private static Dictionary<EntitySystem, HashSet<EntitySystem>> CloneDependencies(
        IReadOnlyDictionary<EntitySystem, HashSet<EntitySystem>> dependencies)
    {
        // Deep copy each adjacency list so Kahn can remove edges without mutating the map used for BuildDependencyClosure.
        Dictionary<EntitySystem, HashSet<EntitySystem>> clone = [];
        foreach (KeyValuePair<EntitySystem, HashSet<EntitySystem>> pair in dependencies)
        {
            clone.Add(pair.Key, [.. pair.Value]);
        }

        return clone;
    }

    private static Dictionary<EntitySystem, HashSet<EntitySystem>> BuildDependents(
        IReadOnlyDictionary<EntitySystem, HashSet<EntitySystem>> dependencies)
    {
        // For each system lists who still depends on it so one finished node can decrement many incoming edges at once.
        Dictionary<EntitySystem, HashSet<EntitySystem>> dependents = [];
        foreach (EntitySystem system in dependencies.Keys)
        {
            dependents.Add(system, []);
        }

        foreach (KeyValuePair<EntitySystem, HashSet<EntitySystem>> pair in dependencies)
        {
            foreach (EntitySystem dependency in pair.Value)
            {
                _ = dependents[dependency].Add(pair.Key);
            }
        }

        return dependents;
    }

    private static Dictionary<EntitySystem, HashSet<EntitySystem>> BuildDependencyClosure(
        IReadOnlyDictionary<EntitySystem, HashSet<EntitySystem>> directDependencies)
    {
        // Full upstream set per system so parallel batching can test ordering without repeated walks.
        Dictionary<EntitySystem, HashSet<EntitySystem>> closure = [];
        foreach (EntitySystem system in directDependencies.Keys)
        {
            HashSet<EntitySystem> reachable = [];
            Stack<EntitySystem> stack = new(directDependencies[system]);
            while (stack.Count > 0)
            {
                EntitySystem dependency = stack.Pop();
                if (!reachable.Add(dependency))
                {
                    continue;
                }

                foreach (EntitySystem nestedDependency in directDependencies[dependency])
                {
                    stack.Push(nestedDependency);
                }
            }

            closure.Add(system, reachable);
        }

        return closure;
    }

    private static IEnumerable<Type> EnumerateOrderingAliases(Type systemType)
    {
        // Lets UpdatesAfter and UpdatesBefore name a base class or interface instead of only the concrete system type.
        for (Type? current = systemType.BaseType; current != null && current != typeof(object); current = current.BaseType)
        {
            yield return current;
        }

        foreach (Type interfaceType in systemType.GetInterfaces())
        {
            yield return interfaceType;
        }
    }

    private static bool TryResolveOrderingSystem(Type systemType, Dictionary<Type, EntitySystem> aliases,
        out EntitySystem system)
    {
        ArgumentNullException.ThrowIfNull(systemType);
        // False means the token never became a unique alias so this ordering edge is skipped quietly.
        bool found = aliases.TryGetValue(systemType, out EntitySystem? resolved);
        system = resolved!;
        return found;
    }

    private static SystemOrderingPlan OrderSystems(
        List<EntitySystem> registrationOrder,
        Func<EntitySystem, int> phaseSelector,
        Func<EntitySystem, bool> includePredicate)
    {
        // Topological sort from UpdatesAfter and UpdatesBefore constraints. Phase then registration order breaks ties.
        var includedSystems = registrationOrder.Where(includePredicate).ToList();
        var registrationIndexes = registrationOrder
            .Select(static (system, index) => new KeyValuePair<EntitySystem, int>(system, index))
            .ToDictionary(static pair => pair.Key, static pair => pair.Value);
        Dictionary<EntitySystem, HashSet<EntitySystem>> directDependencies = BuildDirectDependencies(includedSystems);
        Dictionary<EntitySystem, HashSet<EntitySystem>> dependencies = CloneDependencies(directDependencies);
        Dictionary<EntitySystem, HashSet<EntitySystem>> dependents = BuildDependents(directDependencies);

        var ready = includedSystems
            .Where(system => dependencies[system].Count == 0)
            .OrderBy(system => phaseSelector(system))
            .ThenBy(system => registrationIndexes[system])
            .ToList();
        List<EntitySystem> ordered = [];

        while (ready.Count > 0)
        {
            EntitySystem current = ready[0];
            ready.RemoveAt(0);
            ordered.Add(current);

            foreach (EntitySystem dependent in dependents[current].OrderBy(system => registrationIndexes[system]))
            {
                _ = dependencies[dependent].Remove(current);
                if (dependencies[dependent].Count == 0)
                {
                    ready.Add(dependent);
                }
            }

            ready.Sort((left, right) =>
            {
                int phaseComparison = phaseSelector(left).CompareTo(phaseSelector(right));
                return phaseComparison != 0
                    ? phaseComparison
                    : registrationIndexes[left].CompareTo(registrationIndexes[right]);
            });
        }

        // If the queue emptied early the graph still has unsatisfied edges so report a cycle.
        if (ordered.Count != includedSystems.Count)
        {
            string cycle = string.Join(", ",
                includedSystems
                    .Except(ordered)
                    .Select(static system => system.GetType().Name)
                    .OrderBy(static name => name, StringComparer.Ordinal));
            throw new InvalidOperationException($"Entity system update order contains a cycle: {cycle}.");
        }

        return new SystemOrderingPlan([.. ordered], BuildDependencyClosure(directDependencies));
    }

    private void ExecuteBatches(SystemExecutionBatch[] batches, float frameTime, bool isFrameUpdate)
    {
        foreach (SystemExecutionBatch batch in batches)
        {
            if (batch.IsParallel && batch.Systems.Length > 1)
            {
                // Each branch owns its own SystemExecutionContext instance so parallel work never shares one buffer list.
                var executionContexts = new SystemExecutionContext?[batch.Systems.Length];
                _ = Interlocked.Increment(ref _parallelBatchDepth);
                try
                {
                    _ = Parallel.For(
                        0,
                        batch.Systems.Length,
                        index => executionContexts[index] = ExecuteSystem(batch.Systems[index], frameTime, isFrameUpdate));
                }
                finally
                {
                    _ = Interlocked.Decrement(ref _parallelBatchDepth);
                }

                // Barrier first so parallel systems finish before any deferred structural work hits the world.
                for (int i = 0; i < executionContexts.Length; i++)
                {
                    PublishDeferredCommandBuffers(executionContexts[i]);
                }

                continue;
            }

            // One system at a time on this thread. Each system's deferred buffers flush before the next system runs.
            for (int i = 0; i < batch.Systems.Length; i++)
            {
                PublishDeferredCommandBuffers(ExecuteSystem(batch.Systems[i], frameTime, isFrameUpdate));
            }
        }
    }

    private SystemExecutionContext ExecuteSystem(EntitySystem system, float frameTime, bool isFrameUpdate)
    {
        SystemExecutionContext executionContext = new();
        SystemExecutionContext? previousContext = _currentExecutionContext.Value;
        // AsyncLocal steers EnqueueCommandBuffer onto this list for nested calls until the system entry returns.
        _currentExecutionContext.Value = executionContext;
        BeginManagedExecution();
        try
        {
            if (isFrameUpdate)
            {
                system.FrameUpdate(frameTime);
            }
            else
            {
                system.Update(frameTime);
            }
        }
        finally
        {
            EndManagedExecution();
            _currentExecutionContext.Value = previousContext;
        }

        return executionContext;
    }

    private static SystemExecutionBatch[] BuildExecutionBatches(
        EntitySystem[] orderedSystems,
        Func<EntitySystem, int> phaseSelector,
        IReadOnlyDictionary<EntitySystem, HashSet<EntitySystem>> dependencyClosure)
    {
        List<SystemExecutionBatch> batches = [];
        List<EntitySystem> candidateBatch = [];
        int currentPhase = int.MinValue;

        // Parallel batches only group same-phase systems that opt in declare access and pass conflict checks.
        for (int i = 0; i < orderedSystems.Length; i++)
        {
            EntitySystem system = orderedSystems[i];
            int phase = phaseSelector(system);
            if (phase != currentPhase)
            {
                FlushCandidateBatch(batches, candidateBatch);
                currentPhase = phase;
            }

            if (!CanRunInParallelBatch(system))
            {
                FlushCandidateBatch(batches, candidateBatch);
                batches.Add(new SystemExecutionBatch([system], isParallel: false));
                continue;
            }

            if (!CanAddToBatch(candidateBatch, system, dependencyClosure))
            {
                FlushCandidateBatch(batches, candidateBatch);
            }

            candidateBatch.Add(system);
        }

        FlushCandidateBatch(batches, candidateBatch);
        return [.. batches];
    }

    private static void FlushCandidateBatch(List<SystemExecutionBatch> batches, List<EntitySystem> candidateBatch)
    {
        if (candidateBatch.Count == 0)
        {
            return;
        }

        // More than one system in a batch means Parallel.For. A single system always runs sequentially.
        batches.Add(new SystemExecutionBatch(
            [.. candidateBatch],
            isParallel: candidateBatch.Count > 1));
        candidateBatch.Clear();
    }

    private static bool CanRunInParallelBatch(EntitySystem system)
    {
        return system.SupportsParallelExecution && HasDeclaredAccess(system);
    }

    private static bool CanAddToBatch(
        List<EntitySystem> currentBatch,
        EntitySystem candidate,
        IReadOnlyDictionary<EntitySystem, HashSet<EntitySystem>> dependencyClosure)
    {
        if (currentBatch.Count == 0)
        {
            return true;
        }

        for (int i = 0; i < currentBatch.Count; i++)
        {
            if (HasAccessConflict(currentBatch[i], candidate)
                || HasOrderingDependency(currentBatch[i], candidate, dependencyClosure))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasDeclaredAccess(EntitySystem system)
    {
        return system.DeclaredComponentReads.Count > 0
               || system.DeclaredComponentWrites.Count > 0
               || system.DeclaredSingletonReads.Count > 0
               || system.DeclaredSingletonWrites.Count > 0;
    }

    private static bool HasAccessConflict(EntitySystem left, EntitySystem right)
    {
        return SharesAny(left.DeclaredComponentWrites, right.DeclaredComponentWrites)
               || SharesAny(left.DeclaredComponentWrites, right.DeclaredComponentReads)
               || SharesAny(left.DeclaredComponentReads, right.DeclaredComponentWrites)
               || SharesAny(left.DeclaredSingletonWrites, right.DeclaredSingletonWrites)
               || SharesAny(left.DeclaredSingletonWrites, right.DeclaredSingletonReads)
               || SharesAny(left.DeclaredSingletonReads, right.DeclaredSingletonWrites);
    }

    private static bool HasOrderingDependency(
        EntitySystem left,
        EntitySystem right,
        IReadOnlyDictionary<EntitySystem, HashSet<EntitySystem>> dependencyClosure)
    {
        return dependencyClosure[right].Contains(left) || dependencyClosure[left].Contains(right);
    }

    private static bool SharesAny(IReadOnlyCollection<Type> left, IReadOnlyCollection<Type> right)
    {
        if (left.Count == 0 || right.Count == 0)
        {
            return false;
        }

        IReadOnlyCollection<Type> smaller = left.Count <= right.Count ? left : right;
        IReadOnlyCollection<Type> larger = ReferenceEquals(smaller, left) ? right : left;

        foreach (Type type in smaller)
        {
            if (larger.Contains(type))
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureImmediateStructuralMutationAllowed(string operation)
    {
        // Managed execution covers system ticks and local event dispatch. Immediate archetype work would race those paths.
        if (IsExecuting)
        {
            throw new InvalidOperationException(
                $"Immediate structural operation '{operation}' is not allowed during system execution or event dispatch. Queue the work through an entity command buffer instead.");
        }
    }

    private void EnsureSynchronousLocalEventAllowed()
    {
        // Sequential batches still allow events. Parallel.For does not because handlers would run without a single-thread contract.
        if (Volatile.Read(ref _parallelBatchDepth) > 0)
        {
            throw new InvalidOperationException(
                "Synchronous local events are not allowed during parallel system execution. Queue the event through an entity command buffer or disable parallel execution for the participating systems.");
        }
    }

    private void BeginManagedExecution()
    {
        // World rejects direct structural edits that bypass command buffers while this scope is active.
        World.BeginStructuralMutationScope();
        _ = Interlocked.Increment(ref _managedExecutionDepth);
    }

    private void EndManagedExecution()
    {
        _ = Interlocked.Decrement(ref _managedExecutionDepth);
        World.EndStructuralMutationScope();
    }

    private interface IOrderedSubscription
    {
        int Order { get; }

        int Sequence { get; }
    }

    private sealed class SystemExecutionBatch
    {
        public SystemExecutionBatch(EntitySystem[] systems, bool isParallel)
        {
            Systems = systems;
            IsParallel = isParallel;
        }

        public EntitySystem[] Systems { get; }

        public bool IsParallel { get; }
    }

    private sealed class SystemExecutionContext
    {
        public List<EntityCommandBuffer> CommandBuffers { get; } = [];
    }

    // OrderedSystems is the linear schedule. DependencyClosure is transitive predecessors for parallel batch rules.
    private readonly record struct SystemOrderingPlan(
        EntitySystem[] OrderedSystems,
        Dictionary<EntitySystem, HashSet<EntitySystem>> DependencyClosure);

    private interface IDirectedSubscription : IOrderedSubscription
    {
        void Invoke(EcsWorld world, EntityId entity, EntityEventArgs args);
    }

    private interface IBroadcastSubscription : IOrderedSubscription
    {
        void Invoke(EntityEventArgs args);
    }

    private interface IRefDirectedSubscription<TEvent> : IOrderedSubscription
        where TEvent : struct
    {
        void Invoke(EcsWorld world, EntityId entity, ref TEvent args);
    }

    private interface IRefBroadcastSubscription<TEvent> : IOrderedSubscription
        where TEvent : struct
    {
        void Invoke(ref TEvent args);
    }

    private readonly record struct DirectedSubscriptionKey(int ComponentId, Type EventType);

    private sealed class DirectedSubscription<TComponent, TEvent> : IDirectedSubscription
        where TComponent : struct, EcsComponent
        where TEvent : EntityEventArgs
    {
        private readonly EntityEventHandler<TComponent, TEvent> _handler;

        public DirectedSubscription(int order, int sequence, EntityEventHandler<TComponent, TEvent> handler)
        {
            Order = order;
            Sequence = sequence;
            _handler = handler;
        }

        public int Order { get; }

        public int Sequence { get; }

        public void Invoke(EcsWorld world, EntityId entity, EntityEventArgs args)
        {
            ref TComponent component = ref world.GetMutableRef<TComponent>(entity);
            _handler(entity, ref component, (TEvent)args);
        }
    }

    private sealed class ReadOnlyDirectedSubscription<TComponent, TEvent> : IDirectedSubscription
        where TComponent : struct, EcsComponent
        where TEvent : EntityEventArgs
    {
        private readonly ReadOnlyEntityEventHandler<TComponent, TEvent> _handler;

        public ReadOnlyDirectedSubscription(int order, int sequence, ReadOnlyEntityEventHandler<TComponent, TEvent> handler)
        {
            Order = order;
            Sequence = sequence;
            _handler = handler;
        }

        public int Order { get; }

        public int Sequence { get; }

        public void Invoke(EcsWorld world, EntityId entity, EntityEventArgs args)
        {
            ref readonly TComponent component = ref world.GetRef<TComponent>(entity);
            _handler(entity, in component, (TEvent)args);
        }
    }

    private sealed class BroadcastSubscription<TEvent> : IBroadcastSubscription
        where TEvent : EntityEventArgs
    {
        private readonly BroadcastEventHandler<TEvent> _handler;

        public BroadcastSubscription(int order, int sequence, BroadcastEventHandler<TEvent> handler)
        {
            Order = order;
            Sequence = sequence;
            _handler = handler;
        }

        public int Order { get; }

        public int Sequence { get; }

        public void Invoke(EntityEventArgs args)
        {
            _handler((TEvent)args);
        }
    }

    private sealed class RefDirectedSubscription<TComponent, TEvent> : IRefDirectedSubscription<TEvent>
        where TComponent : struct, EcsComponent
        where TEvent : struct
    {
        private readonly RefEntityEventHandler<TComponent, TEvent> _handler;

        public RefDirectedSubscription(int order, int sequence, RefEntityEventHandler<TComponent, TEvent> handler)
        {
            Order = order;
            Sequence = sequence;
            _handler = handler;
        }

        public int Order { get; }

        public int Sequence { get; }

        public void Invoke(EcsWorld world, EntityId entity, ref TEvent args)
        {
            ref TComponent component = ref world.GetMutableRef<TComponent>(entity);
            _handler(entity, ref component, ref args);
        }
    }

    private sealed class ReadOnlyRefDirectedSubscription<TComponent, TEvent> : IRefDirectedSubscription<TEvent>
        where TComponent : struct, EcsComponent
        where TEvent : struct
    {
        private readonly ReadOnlyRefEntityEventHandler<TComponent, TEvent> _handler;

        public ReadOnlyRefDirectedSubscription(
            int order,
            int sequence,
            ReadOnlyRefEntityEventHandler<TComponent, TEvent> handler)
        {
            Order = order;
            Sequence = sequence;
            _handler = handler;
        }

        public int Order { get; }

        public int Sequence { get; }

        public void Invoke(EcsWorld world, EntityId entity, ref TEvent args)
        {
            ref readonly TComponent component = ref world.GetRef<TComponent>(entity);
            _handler(entity, in component, ref args);
        }
    }

    private sealed class RefBroadcastSubscription<TEvent> : IRefBroadcastSubscription<TEvent>
        where TEvent : struct
    {
        private readonly RefBroadcastEventHandler<TEvent> _handler;

        public RefBroadcastSubscription(int order, int sequence, RefBroadcastEventHandler<TEvent> handler)
        {
            Order = order;
            Sequence = sequence;
            _handler = handler;
        }

        public int Order { get; }

        public int Sequence { get; }

        public void Invoke(ref TEvent args)
        {
            _handler(ref args);
        }
    }

    private sealed class SubscriptionOrderComparer : IComparer<IOrderedSubscription>
    {
        public static SubscriptionOrderComparer Instance { get; } = new();

        public int Compare(IOrderedSubscription? left, IOrderedSubscription? right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            int order = left.Order.CompareTo(right.Order);
            return order != 0 ? order : left.Sequence.CompareTo(right.Sequence);
        }
    }
}
