using Rex.Shared.Entities;

namespace Rex.Shared.GameObjects;

/// <summary>
/// Gameplay-facing local event bus for directed and broadcast entity events.
/// </summary>
public interface IDirectedEventBus
{
    /// <summary>Raises one directed local event and optionally broadcasts it.</summary>
    void RaiseLocalEvent<TEvent>(EntityId entity, TEvent args, bool broadcast = false)
        where TEvent : EntityEventArgs;

    /// <summary>Raises one broadcast local event.</summary>
    void RaiseLocalEvent<TEvent>(TEvent args)
        where TEvent : EntityEventArgs;

    /// <summary>Raises one directed local event with a struct payload passed by ref and optionally broadcasts it.</summary>
    void RaiseLocalEvent<TEvent>(EntityId entity, ref TEvent args, bool broadcast = false)
        where TEvent : struct;

    /// <summary>Raises one broadcast local event with a struct payload passed by ref.</summary>
    void RaiseLocalEvent<TEvent>(ref TEvent args)
        where TEvent : struct;

    /// <summary>Queues one directed local event and optionally broadcasts it during deferred playback.</summary>
    void QueueLocalEvent<TEvent>(EntityId entity, TEvent args, bool broadcast = false)
        where TEvent : EntityEventArgs;

    /// <summary>Queues one broadcast local event for deferred playback.</summary>
    void QueueLocalEvent<TEvent>(TEvent args)
        where TEvent : EntityEventArgs;

    /// <summary>Queues one directed struct event supplied through an in parameter and optionally broadcasts it during deferred playback.</summary>
    void QueueLocalEvent<TEvent>(EntityId entity, in TEvent args, bool broadcast = false)
        where TEvent : struct;

    /// <summary>Queues one broadcast struct event supplied through an in parameter for deferred playback.</summary>
    void QueueLocalEvent<TEvent>(in TEvent args)
        where TEvent : struct;
}
