using Rex.Shared.Entities;
using Rex.Shared.Entities.World;
using EcsComponent = Rex.Shared.Components.IComponent;

namespace Rex.Shared.GameObjects;

/// <summary>
/// Base class for entity systems operating on the shared ECS world.
/// </summary>
/// <remarks>
/// Subscribe local events from <see cref="Initialize"/> after the manager attached this system and dependency
/// injection completed on every system. <see cref="EntitySystemManager.Shutdown"/> clears all subscription tables
/// on that manager.
/// Declare component and singleton access from the constructor or <see cref="Initialize"/> so parallel batching
/// matches runtime usage. Declarations matter when <see cref="SupportsParallelExecution"/> is true and at least
/// one read or write set is non-empty on this system.
/// </remarks>
public abstract class EntitySystem
{
    private EntitySystemManager? _manager;
    private readonly HashSet<Type> _declaredComponentReads = [];
    private readonly HashSet<Type> _declaredComponentWrites = [];
    private readonly HashSet<Type> _declaredSingletonReads = [];
    private readonly HashSet<Type> _declaredSingletonWrites = [];
    private readonly List<Type> _updatesAfter = [];
    private readonly List<Type> _updatesBefore = [];

    /// <summary>Gets the world owned by the current system manager.</summary>
    protected EcsWorld World => _manager?.World
        ?? throw new InvalidOperationException("Entity system has not been attached to a system manager.");

    /// <summary>Gets the systems that must update before this system.</summary>
    protected internal IList<Type> UpdatesAfter => _updatesAfter;

    /// <summary>Gets the systems that must update after this system.</summary>
    protected internal IList<Type> UpdatesBefore => _updatesBefore;

    /// <summary>Gets the fixed-step execution phase for this system.</summary>
    protected internal virtual SystemUpdatePhase UpdatePhase => SystemUpdatePhase.Update;

    /// <summary>Gets the frame execution phase for this system.</summary>
    protected internal virtual FrameUpdatePhase FrameUpdatePhase => FrameUpdatePhase.Frame;

    /// <summary>Gets a value indicating whether this system may run in a parallel scheduler batch.</summary>
    protected internal virtual bool SupportsParallelExecution => false;

    /// <summary>Gets the declared component read dependencies for this system.</summary>
    protected internal IReadOnlyCollection<Type> DeclaredComponentReads => _declaredComponentReads;

    /// <summary>Gets the declared component write dependencies for this system.</summary>
    protected internal IReadOnlyCollection<Type> DeclaredComponentWrites => _declaredComponentWrites;

    /// <summary>Gets the declared singleton read dependencies for this system.</summary>
    protected internal IReadOnlyCollection<Type> DeclaredSingletonReads => _declaredSingletonReads;

    /// <summary>Gets the declared singleton write dependencies for this system.</summary>
    protected internal IReadOnlyCollection<Type> DeclaredSingletonWrites => _declaredSingletonWrites;

    /// <summary>Called when the system graph has been created and dependencies were injected.</summary>
    public virtual void Initialize()
    {
    }

    /// <summary>Called when the system graph is shutting down.</summary>
    public virtual void Shutdown()
    {
    }

    /// <summary>Called once per update tick by the system manager.</summary>
    /// <param name="frameTime">Frame time in seconds.</param>
    public virtual void Update(float frameTime)
    {
    }

    /// <summary>Called once per rendered frame by the system manager.</summary>
    /// <param name="frameTime">Frame time in seconds.</param>
    public virtual void FrameUpdate(float frameTime)
    {
    }

    /// <summary>Gets another registered entity system.</summary>
    /// <typeparam name="TSystem">System type to resolve.</typeparam>
    /// <returns>Resolved system singleton.</returns>
    protected TSystem GetEntitySystem<TSystem>()
        where TSystem : EntitySystem
    {
        return Manager.GetEntitySystem<TSystem>();
    }

    /// <summary>Checks whether one shared singleton component exists.</summary>
    protected bool HasSingleton<TComponent>()
        where TComponent : struct, EcsComponent
    {
        return World.HasSingleton<TComponent>();
    }

    /// <summary>Reads one shared singleton component value.</summary>
    protected TComponent GetSingleton<TComponent>()
        where TComponent : struct, EcsComponent
    {
        return World.GetSingleton<TComponent>();
    }

    /// <summary>Gets a read-only reference to one shared singleton component.</summary>
    protected ref readonly TComponent GetSingletonRef<TComponent>()
        where TComponent : struct, EcsComponent
    {
        return ref World.GetSingletonRef<TComponent>();
    }

