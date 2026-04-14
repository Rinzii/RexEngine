namespace Rex.Shared.Utility;

/// <summary>Ring with fixed capacity addressed by monotonic ticks. Wrapper types validate and reset reused slots per scenario.</summary>
/// <typeparam name="T">Stored payload type.</typeparam>
public sealed class TickRingBuffer<T>
{
    private readonly Entry[] _entries;

    /// <summary>Allocates slots using <c>default!</c> for each entry.</summary>
    /// <param name="capacity">Number of ring slots (at least one).</param>
    public TickRingBuffer(int capacity)
        : this(capacity, static () => default!) { }

    /// <summary>Allocates slots using <paramref name="valueFactory"/>.</summary>
    /// <param name="capacity">Number of ring slots (at least one).</param>
    /// <param name="valueFactory">Creates the initial value for each slot.</param>
    public TickRingBuffer(int capacity, Func<T> valueFactory)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        ArgumentNullException.ThrowIfNull(valueFactory);

        _entries = new Entry[capacity];
        for (int i = 0; i < _entries.Length; i++)
        {
            _entries[i] = new Entry(valueFactory());
        }
    }

    /// <summary>Number of slots in the ring.</summary>
    public int Capacity => _entries.Length;

    /// <summary>Bucket for this tick. Same bucket recycles as ticks wrap past <see cref="Capacity"/>.</summary>
    /// <param name="tick">Monotonic tick used as the address.</param>
    public Entry GetSlot(uint tick)
    {
        return _entries[(int)(tick % (uint)_entries.Length)];
    }

    /// <summary>Direct index into the ring. Prefer <see cref="GetSlot(uint)"/> when you have a tick.</summary>
    /// <param name="index">Index into the backing array starting at zero.</param>
    public Entry GetSlotAt(int index)
    {
        return _entries[index];
    }

    /// <summary>One slot. Callers validate <see cref="Tick"/> matches the tick they asked for.</summary>
    public sealed class Entry
    {
        /// <summary>Wraps an initial <paramref name="value"/>.</summary>
        public Entry(T value)
        {
            Value = value;
        }

        /// <summary>Tick this slot was last written for.</summary>
        public uint Tick { get; set; }

        /// <summary>False if the slot is stale or cleared.</summary>
        public bool IsAssigned { get; set; }

        /// <summary>Payload stored in this slot.</summary>
        public T Value { get; set; }
    }
}
