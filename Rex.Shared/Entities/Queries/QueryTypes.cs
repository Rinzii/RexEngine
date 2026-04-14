using Rex.Shared.Components;
using Rex.Shared.Entities.Storage;
using Rex.Shared.Entities.World;

namespace Rex.Shared.Entities.Queries;

/// <summary>Allocation-free query view over entities containing one required component type.</summary>
/// <typeparam name="T1">Required component type.</typeparam>
public readonly struct ComponentQuery<T1>
    where T1 : struct, IComponent
{
    private readonly EcsWorld _world;
    private readonly QueryDescription _description;

    internal ComponentQuery(EcsWorld world, QueryDescription description)
    {
        _world = world;
        _description = description;
    }

    /// <summary>Creates the query enumerator.</summary>
    public ComponentQueryEnumerator<T1> GetEnumerator() => new(_world, _description);
}

/// <summary>Allocation-free enumerator over entities containing one required component type.</summary>
public ref struct ComponentQueryEnumerator<T1>
    where T1 : struct, IComponent
{
    private readonly EcsWorld _world;
    private readonly CompiledQueryPlan _plan;
    private readonly int _structuralChangeVersion;
    private QueryArchetypePlan? _currentArchetypePlan;
    private ArchetypeChunk? _currentChunk;
    private ComponentColumn<T1>? _column1;
    private int _archetypeIndex;
    private int _chunkIndex;
    private int _row;

    internal ComponentQueryEnumerator(EcsWorld world, QueryDescription description)
    {
        _world = world;
        int componentId1 = world.Registry.GetComponentId<T1>();
        _plan = world.GetCompiledQueryPlan(description, componentId1);
        _structuralChangeVersion = world.StructuralChangeVersion;
        _currentArchetypePlan = null;
        _currentChunk = null;
        _column1 = null;
        _archetypeIndex = -1;
        _chunkIndex = -1;
        _row = -1;
    }

    /// <summary>Gets the current entity id.</summary>
    public readonly EntityId Current => Entity;

    /// <summary>Gets the current entity id.</summary>
    public readonly EntityId Entity
    {
        get
        {
            EnsureCurrentRow();
            return _currentChunk!.GetEntity(_row);
        }
    }

    /// <summary>Gets a read-only reference to the first component on the current entity.</summary>
    public readonly ref readonly T1 Component1
    {
        get
        {
            EnsureCurrentRow();
            return ref _column1!.GetRef(_row);
        }
    }

    /// <summary>Gets a writable reference to the first component on the current entity.</summary>
    public readonly ref T1 MutableComponent1
    {
        get
        {
            EnsureCurrentRow();
            _column1!.SetVersion(_row, _world.ReserveChangeVersion());
            return ref _column1!.GetRef(_row);
        }
    }

    /// <summary>Advances to the next matching entity row.</summary>
    public bool MoveNext()
    {
        EnsureStructuralConsistency();

        while (true)
        {
            if (_currentChunk != null && ++_row < _currentChunk.Count)
            {
                return true;
            }

            if (MoveNextChunk())
            {
                continue;
            }

            return false;
        }
    }

    private bool MoveNextChunk()
    {
        while (true)
        {
            if (_currentArchetypePlan != null && ++_chunkIndex < _currentArchetypePlan.Archetype.ChunkCount)
            {
                ArchetypeChunk chunk = _currentArchetypePlan.Archetype.GetChunk(_chunkIndex);
                if (chunk.Count == 0)
                {
                    continue;
                }

                _currentChunk = chunk;
                _column1 = chunk.GetColumn<T1>(_currentArchetypePlan.AccessColumnIndexes[0]);
                _row = -1;
                return true;
            }

            _archetypeIndex++;
            if (_archetypeIndex >= _plan.Archetypes.Length)
            {
                _currentArchetypePlan = null;
                _currentChunk = null;
                return false;
            }

            _currentArchetypePlan = _plan.Archetypes[_archetypeIndex];
            _chunkIndex = -1;
        }
    }

    private readonly void EnsureCurrentRow()
    {
        EnsureStructuralConsistency();
        if (_currentChunk == null || _row < 0 || _row >= _currentChunk.Count)
        {
            throw new InvalidOperationException("The query enumerator is not positioned on a live row.");
        }
    }

    private readonly void EnsureStructuralConsistency()
    {
        if (_world.StructuralChangeVersion != _structuralChangeVersion)
        {
            throw new InvalidOperationException(
                "The ECS world changed structurally during query iteration. Use deferred commands or create a new query enumerator.");
        }
    }
}