    /// <summary>Gets a writable reference to one shared singleton component.</summary>
    protected ref TComponent GetMutableSingletonRef<TComponent>()
        where TComponent : struct, EcsComponent
    {
        return ref World.GetMutableSingletonRef<TComponent>();
    }

    /// <summary>Adds one shared singleton component.</summary>
    protected void AddSingleton<TComponent>(in TComponent component)
        where TComponent : struct, EcsComponent
    {
        World.AddSingleton(component);
    }

    /// <summary>Overwrites one shared singleton component in place.</summary>
    protected void SetSingleton<TComponent>(in TComponent component)
        where TComponent : struct, EcsComponent
    {
        World.SetSingleton(component);
    }

    /// <summary>Removes one shared singleton component.</summary>
    protected bool RemoveSingleton<TComponent>()
        where TComponent : struct, EcsComponent
    {
        return World.RemoveSingleton<TComponent>();
    }

    /// <summary>Subscribes a directed local event handler for one component type.</summary>
    protected void SubscribeLocalEvent<TComponent, TEvent>(EntityEventHandler<TComponent, TEvent> handler, int order = 0)
        where TComponent : struct, EcsComponent
        where TEvent : EntityEventArgs
    {
        Manager.SubscribeDirected(this, handler, order);
    }

    /// <summary>Subscribes a directed local event handler for one component type with read-only component access.</summary>
    protected void SubscribeReadOnlyLocalEvent<TComponent, TEvent>(ReadOnlyEntityEventHandler<TComponent, TEvent> handler, int order = 0)
        where TComponent : struct, EcsComponent
        where TEvent : EntityEventArgs
    {
        Manager.SubscribeReadOnlyDirected(this, handler, order);
    }

    /// <summary>Subscribes a broadcast local event handler.</summary>
    protected void SubscribeLocalEvent<TEvent>(BroadcastEventHandler<TEvent> handler, int order = 0)
        where TEvent : EntityEventArgs
    {
        Manager.SubscribeBroadcast(this, handler, order);
    }

    /// <summary>Subscribes a directed local event handler for one component type when the payload is a struct passed by ref.</summary>
    protected void SubscribeLocalEvent<TComponent, TEvent>(RefEntityEventHandler<TComponent, TEvent> handler, int order = 0)
        where TComponent : struct, EcsComponent
        where TEvent : struct
    {
        Manager.SubscribeDirected(this, handler, order);
    }

    /// <summary>Subscribes a directed local event handler for one component type with read-only component access when the payload is a struct passed by ref.</summary>
    protected void SubscribeReadOnlyLocalEvent<TComponent, TEvent>(ReadOnlyRefEntityEventHandler<TComponent, TEvent> handler, int order = 0)
        where TComponent : struct, EcsComponent
        where TEvent : struct
    {
        Manager.SubscribeReadOnlyDirected(this, handler, order);
    }

    /// <summary>Subscribes a broadcast local event handler for struct payloads passed by ref.</summary>
    protected void SubscribeLocalEvent<TEvent>(RefBroadcastEventHandler<TEvent> handler, int order = 0)
        where TEvent : struct
    {
        Manager.SubscribeBroadcast(this, handler, order);
    }

    /// <summary>Raises a directed local event on one entity.</summary>
    protected void RaiseLocalEvent<TEvent>(EntityId entity, TEvent args, bool broadcast = false)
        where TEvent : EntityEventArgs
    {
        Manager.RaiseLocalEvent(entity, args, broadcast);
    }

    /// <summary>Raises a broadcast local event.</summary>
    protected void RaiseLocalEvent<TEvent>(TEvent args)
        where TEvent : EntityEventArgs
    {
        Manager.RaiseLocalEvent(args);
    }

    /// <summary>Raises a directed local event with a struct payload passed by ref on one entity.</summary>
    protected void RaiseLocalEvent<TEvent>(EntityId entity, ref TEvent args, bool broadcast = false)
        where TEvent : struct
    {
        Manager.RaiseLocalEvent(entity, ref args, broadcast);
    }

    /// <summary>Raises a broadcast local event with a struct payload passed by ref.</summary>
    protected void RaiseLocalEvent<TEvent>(ref TEvent args)
        where TEvent : struct
    {
        Manager.RaiseLocalEvent(ref args);
    }

    /// <summary>Creates one empty deferred command buffer.</summary>
    protected EntityCommandBuffer CreateCommandBuffer()
    {
        return Manager.CreateCommandBuffer();
    }

    /// <summary>Queues one prepared command buffer for playback after the current system pass.</summary>
    protected void EnqueueCommandBuffer(EntityCommandBuffer commandBuffer)
    {
        Manager.EnqueueCommandBuffer(commandBuffer);
    }

