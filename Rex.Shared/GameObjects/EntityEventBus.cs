using Rex.Shared.Entities;

namespace Rex.Shared.GameObjects;

/// <summary>
/// Gameplay-facing local event bus backed by an <see cref="EntitySystemManager"/>.
/// </summary>
public sealed class EntityEventBus : IDirectedEventBus
{
    private readonly EntitySystemManager _manager;

    /// <summary>
    /// Creates a local event bus over one entity system manager.
    /// </summary>
    /// <param name="manager">Manager to delegate event dispatch to.</param>
    public EntityEventBus(EntitySystemManager manager)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
    }

    /// <inheritdoc />
    public void RaiseLocalEvent<TEvent>(EntityId entity, TEvent args, bool broadcast = false)
        where TEvent : EntityEventArgs
    {
        _manager.RaiseLocalEvent(entity, args, broadcast);
    }

    /// <inheritdoc />
    public void RaiseLocalEvent<TEvent>(TEvent args)
        where TEvent : EntityEventArgs
    {
        _manager.RaiseLocalEvent(args);
    }

    /// <inheritdoc />
    public void RaiseLocalEvent<TEvent>(EntityId entity, ref TEvent args, bool broadcast = false)
        where TEvent : struct
    {
        _manager.RaiseLocalEvent(entity, ref args, broadcast);
    }

    /// <inheritdoc />
    public void RaiseLocalEvent<TEvent>(ref TEvent args)
        where TEvent : struct
    {
        _manager.RaiseLocalEvent(ref args);
    }

    /// <inheritdoc />
    public void QueueLocalEvent<TEvent>(EntityId entity, TEvent args, bool broadcast = false)
        where TEvent : EntityEventArgs
    {
        _manager.QueueLocalEvent(entity, args, broadcast);
    }

    /// <inheritdoc />
    public void QueueLocalEvent<TEvent>(TEvent args)
        where TEvent : EntityEventArgs
    {
        _manager.QueueLocalEvent(args);
    }

    /// <inheritdoc />
    public void QueueLocalEvent<TEvent>(EntityId entity, in TEvent args, bool broadcast = false)
        where TEvent : struct
    {
        _manager.QueueLocalEvent(entity, args, broadcast);
    }

    /// <inheritdoc />
    public void QueueLocalEvent<TEvent>(in TEvent args)
        where TEvent : struct
    {
        _manager.QueueLocalEvent(args);
    }
}