/// <summary>Allocation-free query view over entities containing two required component types.</summary>
public readonly struct ComponentQuery<T1, T2>
    where T1 : struct, IComponent
    where T2 : struct, IComponent
{
    private readonly EcsWorld _world;
    private readonly QueryDescription _description;

    internal ComponentQuery(EcsWorld world, QueryDescription description)
    {
        _world = world;
        _description = description;
    }

    /// <summary>Creates the query enumerator.</summary>
    public ComponentQueryEnumerator<T1, T2> GetEnumerator() => new(_world, _description);
}

/// <summary>Allocation-free enumerator over entities containing two required component types.</summary>
public ref struct ComponentQueryEnumerator<T1, T2>
    where T1 : struct, IComponent
    where T2 : struct, IComponent
{
    private readonly EcsWorld _world;
    private readonly CompiledQueryPlan _plan;
    private readonly int _structuralChangeVersion;
    private QueryArchetypePlan? _currentArchetypePlan;
    private ArchetypeChunk? _currentChunk;
    private ComponentColumn<T1>? _column1;
    private ComponentColumn<T2>? _column2;
    private int _archetypeIndex;
    private int _chunkIndex;
    private int _row;

    internal ComponentQueryEnumerator(EcsWorld world, QueryDescription description)
    {
        _world = world;
        int componentId1 = world.Registry.GetComponentId<T1>();
        int componentId2 = world.Registry.GetComponentId<T2>();
        _plan = world.GetCompiledQueryPlan(
            description,
            componentId1,
            componentId2);
        _structuralChangeVersion = world.StructuralChangeVersion;
        _currentArchetypePlan = null;
        _currentChunk = null;
        _column1 = null;
        _column2 = null;
        _archetypeIndex = -1;
        _chunkIndex = -1;
        _row = -1;
    }

    /// <summary>Gets the current entity id.</summary>
    public readonly EntityId Current => Entity;

    /// <summary>Gets the current entity id.</summary>
    public readonly EntityId Entity
    {
        get
        {
            EnsureCurrentRow();
            return _currentChunk!.GetEntity(_row);
        }
    }

    /// <summary>Gets a read-only reference to the first component on the current entity.</summary>
    public readonly ref readonly T1 Component1
    {
        get
        {
            EnsureCurrentRow();
            return ref _column1!.GetRef(_row);
        }
    }

    /// <summary>Gets a writable reference to the first component on the current entity.</summary>
    public readonly ref T1 MutableComponent1
    {
        get
        {
            EnsureCurrentRow();
            _column1!.SetVersion(_row, _world.ReserveChangeVersion());
            return ref _column1!.GetRef(_row);
        }
    }

    /// <summary>Gets a read-only reference to the second component on the current entity.</summary>
    public readonly ref readonly T2 Component2
    {
        get
        {
            EnsureCurrentRow();
            return ref _column2!.GetRef(_row);
        }
    }

    /// <summary>Gets a writable reference to the second component on the current entity.</summary>
    public readonly ref T2 MutableComponent2
    {
        get
        {
            EnsureCurrentRow();
            _column2!.SetVersion(_row, _world.ReserveChangeVersion());
            return ref _column2!.GetRef(_row);
        }
    }

    /// <summary>Advances to the next matching entity row.</summary>
    public bool MoveNext()
    {
        EnsureStructuralConsistency();

        while (true)
        {
            if (_currentChunk != null && ++_row < _currentChunk.Count)
            {
                return true;
            }

            if (MoveNextChunk())
            {
                continue;
            }

            return false;
        }
    }

    private bool MoveNextChunk()
    {
        while (true)
        {
            if (_currentArchetypePlan != null && ++_chunkIndex < _currentArchetypePlan.Archetype.ChunkCount)
            {
                ArchetypeChunk chunk = _currentArchetypePlan.Archetype.GetChunk(_chunkIndex);
                if (chunk.Count == 0)
                {
                    continue;
                }

                _currentChunk = chunk;
                _column1 = chunk.GetColumn<T1>(_currentArchetypePlan.AccessColumnIndexes[0]);
                _column2 = chunk.GetColumn<T2>(_currentArchetypePlan.AccessColumnIndexes[1]);
                _row = -1;
                return true;
            }

            _archetypeIndex++;
            if (_archetypeIndex >= _plan.Archetypes.Length)
            {
                _currentArchetypePlan = null;
                _currentChunk = null;
                return false;
            }

            _currentArchetypePlan = _plan.Archetypes[_archetypeIndex];
            _chunkIndex = -1;
        }
    }

    private readonly void EnsureCurrentRow()
    {
        EnsureStructuralConsistency();
        if (_currentChunk == null || _row < 0 || _row >= _currentChunk.Count)
        {
            throw new InvalidOperationException("The query enumerator is not positioned on a live row.");
        }
    }

    private readonly void EnsureStructuralConsistency()
    {
        if (_world.StructuralChangeVersion != _structuralChangeVersion)
        {
            throw new InvalidOperationException(
                "The ECS world changed structurally during query iteration. Use deferred commands or create a new query enumerator.");
        }
    }
}

