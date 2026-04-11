using Rex.Shared.Entities;
using EcsComponent = Rex.Shared.Components.IComponent;

namespace Rex.Shared.GameObjects;

/// <summary>
/// Records gameplay-facing ECS work for deferred playback through an <see cref="EntityManager"/> or
/// <see cref="EntitySystemManager"/>.
/// </summary>
/// <remarks>
/// Commands replay in list order inside one buffer. Each row runs synchronously through the manager so lifecycles
/// and local events for earlier rows finish before later rows run. <see cref="DeferredEntity"/> tokens bind when
/// create or spawn commands run so later commands in the same buffer resolve real <see cref="EntityId"/> values.
/// After <see cref="Playback"/> this instance cannot record again. Allocate a new buffer for new work.
/// </remarks>
public sealed class EntityCommandBuffer
{
    private readonly EntitySystemManager? _owner;
    private readonly List<ICommand> _commands = [];
    private int _nextDeferredEntityToken;
    private CommandBufferState _state;

    /// <summary>Creates one detached command buffer.</summary>
    public EntityCommandBuffer()
    {
    }

    internal EntityCommandBuffer(EntitySystemManager owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    /// <summary>Gets a value indicating whether the buffer contains no recorded work.</summary>
    public bool IsEmpty => _commands.Count == 0;

    /// <summary>Enqueues this buffer on its owning system manager.</summary>
    public void Enqueue()
    {
        if (_owner == null)
        {
            throw new InvalidOperationException(
                "This command buffer is not attached to an entity-system manager. Use EntityManager or EntitySystemManager to enqueue it.");
        }

        _owner.EnqueueCommandBuffer(this);
    }

    /// <summary>Queues one entity deletion.</summary>
    /// <param name="entity">Entity to delete during playback.</param>
    public void DeleteEntity(EntityId entity)
    {
        EnsureWritable();
        _commands.Add(new DeleteEntityCommand(CommandTarget.ForEntity(entity)));
    }

    /// <summary>Queues one deferred entity deletion.</summary>
    /// <param name="entity">Deferred entity placeholder to delete during playback.</param>
    public void DeleteEntity(DeferredEntity entity)
    {
        EnsureWritable();
        _commands.Add(new DeleteEntityCommand(CommandTarget.ForDeferredEntity(entity)));
    }

    /// <summary>Queues one entity creation.</summary>
    /// <param name="onCreated">Optional callback invoked with the created entity during playback.</param>
    public void CreateEntity(Action<EntityId>? onCreated = null)
    {
        _ = CreateDeferredEntity(onCreated);
    }

    /// <summary>Queues one entity creation and returns a placeholder for later commands in the same buffer.</summary>
    /// <param name="onCreated">Optional callback invoked with the created entity during playback.</param>
    /// <returns>Deferred placeholder representing the created entity.</returns>
    public DeferredEntity CreateDeferredEntity(Action<EntityId>? onCreated = null)
    {
        EnsureWritable();
        DeferredEntity deferredEntity = AllocateDeferredEntity();
        _commands.Add(new CreateEntityCommand(deferredEntity, onCreated));
        return deferredEntity;
    }

    /// <summary>Queues one prototype-backed entity spawn.</summary>
    /// <param name="prototypeId">Prototype id to spawn during playback.</param>
    /// <param name="onSpawned">Optional callback invoked with the spawned entity during playback.</param>
    public void SpawnEntity(string prototypeId, Action<EntityId>? onSpawned = null)
    {
        _ = SpawnDeferredEntity(prototypeId, onSpawned);
    }

    /// <summary>Queues one prototype-backed entity spawn and returns a placeholder for later commands in the same buffer.</summary>
    /// <param name="prototypeId">Prototype id to spawn during playback.</param>
    /// <param name="onSpawned">Optional callback invoked with the spawned entity during playback.</param>
    /// <returns>Deferred placeholder representing the spawned entity.</returns>
    public DeferredEntity SpawnDeferredEntity(string prototypeId, Action<EntityId>? onSpawned = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prototypeId);
        EnsureWritable();
        DeferredEntity deferredEntity = AllocateDeferredEntity();
        _commands.Add(new SpawnEntityCommand(deferredEntity, prototypeId, onSpawned));
        return deferredEntity;
    }

