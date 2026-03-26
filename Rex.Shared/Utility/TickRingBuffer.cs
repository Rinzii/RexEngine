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

    public Entry GetSlot(uint tick)
    {
        return _entries[(int)(tick % (uint)_entries.Length)];
    }

    public Entry GetSlotAt(int index)
    {
        return _entries[index];
    }

    public sealed class Entry
    {
        public Entry(T value)
        {
            Value = value;
        }

        public uint Tick { get; set; }
        public bool IsAssigned { get; set; }
        public T Value { get; set; }
    }
}