/// <summary>Allocation-free query view over entities containing three required component types.</summary>
public readonly struct ComponentQuery<T1, T2, T3>
    where T1 : struct, IComponent
    where T2 : struct, IComponent
    where T3 : struct, IComponent
{
    private readonly EcsWorld _world;
    private readonly QueryDescription _description;

    internal ComponentQuery(EcsWorld world, QueryDescription description)
    {
        _world = world;
        _description = description;
    }

    /// <summary>Creates the query enumerator.</summary>
    public ComponentQueryEnumerator<T1, T2, T3> GetEnumerator() => new(_world, _description);
}

/// <summary>Allocation-free enumerator over entities containing three required component types.</summary>
public ref struct ComponentQueryEnumerator<T1, T2, T3>
    where T1 : struct, IComponent
    where T2 : struct, IComponent
    where T3 : struct, IComponent
{
    private readonly EcsWorld _world;
    private readonly CompiledQueryPlan _plan;
    private readonly int _structuralChangeVersion;
    private QueryArchetypePlan? _currentArchetypePlan;
    private ArchetypeChunk? _currentChunk;
    private ComponentColumn<T1>? _column1;
    private ComponentColumn<T2>? _column2;
    private ComponentColumn<T3>? _column3;
    private int _archetypeIndex;
    private int _chunkIndex;
    private int _row;

    internal ComponentQueryEnumerator(EcsWorld world, QueryDescription description)
    {
        _world = world;
        int componentId1 = world.Registry.GetComponentId<T1>();
        int componentId2 = world.Registry.GetComponentId<T2>();
        int componentId3 = world.Registry.GetComponentId<T3>();
        _plan = world.GetCompiledQueryPlan(
            description,
            componentId1,
            componentId2,
            componentId3);
        _structuralChangeVersion = world.StructuralChangeVersion;
        _currentArchetypePlan = null;
        _currentChunk = null;
        _column1 = null;
        _column2 = null;
        _column3 = null;
        _archetypeIndex = -1;
        _chunkIndex = -1;
        _row = -1;
    }

    /// <summary>Gets the current entity id.</summary>
    public readonly EntityId Current => Entity;

    /// <summary>Gets the current entity id.</summary>
    public readonly EntityId Entity
    {
        get
        {
            EnsureCurrentRow();
            return _currentChunk!.GetEntity(_row);
        }
    }

    /// <summary>Gets a read-only reference to the first component on the current entity.</summary>
    public readonly ref readonly T1 Component1
    {
        get
        {
            EnsureCurrentRow();
            return ref _column1!.GetRef(_row);
        }
    }

    /// <summary>Gets a writable reference to the first component on the current entity.</summary>
    public readonly ref T1 MutableComponent1
    {
        get
        {
            EnsureCurrentRow();
            _column1!.SetVersion(_row, _world.ReserveChangeVersion());
            return ref _column1!.GetRef(_row);
        }
    }

    /// <summary>Gets a read-only reference to the second component on the current entity.</summary>
    public readonly ref readonly T2 Component2
    {
        get
        {
            EnsureCurrentRow();
            return ref _column2!.GetRef(_row);
        }
    }

    /// <summary>Gets a writable reference to the second component on the current entity.</summary>
    public readonly ref T2 MutableComponent2
    {
        get
        {
            EnsureCurrentRow();
            _column2!.SetVersion(_row, _world.ReserveChangeVersion());
            return ref _column2!.GetRef(_row);
        }
    }

    /// <summary>Gets a read-only reference to the third component on the current entity.</summary>
    public readonly ref readonly T3 Component3
    {
        get
        {
            EnsureCurrentRow();
            return ref _column3!.GetRef(_row);
        }
    }

    /// <summary>Gets a writable reference to the third component on the current entity.</summary>
    public readonly ref T3 MutableComponent3
    {
        get
        {
            EnsureCurrentRow();
            _column3!.SetVersion(_row, _world.ReserveChangeVersion());
            return ref _column3!.GetRef(_row);
        }
    }

    /// <summary>Advances to the next matching entity row.</summary>
    public bool MoveNext()
    {
        EnsureStructuralConsistency();

        while (true)
        {
            if (_currentChunk != null && ++_row < _currentChunk.Count)
            {
                return true;
            }

            if (MoveNextChunk())
            {
                continue;
            }

            return false;
        }
    }

    private bool MoveNextChunk()
    {
        while (true)
        {
            if (_currentArchetypePlan != null && ++_chunkIndex < _currentArchetypePlan.Archetype.ChunkCount)
            {
                ArchetypeChunk chunk = _currentArchetypePlan.Archetype.GetChunk(_chunkIndex);
                if (chunk.Count == 0)
                {
                    continue;
                }

                _currentChunk = chunk;
                _column1 = chunk.GetColumn<T1>(_currentArchetypePlan.AccessColumnIndexes[0]);
                _column2 = chunk.GetColumn<T2>(_currentArchetypePlan.AccessColumnIndexes[1]);
                _column3 = chunk.GetColumn<T3>(_currentArchetypePlan.AccessColumnIndexes[2]);
                _row = -1;
                return true;
            }

            _archetypeIndex++;
            if (_archetypeIndex >= _plan.Archetypes.Length)
            {
                _currentArchetypePlan = null;
                _currentChunk = null;
                return false;
            }

            _currentArchetypePlan = _plan.Archetypes[_archetypeIndex];
            _chunkIndex = -1;
        }
    }

    private readonly void EnsureCurrentRow()
    {
        EnsureStructuralConsistency();
        if (_currentChunk == null || _row < 0 || _row >= _currentChunk.Count)
        {
            throw new InvalidOperationException("The query enumerator is not positioned on a live row.");
        }
    }

    private readonly void EnsureStructuralConsistency()
    {
        if (_world.StructuralChangeVersion != _structuralChangeVersion)
        {
            throw new InvalidOperationException(
                "The ECS world changed structurally during query iteration. Use deferred commands or create a new query enumerator.");
        }
    }
}
