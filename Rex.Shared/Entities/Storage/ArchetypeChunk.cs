using Rex.Shared.Components;

namespace Rex.Shared.Entities.Storage;

internal sealed class ArchetypeChunk
{
    private readonly IComponentColumn[] _columns;
    private readonly EntityId[] _entities;

    public ArchetypeChunk(IComponentColumn[] columns, int capacity)
    {
        ArgumentNullException.ThrowIfNull(columns);
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Chunk capacity must be positive.");
        }

        _columns = columns;
        _entities = new EntityId[capacity];

        foreach (IComponentColumn column in _columns)
        {
            column.EnsureCapacity(capacity);
        }
    }

    public int Count { get; private set; }

    public int Capacity => _entities.Length;

    public bool HasCapacity => Count < Capacity;

    public int AddEntity(EntityId entity)
    {
        if (!HasCapacity)
        {
            throw new InvalidOperationException("The archetype chunk is already full.");
        }

        _entities[Count] = entity;
        return Count++;
    }

    public EntityId GetEntity(int row) => _entities[row];

    public void SetEntity(int row, EntityId entity)
    {
        _entities[row] = entity;
    }

    public void RemoveLastRow()
    {
        int lastRow = Count - 1;
        if (lastRow < 0)
        {
            throw new InvalidOperationException("The archetype chunk is empty.");
        }

        _entities[lastRow] = default;
        foreach (IComponentColumn column in _columns)
        {
            column.Clear(lastRow);
        }

        Count--;
    }

    public ComponentColumn<T> GetColumn<T>(int columnIndex)
        where T : struct, IComponent
    {
        return (ComponentColumn<T>)_columns[columnIndex];
    }

    public IComponentColumn GetColumn(int columnIndex)
    {
        return _columns[columnIndex];
    }
}
