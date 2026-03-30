namespace Rex.Shared.Utility;

/// <summary>
/// Fixed-capacity ring buffer addressed by monotonically increasing tick values.
/// Task-specific wrappers decide how to validate and reset reused slots.
/// </summary>
public sealed class TickRingBuffer<T>
{
    private readonly Entry[] _entries;

    public TickRingBuffer(int capacity)
        : this(capacity, static () => default!)
    {
    }

    public TickRingBuffer(int capacity, Func<T> valueFactory)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        ArgumentNullException.ThrowIfNull(valueFactory);

        _entries = new Entry[capacity];
        for (var i = 0; i < _entries.Length; i++)
        {
            _entries[i] = new Entry(valueFactory());
        }
    }

    public int Capacity => _entries.Length;

    /// <summary>Bucket for this tick. Same bucket recycles as ticks wrap past <see cref="Capacity"/>.</summary>
    public Entry GetSlot(uint tick)
    {
        return _entries[(int)(tick % (uint)_entries.Length)];
    }

    /// <summary>Direct index into the ring. Prefer <see cref="GetSlot(uint)"/> when you have a tick.</summary>
    public Entry GetSlotAt(int index)
    {
        return _entries[index];
    }

    /// <summary>One slot. Callers validate <see cref="Tick"/> matches the tick they asked for.</summary>
    public sealed class Entry
    {
        public Entry(T value)
        {
            Value = value;
        }

        /// <summary>Tick this slot was last written for.</summary>
        public uint Tick { get; set; }

        /// <summary>False if the slot is stale or cleared.</summary>
        public bool IsAssigned { get; set; }

        public T Value { get; set; }
    }
}
