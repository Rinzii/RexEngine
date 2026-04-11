using Rex.Shared.Components;

namespace Rex.Shared.Entities.Storage;

internal interface IComponentColumn
{
    void EnsureCapacity(int capacity);

    void CopyValueTo(int sourceRow, IComponentColumn destination, int destinationRow);

    void SetDefault(int row);

    void Clear(int row);

    object GetBoxed(int row);

    void SetBoxed(int row, object value);

    uint GetVersion(int row);

    void SetVersion(int row, uint version);
}

internal sealed class ComponentColumn<T> : IComponentColumn
    where T : struct, IComponent
{
    private T[] _items = [];
    private uint[] _versions = [];

    public ref T GetRef(int row) => ref _items[row];

    public T Get(int row) => _items[row];

    public void Set(int row, in T value)
    {
        _items[row] = value;
    }

    public void EnsureCapacity(int capacity)
    {
        if (_items.Length >= capacity)
        {
            return;
        }

        int nextCapacity = _items.Length == 0 ? 4 : _items.Length * 2;
        if (nextCapacity < capacity)
        {
            nextCapacity = capacity;
        }

        Array.Resize(ref _items, nextCapacity);
        Array.Resize(ref _versions, nextCapacity);
    }

    public void CopyValueTo(int sourceRow, IComponentColumn destination, int destinationRow)
    {
        var typedDestination = (ComponentColumn<T>)destination;
        typedDestination._items[destinationRow] = _items[sourceRow];
        typedDestination._versions[destinationRow] = _versions[sourceRow];
    }

    public void SetDefault(int row)
    {
        _items[row] = default;
    }

    public void Clear(int row)
    {
        _items[row] = default;
        _versions[row] = default;
    }

    public object GetBoxed(int row) => _items[row];

    public void SetBoxed(int row, object value)
    {
        if (value is not T typed)
        {
            throw new InvalidOperationException(
                $"Cannot assign value of type '{value.GetType().FullName}' to component column '{typeof(T).FullName}'.");
        }

        _items[row] = typed;
    }

    public uint GetVersion(int row)
    {
        return _versions[row];
    }

    public void SetVersion(int row, uint version)
    {
        _versions[row] = version;
    }
}
