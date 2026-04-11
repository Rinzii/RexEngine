namespace Rex.Shared.Entities.Storage;

internal readonly struct ArchetypeSignature : IEquatable<ArchetypeSignature>
{
    public static readonly ArchetypeSignature Empty = new([]);

    private readonly int[] _componentIds;

    public ArchetypeSignature(int[] componentIds)
    {
        _componentIds = componentIds;
    }

    public int Count => _componentIds.Length;

    public ReadOnlySpan<int> ComponentIds => _componentIds;

    public bool Contains(int componentId) => Array.BinarySearch(_componentIds, componentId) >= 0;

    public int IndexOf(int componentId) => Array.BinarySearch(_componentIds, componentId);

    public ArchetypeSignature Add(int componentId)
    {
        int index = Array.BinarySearch(_componentIds, componentId);
        if (index >= 0)
        {
            throw new InvalidOperationException($"Component id {componentId} is already present in the signature.");
        }

        int insertAt = ~index;
        int[] next = new int[_componentIds.Length + 1];
        if (insertAt > 0)
        {
            Array.Copy(_componentIds, next, insertAt);
        }

        next[insertAt] = componentId;

        if (insertAt < _componentIds.Length)
        {
            Array.Copy(_componentIds, insertAt, next, insertAt + 1, _componentIds.Length - insertAt);
        }

        return new ArchetypeSignature(next);
    }

    public ArchetypeSignature Remove(int componentId)
    {
        int index = Array.BinarySearch(_componentIds, componentId);
        if (index < 0)
        {
            throw new InvalidOperationException($"Component id {componentId} is not present in the signature.");
        }

        if (_componentIds.Length == 1)
        {
            return Empty;
        }

        int[] next = new int[_componentIds.Length - 1];
        if (index > 0)
        {
            Array.Copy(_componentIds, next, index);
        }

        if (index < _componentIds.Length - 1)
        {
            Array.Copy(_componentIds, index + 1, next, index, _componentIds.Length - index - 1);
        }

        return new ArchetypeSignature(next);
    }

    public bool Matches(ReadOnlySpan<int> requiredIds, ReadOnlySpan<int> excludedIds)
    {
        foreach (int requiredId in requiredIds)
        {
            if (!Contains(requiredId))
            {
                return false;
            }
        }

        foreach (int excludedId in excludedIds)
        {
            if (Contains(excludedId))
            {
                return false;
            }
        }

        return true;
    }

    public int[] ToArray()
    {
        if (_componentIds.Length == 0)
        {
            return [];
        }

        int[] copy = new int[_componentIds.Length];
        Array.Copy(_componentIds, copy, _componentIds.Length);
        return copy;
    }

    public static ArchetypeSignature FromComponentIds(IEnumerable<int> componentIds)
    {
        ArgumentNullException.ThrowIfNull(componentIds);

        int[] list = componentIds.ToArray();
        Array.Sort(list);

        if (list.Length == 0)
        {
            return Empty;
        }

        int uniqueCount = 1;
        for (int i = 0; i < list.Length; i++)
        {
            if (list[i] <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(componentIds), "Component ids must be positive.");
            }

            if (i == 0)
            {
                continue;
            }

            if (list[i] == list[i - 1])
            {
                continue;
            }

            list[uniqueCount++] = list[i];
        }

        if (uniqueCount == list.Length)
        {
            return new ArchetypeSignature(list);
        }

        int[] unique = new int[uniqueCount];
        Array.Copy(list, unique, uniqueCount);
        return new ArchetypeSignature(unique);
    }

    public bool Equals(ArchetypeSignature other) => _componentIds.AsSpan().SequenceEqual(other._componentIds);

    public override bool Equals(object? obj) => obj is ArchetypeSignature other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (int componentId in _componentIds)
        {
            hash.Add(componentId);
        }

        return hash.ToHashCode();
    }

    public static bool operator ==(ArchetypeSignature left, ArchetypeSignature right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ArchetypeSignature left, ArchetypeSignature right)
    {
        return !left.Equals(right);
    }
}