    /// <summary>Queues one component add.</summary>
    /// <param name="entity">Entity to update.</param>
    /// <param name="component">Component to add during playback.</param>
    /// <typeparam name="TComponent">Component type being added.</typeparam>
    public void AddComponent<TComponent>(EntityId entity, in TComponent component)
        where TComponent : struct, EcsComponent
    {
        EnsureWritable();
        _commands.Add(new AddComponentCommand<TComponent>(CommandTarget.ForEntity(entity), component));
    }

    /// <summary>Queues one component add against a deferred entity placeholder.</summary>
    /// <param name="entity">Deferred entity placeholder to update.</param>
    /// <param name="component">Component to add during playback.</param>
    /// <typeparam name="TComponent">Component type being added.</typeparam>
    public void AddComponent<TComponent>(DeferredEntity entity, in TComponent component)
        where TComponent : struct, EcsComponent
    {
        EnsureWritable();
        _commands.Add(new AddComponentCommand<TComponent>(CommandTarget.ForDeferredEntity(entity), component));
    }

    /// <summary>Queues one boxed component add.</summary>
    /// <param name="entity">Entity to update.</param>
    /// <param name="componentType">Registered component type being added.</param>
    /// <param name="component">Component value to add during playback.</param>
    public void AddComponent(EntityId entity, Type componentType, object component)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        ArgumentNullException.ThrowIfNull(component);
        EnsureWritable();
        _commands.Add(new AddBoxedComponentCommand(CommandTarget.ForEntity(entity), componentType, component));
    }

    /// <summary>Queues one boxed component add against a deferred entity placeholder.</summary>
    /// <param name="entity">Deferred entity placeholder to update.</param>
    /// <param name="componentType">Registered component type being added.</param>
    /// <param name="component">Component value to add during playback.</param>
    public void AddComponent(DeferredEntity entity, Type componentType, object component)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        ArgumentNullException.ThrowIfNull(component);
        EnsureWritable();
        _commands.Add(new AddBoxedComponentCommand(CommandTarget.ForDeferredEntity(entity), componentType, component));
    }

    /// <summary>Queues one component removal.</summary>
    /// <param name="entity">Entity to update.</param>
    /// <typeparam name="TComponent">Component type being removed.</typeparam>
    public void RemoveComponent<TComponent>(EntityId entity)
        where TComponent : struct, EcsComponent
    {
        EnsureWritable();
        _commands.Add(new RemoveComponentCommand<TComponent>(CommandTarget.ForEntity(entity)));
    }

    /// <summary>Queues one component removal against a deferred entity placeholder.</summary>
    /// <param name="entity">Deferred entity placeholder to update.</param>
    /// <typeparam name="TComponent">Component type being removed.</typeparam>
    public void RemoveComponent<TComponent>(DeferredEntity entity)
        where TComponent : struct, EcsComponent
    {
        EnsureWritable();
        _commands.Add(new RemoveComponentCommand<TComponent>(CommandTarget.ForDeferredEntity(entity)));
    }

    /// <summary>Queues one boxed component removal.</summary>
    /// <param name="entity">Entity to update.</param>
    /// <param name="componentType">Registered component type being removed.</param>
    public void RemoveComponent(EntityId entity, Type componentType)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        EnsureWritable();
        _commands.Add(new RemoveBoxedComponentCommand(CommandTarget.ForEntity(entity), componentType));
    }

    /// <summary>Queues one boxed component removal against a deferred entity placeholder.</summary>
    /// <param name="entity">Deferred entity placeholder to update.</param>
    /// <param name="componentType">Registered component type being removed.</param>
    public void RemoveComponent(DeferredEntity entity, Type componentType)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        EnsureWritable();
        _commands.Add(new RemoveBoxedComponentCommand(CommandTarget.ForDeferredEntity(entity), componentType));
    }

    /// <summary>Queues one in-place component overwrite.</summary>
    /// <param name="entity">Entity to update.</param>
    /// <param name="component">Replacement component value.</param>
    /// <typeparam name="TComponent">Component type being updated.</typeparam>
    public void SetComponent<TComponent>(EntityId entity, in TComponent component)
        where TComponent : struct, EcsComponent
    {
        EnsureWritable();
        _commands.Add(new SetComponentCommand<TComponent>(CommandTarget.ForEntity(entity), component));
    }

    /// <summary>Queues one component overwrite against a deferred entity placeholder.</summary>
    /// <param name="entity">Deferred entity placeholder to update.</param>
    /// <param name="component">Replacement component value.</param>
    /// <typeparam name="TComponent">Component type being updated.</typeparam>
    public void SetComponent<TComponent>(DeferredEntity entity, in TComponent component)
        where TComponent : struct, EcsComponent
    {
        EnsureWritable();
        _commands.Add(new SetComponentCommand<TComponent>(CommandTarget.ForDeferredEntity(entity), component));
    }

    /// <summary>Queues one boxed in-place component overwrite.</summary>
    /// <param name="entity">Entity to update.</param>
    /// <param name="componentType">Registered component type being updated.</param>
    /// <param name="component">Replacement component value.</param>
    public void SetComponent(EntityId entity, Type componentType, object component)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        ArgumentNullException.ThrowIfNull(component);
        EnsureWritable();
        _commands.Add(new SetBoxedComponentCommand(CommandTarget.ForEntity(entity), componentType, component));
    }

    /// <summary>Queues one boxed component overwrite against a deferred entity placeholder.</summary>
    /// <param name="entity">Deferred entity placeholder to update.</param>
    /// <param name="componentType">Registered component type being updated.</param>
    /// <param name="component">Replacement component value.</param>
    public void SetComponent(DeferredEntity entity, Type componentType, object component)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        ArgumentNullException.ThrowIfNull(component);
        EnsureWritable();
        _commands.Add(new SetBoxedComponentCommand(CommandTarget.ForDeferredEntity(entity), componentType, component));
    }

    /// <summary>Queues one directed local event and optionally broadcasts it.</summary>
    /// <param name="entity">Entity that receives the event.</param>
    /// <param name="args">Event payload to dispatch during playback.</param>
    /// <param name="broadcast"><see langword="true"/> to also broadcast after directed dispatch.</param>
    /// <typeparam name="TEvent">Reference event type being queued.</typeparam>
    public void RaiseLocalEvent<TEvent>(EntityId entity, TEvent args, bool broadcast = false)
        where TEvent : EntityEventArgs
    {
        ArgumentNullException.ThrowIfNull(args);
        EnsureWritable();
        _commands.Add(new DirectedReferenceEventCommand<TEvent>(CommandTarget.ForEntity(entity), args, broadcast));
    }

    /// <summary>Queues one directed local event against a deferred entity placeholder and optionally broadcasts it.</summary>
    /// <param name="entity">Deferred entity placeholder that receives the event.</param>
    /// <param name="args">Event payload to dispatch during playback.</param>
    /// <param name="broadcast"><see langword="true"/> to also broadcast after directed dispatch.</param>
    /// <typeparam name="TEvent">Reference event type being queued.</typeparam>
    public void RaiseLocalEvent<TEvent>(DeferredEntity entity, TEvent args, bool broadcast = false)
        where TEvent : EntityEventArgs
    {
        ArgumentNullException.ThrowIfNull(args);
        EnsureWritable();
        _commands.Add(new DirectedReferenceEventCommand<TEvent>(CommandTarget.ForDeferredEntity(entity), args, broadcast));
    }

    /// <summary>Queues one broadcast local event.</summary>
    /// <param name="args">Event payload to broadcast during playback.</param>
    /// <typeparam name="TEvent">Reference event type being queued.</typeparam>
    public void RaiseLocalEvent<TEvent>(TEvent args)
        where TEvent : EntityEventArgs
    {
        ArgumentNullException.ThrowIfNull(args);
        EnsureWritable();
        _commands.Add(new BroadcastReferenceEventCommand<TEvent>(args));
    }

    /// <summary>Queues one directed struct event supplied through an in parameter and optionally broadcasts it.</summary>
    /// <param name="entity">Entity that receives the event.</param>
    /// <param name="args">Event payload copied into the queue.</param>
    /// <param name="broadcast"><see langword="true"/> to also broadcast after directed dispatch.</param>
    /// <typeparam name="TEvent">Value event type being queued.</typeparam>
    public void RaiseLocalEvent<TEvent>(EntityId entity, in TEvent args, bool broadcast = false)
        where TEvent : struct
    {
        EnsureWritable();
        _commands.Add(new DirectedValueEventCommand<TEvent>(CommandTarget.ForEntity(entity), args, broadcast));
    }

    /// <summary>Queues one directed struct event for a deferred entity placeholder supplied through an in parameter and optionally broadcasts it.</summary>
    /// <param name="entity">Deferred entity placeholder that receives the event.</param>
    /// <param name="args">Event payload copied into the queue.</param>
    /// <param name="broadcast"><see langword="true"/> to also broadcast after directed dispatch.</param>
    /// <typeparam name="TEvent">Value event type being queued.</typeparam>
    public void RaiseLocalEvent<TEvent>(DeferredEntity entity, in TEvent args, bool broadcast = false)
        where TEvent : struct
    {
        EnsureWritable();
        _commands.Add(new DirectedValueEventCommand<TEvent>(CommandTarget.ForDeferredEntity(entity), args, broadcast));
    }

    /// <summary>Queues one broadcast struct event supplied through an in parameter.</summary>
    /// <param name="args">Event payload copied into the queue.</param>
    /// <typeparam name="TEvent">Value event type being queued.</typeparam>
    public void RaiseLocalEvent<TEvent>(in TEvent args)
        where TEvent : struct
    {
        EnsureWritable();
        _commands.Add(new BroadcastValueEventCommand<TEvent>(args));
    }

    internal void Playback(EntitySystemManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);
        if (_state != CommandBufferState.Enqueued)
        {
            throw new InvalidOperationException("Only enqueued command buffers can be played back.");
        }

        PlaybackContext playbackContext = new();

        // One linear pass. Each Invoke hits the manager immediately so later rows see world effects from earlier rows.
        // Earlier create or spawn rows bind deferred tokens so placeholders in later rows resolve to real ids.
        foreach (ICommand command in _commands)
        {
            command.Invoke(manager, playbackContext);
        }

        _commands.Clear();
        _state = CommandBufferState.PlayedBack;
    }

    internal void SealForEnqueue()
    {
        if (IsEmpty)
        {
            return;
        }

        if (_state != CommandBufferState.Recording)
        {
            throw new InvalidOperationException("Command buffers can only be enqueued once.");
        }

        // Recording stops here so the same instance cannot be sealed twice or mutate after it enters the global queue.
        _state = CommandBufferState.Enqueued;
    }

    private DeferredEntity AllocateDeferredEntity()
    {
        _nextDeferredEntityToken++;
        // Token index becomes the row key inside PlaybackContext during replay.
        return DeferredEntity.FromToken(_nextDeferredEntityToken);
    }

    private void EnsureWritable()
    {
        if (_state != CommandBufferState.Recording)
        {
            throw new InvalidOperationException("Command buffers cannot be modified after they have been enqueued.");
        }
    }

    private enum CommandBufferState
    {
        Recording,
        Enqueued,
        PlayedBack
    }

    private interface ICommand
    {
        void Invoke(EntitySystemManager manager, PlaybackContext playbackContext);
    }

    private sealed class DeleteEntityCommand : ICommand
    {
        private readonly CommandTarget _entity;

        public DeleteEntityCommand(CommandTarget entity)
        {
            _entity = entity;
        }

        public void Invoke(EntitySystemManager manager, PlaybackContext playbackContext)
        {
            _ = manager.DestroyEntity(_entity.Resolve(playbackContext));
        }
    }

    private sealed class CreateEntityCommand : ICommand
    {
        private readonly DeferredEntity _deferredEntity;
        private readonly Action<EntityId>? _onCreated;

        public CreateEntityCommand(DeferredEntity deferredEntity, Action<EntityId>? onCreated)
        {
            _deferredEntity = deferredEntity;
            _onCreated = onCreated;
        }

        public void Invoke(EntitySystemManager manager, PlaybackContext playbackContext)
        {
            EntityId entity = manager.CreateEntity();
            playbackContext.Bind(_deferredEntity, entity);
            _onCreated?.Invoke(entity);
        }
    }

    private sealed class SpawnEntityCommand : ICommand
    {
        private readonly DeferredEntity _deferredEntity;
        private readonly string _prototypeId;
        private readonly Action<EntityId>? _onSpawned;

        public SpawnEntityCommand(DeferredEntity deferredEntity, string prototypeId, Action<EntityId>? onSpawned)
        {
            _deferredEntity = deferredEntity;
            _prototypeId = prototypeId;
            _onSpawned = onSpawned;
        }

        public void Invoke(EntitySystemManager manager, PlaybackContext playbackContext)
        {
            EntityId entity = manager.SpawnEntity(_prototypeId);
            playbackContext.Bind(_deferredEntity, entity);
            _onSpawned?.Invoke(entity);
        }
    }

    private sealed class AddComponentCommand<TComponent> : ICommand
        where TComponent : struct, EcsComponent
    {
        private readonly CommandTarget _entity;
        private readonly TComponent _component;

        public AddComponentCommand(CommandTarget entity, in TComponent component)
        {
            _entity = entity;
            _component = component;
        }

        public void Invoke(EntitySystemManager manager, PlaybackContext playbackContext)
        {
            manager.AddComponent(_entity.Resolve(playbackContext), _component);
        }
    }

    private sealed class AddBoxedComponentCommand : ICommand
    {
        private readonly CommandTarget _entity;
        private readonly Type _componentType;
        private readonly object _component;

        public AddBoxedComponentCommand(CommandTarget entity, Type componentType, object component)
        {
            _entity = entity;
            _componentType = componentType;
            _component = component;
        }

        public void Invoke(EntitySystemManager manager, PlaybackContext playbackContext)
        {
            manager.AddComponent(_entity.Resolve(playbackContext), _componentType, _component);
        }
    }

    private sealed class RemoveComponentCommand<TComponent> : ICommand
        where TComponent : struct, EcsComponent
    {
        private readonly CommandTarget _entity;

        public RemoveComponentCommand(CommandTarget entity)
        {
            _entity = entity;
        }

        public void Invoke(EntitySystemManager manager, PlaybackContext playbackContext)
        {
            _ = manager.RemoveComponent<TComponent>(_entity.Resolve(playbackContext));
        }
    }

    private sealed class RemoveBoxedComponentCommand : ICommand
    {
        private readonly CommandTarget _entity;
        private readonly Type _componentType;

        public RemoveBoxedComponentCommand(CommandTarget entity, Type componentType)
        {
            _entity = entity;
            _componentType = componentType;
        }

        public void Invoke(EntitySystemManager manager, PlaybackContext playbackContext)
        {
            _ = manager.RemoveComponent(_entity.Resolve(playbackContext), _componentType);
        }
    }

    private sealed class SetComponentCommand<TComponent> : ICommand
        where TComponent : struct, EcsComponent
    {
        private readonly CommandTarget _entity;
        private readonly TComponent _component;

        public SetComponentCommand(CommandTarget entity, in TComponent component)
        {
            _entity = entity;
            _component = component;
        }

        public void Invoke(EntitySystemManager manager, PlaybackContext playbackContext)
        {
            manager.World.Set(_entity.Resolve(playbackContext), _component);
        }
    }

    private sealed class SetBoxedComponentCommand : ICommand
    {
        private readonly CommandTarget _entity;
        private readonly Type _componentType;
        private readonly object _component;

        public SetBoxedComponentCommand(CommandTarget entity, Type componentType, object component)
        {
            _entity = entity;
            _componentType = componentType;
            _component = component;
        }

        public void Invoke(EntitySystemManager manager, PlaybackContext playbackContext)
        {
            manager.SetComponent(_entity.Resolve(playbackContext), _componentType, _component);
        }
    }

    private sealed class DirectedReferenceEventCommand<TEvent> : ICommand
        where TEvent : EntityEventArgs
    {
        private readonly CommandTarget _entity;
        private readonly TEvent _args;
        private readonly bool _broadcast;

        public DirectedReferenceEventCommand(CommandTarget entity, TEvent args, bool broadcast)
        {
            _entity = entity;
            _args = args;
            _broadcast = broadcast;
        }

        public void Invoke(EntitySystemManager manager, PlaybackContext playbackContext)
        {
            manager.RaiseLocalEvent(_entity.Resolve(playbackContext), _args, _broadcast);
        }
    }

    private sealed class BroadcastReferenceEventCommand<TEvent> : ICommand
        where TEvent : EntityEventArgs
    {
        private readonly TEvent _args;

        public BroadcastReferenceEventCommand(TEvent args)
        {
            _args = args;
        }

        public void Invoke(EntitySystemManager manager, PlaybackContext playbackContext)
        {
            // No entity resolution. Parameter stays for the same ICommand signature as directed commands.
            _ = playbackContext;
            manager.RaiseLocalEvent(_args);
        }
    }

    private sealed class DirectedValueEventCommand<TEvent> : ICommand
        where TEvent : struct
    {
        private readonly CommandTarget _entity;
        private readonly TEvent _args;
        private readonly bool _broadcast;

        public DirectedValueEventCommand(CommandTarget entity, in TEvent args, bool broadcast)
        {
            _entity = entity;
            _args = args;
            _broadcast = broadcast;
        }

        public void Invoke(EntitySystemManager manager, PlaybackContext playbackContext)
        {
            TEvent args = _args;
            manager.RaiseLocalEvent(_entity.Resolve(playbackContext), ref args, _broadcast);
        }
    }

    private sealed class BroadcastValueEventCommand<TEvent> : ICommand
        where TEvent : struct
    {
        private readonly TEvent _args;

        public BroadcastValueEventCommand(in TEvent args)
        {
            _args = args;
        }

        public void Invoke(EntitySystemManager manager, PlaybackContext playbackContext)
        {
            // No entity resolution. Parameter stays for the same ICommand signature as directed commands.
            _ = playbackContext;
            TEvent args = _args;
            manager.RaiseLocalEvent(ref args);
        }
    }

    private readonly struct CommandTarget
    {
        private CommandTarget(EntityId entity, DeferredEntity deferredEntity, bool usesDeferredEntity)
        {
            Entity = entity;
            DeferredEntity = deferredEntity;
            UsesDeferredEntity = usesDeferredEntity;
        }

        private EntityId Entity { get; }

        private DeferredEntity DeferredEntity { get; }

        private bool UsesDeferredEntity { get; }

        public static CommandTarget ForEntity(EntityId entity)
        {
            return new CommandTarget(entity, DeferredEntity.Invalid, usesDeferredEntity: false);
        }

        public static CommandTarget ForDeferredEntity(DeferredEntity deferredEntity)
        {
            if (!deferredEntity.IsValid)
            {
                throw new InvalidOperationException("Deferred entity placeholder is invalid.");
            }

            return new CommandTarget(EntityId.Invalid, deferredEntity, usesDeferredEntity: true);
        }

        public EntityId Resolve(PlaybackContext playbackContext)
        {
            ArgumentNullException.ThrowIfNull(playbackContext);
            return UsesDeferredEntity
                ? playbackContext.Resolve(DeferredEntity)
                : Entity;
        }
    }

    private sealed class PlaybackContext
    {
        // DeferredEntity.Index maps into this list. Invalid slots mean create or spawn has not run yet.
        private readonly List<EntityId> _deferredEntities = [];

        public void Bind(DeferredEntity deferredEntity, EntityId entity)
        {
            EnsureCapacity(deferredEntity.Index);
            _deferredEntities[deferredEntity.Index] = entity;
        }

        public EntityId Resolve(DeferredEntity deferredEntity)
        {
            if (!deferredEntity.IsValid)
            {
                throw new InvalidOperationException("Deferred entity placeholder is invalid.");
            }

            if (deferredEntity.Index >= _deferredEntities.Count)
            {
                throw new InvalidOperationException(
                    $"Deferred entity '{deferredEntity}' has not been materialized yet.");
            }

            EntityId entity = _deferredEntities[deferredEntity.Index];
            if (entity == EntityId.Invalid)
            {
                throw new InvalidOperationException(
                    $"Deferred entity '{deferredEntity}' has not been materialized yet.");
            }

            return entity;
        }

        private void EnsureCapacity(int index)
        {
            while (_deferredEntities.Count <= index)
            {
                _deferredEntities.Add(EntityId.Invalid);
            }
        }
    }
}
