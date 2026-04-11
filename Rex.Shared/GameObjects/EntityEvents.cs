using Rex.Shared.Entities;
using IComponent = Rex.Shared.Components.IComponent;

namespace Rex.Shared.GameObjects;

/// <summary>
/// Base class for local ECS events raised through entity systems.
/// </summary>
public abstract class EntityEventArgs;

/// <summary>
/// Base class for local events that can be marked as handled.
/// </summary>
public abstract class HandledEntityEventArgs : EntityEventArgs
{
    /// <summary>Gets a value indicating whether some system handled the event.</summary>
    public bool Handled { get; set; }
}

/// <summary>
/// Base class for local events that can be cancelled.
/// </summary>
public abstract class CancellableEntityEventArgs : HandledEntityEventArgs
{
    /// <summary>Gets a value indicating whether the event was cancelled.</summary>
    public bool Cancelled { get; private set; }

    /// <summary>Cancels the event and marks it handled.</summary>
    public void Cancel()
    {
        Cancelled = true;
        Handled = true;
    }
}

/// <summary>Raised after a component has been added to an entity.</summary>
[ByRefEvent]
public readonly struct ComponentAddedEvent;

/// <summary>Raised after a component has been initialized on an entity.</summary>
[ByRefEvent]
public readonly struct ComponentInitEvent;

/// <summary>Raised after a component has been started on an entity.</summary>
[ByRefEvent]
public readonly struct ComponentStartupEvent;

/// <summary>Raised before a component is removed from an entity.</summary>
[ByRefEvent]
public readonly struct ComponentShutdownEvent;

/// <summary>Raised before a component is removed from an entity.</summary>
[ByRefEvent]
public readonly struct ComponentRemoveEvent;

/// <summary>Raised before an entity is destroyed.</summary>
[ByRefEvent]
public readonly struct EntityTerminatingEvent;

/// <summary>
/// Directed local event handler bound to one component type on an entity.
/// </summary>
/// <typeparam name="TComponent">Component type required on the entity.</typeparam>
/// <typeparam name="TEvent">Event type being handled.</typeparam>
/// <param name="entity">Entity receiving the event.</param>
/// <param name="component">Live component reference.</param>
/// <param name="args">Event payload.</param>
public delegate void EntityEventHandler<TComponent, in TEvent>(EntityId entity, ref TComponent component, TEvent args)
    where TComponent : struct, IComponent
    where TEvent : EntityEventArgs;

/// <summary>
/// Directed local event handler bound to one component type on an entity for read-only component access.
/// </summary>
/// <typeparam name="TComponent">Component type required on the entity.</typeparam>
/// <typeparam name="TEvent">Event type being handled.</typeparam>
/// <param name="entity">Entity receiving the event.</param>
/// <param name="component">Live read-only component reference.</param>
/// <param name="args">Event payload.</param>
public delegate void ReadOnlyEntityEventHandler<TComponent, in TEvent>(
    EntityId entity,
    in TComponent component,
    TEvent args)
    where TComponent : struct, IComponent
    where TEvent : EntityEventArgs;

/// <summary>
/// Directed local event handler bound to one component type on an entity when the payload is a struct passed by ref.
/// </summary>
/// <typeparam name="TComponent">Component type required on the entity.</typeparam>
/// <typeparam name="TEvent">Value event type being handled.</typeparam>
/// <param name="entity">Entity receiving the event.</param>
/// <param name="component">Live component reference.</param>
/// <param name="args">Mutable event payload passed by reference.</param>
public delegate void RefEntityEventHandler<TComponent, TEvent>(EntityId entity, ref TComponent component, ref TEvent args)
    where TComponent : struct, IComponent
    where TEvent : struct;

/// <summary>
/// Directed local event handler bound to one component type on an entity when the payload is a struct passed by ref and the component is read-only.
/// </summary>
/// <typeparam name="TComponent">Component type required on the entity.</typeparam>
/// <typeparam name="TEvent">Value event type being handled.</typeparam>
/// <param name="entity">Entity receiving the event.</param>
/// <param name="component">Live read-only component reference.</param>
/// <param name="args">Mutable event payload passed by reference.</param>
public delegate void ReadOnlyRefEntityEventHandler<TComponent, TEvent>(
    EntityId entity,
    in TComponent component,
    ref TEvent args)
    where TComponent : struct, IComponent
    where TEvent : struct;

/// <summary>
/// Broadcast local event handler.
/// </summary>
/// <typeparam name="TEvent">Event type being handled.</typeparam>
/// <param name="args">Event payload.</param>
public delegate void BroadcastEventHandler<in TEvent>(TEvent args)
    where TEvent : EntityEventArgs;

/// <summary>
/// Broadcast local event handler for struct payloads passed by ref.
/// </summary>
/// <typeparam name="TEvent">Value event type being handled.</typeparam>
/// <param name="args">Mutable event payload passed by reference.</param>
public delegate void RefBroadcastEventHandler<TEvent>(ref TEvent args)
    where TEvent : struct;