    /// <summary>Queues one entity deletion for deferred playback.</summary>
    protected void QueueDeleteEntity(EntityId entity)
    {
        Manager.QueueDeleteEntity(entity);
    }

    /// <summary>Queues one entity creation for deferred playback.</summary>
    protected void QueueCreateEntity(Action<EntityId>? onCreated = null)
    {
        Manager.QueueCreateEntity(onCreated);
    }

    /// <summary>Queues one prototype-backed entity spawn for deferred playback.</summary>
    protected void QueueSpawnEntity(string prototypeId, Action<EntityId>? onSpawned = null)
    {
        Manager.QueueSpawnEntity(prototypeId, onSpawned);
    }

    /// <summary>Queues one component add for deferred playback.</summary>
    protected void QueueAddComponent<TComponent>(EntityId entity, in TComponent component)
        where TComponent : struct, EcsComponent
    {
        Manager.QueueAddComponent(entity, component);
    }

    /// <summary>Queues one boxed component add for deferred playback.</summary>
    protected void QueueAddComponent(EntityId entity, Type componentType, object component)
    {
        Manager.QueueAddComponent(entity, componentType, component);
    }

    /// <summary>Queues one component removal for deferred playback.</summary>
    protected void QueueRemoveComponent<TComponent>(EntityId entity)
        where TComponent : struct, EcsComponent
    {
        Manager.QueueRemoveComponent<TComponent>(entity);
    }

    /// <summary>Queues one boxed component removal for deferred playback.</summary>
    protected void QueueRemoveComponent(EntityId entity, Type componentType)
    {
        Manager.QueueRemoveComponent(entity, componentType);
    }

    /// <summary>Queues one in-place component overwrite for deferred playback.</summary>
    protected void QueueSetComponent<TComponent>(EntityId entity, in TComponent component)
        where TComponent : struct, EcsComponent
    {
        Manager.QueueSetComponent(entity, component);
    }

    /// <summary>Queues one boxed in-place component overwrite for deferred playback.</summary>
    protected void QueueSetComponent(EntityId entity, Type componentType, object component)
    {
        Manager.QueueSetComponent(entity, componentType, component);
    }

    /// <summary>Queues one directed local event and optionally broadcasts it during deferred playback.</summary>
    protected void QueueLocalEvent<TEvent>(EntityId entity, TEvent args, bool broadcast = false)
        where TEvent : EntityEventArgs
    {
        Manager.QueueLocalEvent(entity, args, broadcast);
    }

    /// <summary>Queues one broadcast local event for deferred playback.</summary>
    protected void QueueLocalEvent<TEvent>(TEvent args)
        where TEvent : EntityEventArgs
    {
        Manager.QueueLocalEvent(args);
    }

    /// <summary>Queues one directed struct event supplied through an in parameter and optionally broadcasts it during deferred playback.</summary>
    protected void QueueLocalEvent<TEvent>(EntityId entity, in TEvent args, bool broadcast = false)
        where TEvent : struct
    {
        Manager.QueueLocalEvent(entity, args, broadcast);
    }

    /// <summary>Queues one broadcast struct event supplied through an in parameter for deferred playback.</summary>
    protected void QueueLocalEvent<TEvent>(in TEvent args)
        where TEvent : struct
    {
        Manager.QueueLocalEvent(args);
    }

    /// <summary>Declares that this system reads one component type.</summary>
    protected void DeclareComponentRead<TComponent>()
        where TComponent : struct, EcsComponent
    {
        _ = _declaredComponentReads.Add(typeof(TComponent));
    }

    /// <summary>Declares that this system writes one component type.</summary>
    protected void DeclareComponentWrite<TComponent>()
        where TComponent : struct, EcsComponent
    {
        _ = _declaredComponentWrites.Add(typeof(TComponent));
    }

    /// <summary>Declares that this system reads one singleton component type.</summary>
    protected void DeclareSingletonRead<TComponent>()
        where TComponent : struct, EcsComponent
    {
        _ = _declaredSingletonReads.Add(typeof(TComponent));
    }

    /// <summary>Declares that this system writes one singleton component type.</summary>
    protected void DeclareSingletonWrite<TComponent>()
        where TComponent : struct, EcsComponent
    {
        _ = _declaredSingletonWrites.Add(typeof(TComponent));
    }

    internal void Attach(EntitySystemManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);
        if (_manager != null)
        {
            throw new InvalidOperationException("Entity system is already attached to a manager.");
        }

        _manager = manager;
    }

    private EntitySystemManager Manager => _manager
        ?? throw new InvalidOperationException("Entity system has not been attached to a system manager.");
}
