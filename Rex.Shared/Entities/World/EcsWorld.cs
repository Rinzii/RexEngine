using Rex.Shared.Components;
using Rex.Shared.Components.Registration;
using Rex.Shared.Entities.Queries;
using Rex.Shared.Entities.Storage;
using Rex.Shared.Serialization.Components;

namespace Rex.Shared.Entities.World;

/// <summary>
/// Shared ECS world runtime with archetype storage, structural migration and cached queries.
/// </summary>
public sealed class EcsWorld
{
    private readonly Dictionary<ArchetypeSignature, Archetype> _archetypes = [];
    private readonly Dictionary<QueryDescription, QueryCacheEntry> _queryCache = [];
    private readonly Dictionary<CompiledQueryKey, CompiledQueryCacheEntry> _compiledQueryCache = [];
    private readonly List<EntityRecord> _entityRecords = [];
    private readonly Stack<int> _freeSlots = new();
    private readonly Dictionary<int, ISingletonComponentBox> _singletonComponents = [];
    private readonly Archetype _emptyArchetype;
    private int _archetypeVersion;
    private int _structuralMutationLockDepth;
    private uint _nextComponentChangeVersion = 1;

    /// <summary>Creates a new ECS world backed by one frozen component registry.</summary>
    /// <param name="registry">Registry describing the component types available to the world.</param>
    public EcsWorld(ComponentRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        Registry = registry;
        Registry.Freeze();

        _emptyArchetype = new Archetype(ArchetypeSignature.Empty, Registry);
        _archetypes.Add(ArchetypeSignature.Empty, _emptyArchetype);
        _archetypeVersion = 1;
        StructuralChangeVersion = 1;
    }

    /// <summary>Gets the frozen component registry used by this world.</summary>
    public ComponentRegistry Registry { get; }

    /// <summary>Gets the number of live entities in the world.</summary>
    public int Count { get; private set; }

    internal int ArchetypeCount => _archetypes.Count;

    internal int QueryCacheCount => _compiledQueryCache.Count != 0
        ? _compiledQueryCache.Count
        : _queryCache.Count;

    internal int StructuralChangeVersion { get; private set; }

    /// <summary>Gets the latest monotonic component change version issued by this world.</summary>
    /// <remarks>Value-only writes bump this counter even when no archetype migration happens.</remarks>
    public uint CurrentChangeVersion => _nextComponentChangeVersion - 1;

    /// <summary>Creates an empty entity in the root archetype.</summary>
    /// <returns>The new live entity handle.</returns>
    public EntityId CreateEntity()
    {
        ThrowIfStructuralMutationLocked(nameof(CreateEntity));
        int slotIndex;
        EntityRecord record;

        if (_freeSlots.Count > 0)
        {
            slotIndex = _freeSlots.Pop();
            record = _entityRecords[slotIndex];
            record.Generation = record.Generation <= 0 ? 1 : record.Generation + 1;
        }
        else
        {
            slotIndex = _entityRecords.Count;
            record = new EntityRecord
            {
                Generation = 1
            };
            _entityRecords.Add(record);
        }

        var entity = EntityId.FromSlotIndex(slotIndex, record.Generation);
        ArchetypeRowLocation location = _emptyArchetype.AddEntity(entity);

        record.IsAlive = true;
        record.Location = location;
        record.Archetype = _emptyArchetype;

        Count++;
        StructuralChangeVersion++;
        return entity;
    }

    /// <summary>Destroys an entity if it is still alive.</summary>
    /// <param name="entity">Entity to destroy.</param>
    /// <returns><see langword="true"/> when the entity existed and was destroyed.</returns>
    public bool DestroyEntity(EntityId entity)
    {
        ThrowIfStructuralMutationLocked(nameof(DestroyEntity));
        if (!TryGetRecord(entity, out EntityRecord record))
        {
            return false;
        }

        RemoveRow(record.Archetype!, record.Location);
        record.IsAlive = false;
        record.Location = ArchetypeRowLocation.Invalid;
        record.Archetype = null;
        _freeSlots.Push(entity.SlotIndex);
        Count--;
        StructuralChangeVersion++;
        return true;
    }

    /// <summary>Checks whether an entity handle still refers to a live entity.</summary>
    /// <param name="entity">Entity handle to test.</param>
    /// <returns><see langword="true"/> when the entity is alive in this world.</returns>
    public bool Exists(EntityId entity)
    {
        return TryGetRecord(entity, out _);
    }

    /// <summary>Checks whether an entity contains a component type.</summary>
    /// <param name="entity">Entity to test.</param>
    /// <typeparam name="T">Component type to check.</typeparam>
    /// <returns><see langword="true"/> when the entity contains the component.</returns>
    public bool Has<T>(EntityId entity)
        where T : struct, IComponent
    {
        return TryGetRecord(entity, out EntityRecord record) && record.Archetype!.Signature.Contains(Registry.GetComponentId<T>());
    }

    /// <summary>Checks whether one shared singleton component exists.</summary>
    public bool HasSingleton<T>()
        where T : struct, IComponent
    {
        return _singletonComponents.ContainsKey(Registry.GetComponentId<T>());
    }

    /// <summary>Reads one shared singleton component value.</summary>
    public T GetSingleton<T>()
        where T : struct, IComponent
    {
        return GetSingletonBox<T>().Value;
    }

    /// <summary>Gets a read-only reference to one shared singleton component value.</summary>
    public ref readonly T GetSingletonRef<T>()
        where T : struct, IComponent
    {
        SingletonComponentBox<T> singleton = GetSingletonBox<T>();
        return ref singleton.Value;
    }

    /// <summary>Gets a writable reference to one shared singleton component value.</summary>
    public ref T GetMutableSingletonRef<T>()
        where T : struct, IComponent
    {
        SingletonComponentBox<T> singleton = GetSingletonBox<T>();
        singleton.Version = AdvanceChangeVersion();
        return ref singleton.Value;
    }

    /// <summary>Adds one shared singleton component.</summary>
    public void AddSingleton<T>(in T component)
        where T : struct, IComponent
    {
        int componentId = Registry.GetComponentId<T>();
        if (_singletonComponents.ContainsKey(componentId))
        {
            throw new InvalidOperationException(
                $"Singleton component '{typeof(T).FullName}' already exists in this world.");
        }

        _singletonComponents.Add(componentId, new SingletonComponentBox<T>(component, AdvanceChangeVersion()));
    }

    /// <summary>Overwrites one shared singleton component in place.</summary>
    public void SetSingleton<T>(in T component)
        where T : struct, IComponent
    {
        SingletonComponentBox<T> singleton = GetSingletonBox<T>();
        singleton.Value = component;
        singleton.Version = AdvanceChangeVersion();
    }

    /// <summary>Removes one shared singleton component.</summary>
    public bool RemoveSingleton<T>()
        where T : struct, IComponent
    {
        bool removed = _singletonComponents.Remove(Registry.GetComponentId<T>());
        if (removed)
        {
            _ = AdvanceChangeVersion();
        }

        return removed;
    }

    /// <summary>Gets the latest change version for one shared singleton component.</summary>
    public uint GetSingletonVersion<T>()
        where T : struct, IComponent
    {
        return GetSingletonBox<T>().Version;
    }

    internal bool Has(EntityId entity, Type componentType)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        return TryGetRecord(entity, out EntityRecord record)
               && record.Archetype!.Signature.Contains(Registry.GetRegistration(componentType).Id);
    }

    /// <summary>Attempts to read a component value from an entity.</summary>
    /// <param name="entity">Entity to read from.</param>
    /// <param name="component">Resolved component value when present.</param>
    /// <typeparam name="T">Component type to read.</typeparam>
    /// <returns><see langword="true"/> when the entity contains the component.</returns>
    public bool TryGet<T>(EntityId entity, out T component)
        where T : struct, IComponent
    {
        component = default;

        if (!TryGetRecord(entity, out EntityRecord record))
        {
            return false;
        }

        int componentId = Registry.GetComponentId<T>();
        if (!record.Archetype!.Signature.Contains(componentId))
        {
            return false;
        }

        component = record.Archetype.GetColumn<T>(componentId, record.Location.ChunkIndex).Get(record.Location.RowIndex);
        return true;
    }

    /// <summary>Reads a component value from an entity.</summary>
    /// <param name="entity">Entity to read from.</param>
    /// <typeparam name="T">Component type to read.</typeparam>
    /// <returns>The component value.</returns>
    public T Get<T>(EntityId entity)
        where T : struct, IComponent
    {
        if (!TryGet(entity, out T component))
        {
            throw new InvalidOperationException(
                $"Entity '{entity}' does not contain component '{typeof(T).FullName}'.");
        }

        return component;
    }

    /// <summary>Gets a read-only reference to a component stored on an entity.</summary>
    /// <param name="entity">Entity to read from.</param>
    /// <typeparam name="T">Component type to read.</typeparam>
    /// <returns>A read-only reference to the live component storage.</returns>
    public ref readonly T GetRef<T>(EntityId entity)
        where T : struct, IComponent
    {
        EntityRecord record = GetRequiredRecord(entity);
        int componentId = Registry.GetComponentId<T>();
        if (!record.Archetype!.Signature.Contains(componentId))
        {
            throw new InvalidOperationException(
                $"Entity '{entity}' does not contain component '{typeof(T).FullName}'.");
        }

        ComponentColumn<T> column = record.Archetype.GetColumn<T>(componentId, record.Location.ChunkIndex);
        return ref column.GetRef(record.Location.RowIndex);
    }

    /// <summary>Gets a writable reference to a component stored on an entity.</summary>
    /// <param name="entity">Entity to edit.</param>
    /// <typeparam name="T">Component type to edit.</typeparam>
    /// <returns>A writable reference to the live component storage.</returns>
    public ref T GetMutableRef<T>(EntityId entity)
        where T : struct, IComponent
    {
        EntityRecord record = GetRequiredRecord(entity);
        int componentId = Registry.GetComponentId<T>();
        if (!record.Archetype!.Signature.Contains(componentId))
        {
            throw new InvalidOperationException(
                $"Entity '{entity}' does not contain component '{typeof(T).FullName}'.");
        }

        ComponentColumn<T> column = record.Archetype.GetColumn<T>(componentId, record.Location.ChunkIndex);
        column.SetVersion(record.Location.RowIndex, AdvanceChangeVersion());
        return ref column.GetRef(record.Location.RowIndex);
    }

    /// <summary>Overwrites an existing component value in place.</summary>
    /// <param name="entity">Entity to update.</param>
    /// <param name="component">New component value.</param>
    /// <typeparam name="T">Component type to update.</typeparam>
    public void Set<T>(EntityId entity, in T component)
        where T : struct, IComponent
    {
        EntityRecord record = GetRequiredRecord(entity);
        int componentId = Registry.GetComponentId<T>();
        if (!record.Archetype!.Signature.Contains(componentId))
        {
            throw new InvalidOperationException(
                $"Entity '{entity}' does not contain component '{typeof(T).FullName}'.");
        }

        ComponentColumn<T> column = record.Archetype.GetColumn<T>(componentId, record.Location.ChunkIndex);
        ref T current = ref column.GetRef(record.Location.RowIndex);
        current = component;
        column.SetVersion(record.Location.RowIndex, AdvanceChangeVersion());
    }

    /// <summary>Adds a default-initialized component to an entity.</summary>
    /// <param name="entity">Entity to update.</param>
    /// <typeparam name="T">Component type to add.</typeparam>
    public void Add<T>(EntityId entity)
        where T : struct, IComponent
    {
        Add(entity, default(T));
    }

    /// <summary>Adds a component to an entity and performs the required archetype migration.</summary>
    /// <param name="entity">Entity to update.</param>
    /// <param name="component">Component value to add.</param>
    /// <typeparam name="T">Component type to add.</typeparam>
    public void Add<T>(EntityId entity, in T component)
        where T : struct, IComponent
    {
        ThrowIfStructuralMutationLocked(nameof(Add));
        EntityRecord record = GetRequiredRecord(entity);
        ComponentRegistration registration = Registry.GetRegistration<T>();
        if (record.Archetype!.Signature.Contains(registration.Id))
        {
            throw new InvalidOperationException(
                $"Entity '{entity}' already contains component '{typeof(T).FullName}'.");
        }

        Archetype sourceArchetype = record.Archetype!;
        ArchetypeRowLocation sourceLocation = record.Location;
        Archetype destinationArchetype = GetOrCreateArchetype(sourceArchetype.Signature.Add(registration.Id));
        ArchetypeRowLocation destinationLocation = destinationArchetype.AddEntity(entity);

        foreach (int componentId in destinationArchetype.ComponentIds)
        {
            IComponentColumn destinationColumn = destinationArchetype.GetColumn(componentId, destinationLocation.ChunkIndex);
            if (componentId == registration.Id)
            {
                var typedDestination = (ComponentColumn<T>)destinationColumn;
                typedDestination.Set(destinationLocation.RowIndex, component);
                typedDestination.SetVersion(destinationLocation.RowIndex, AdvanceChangeVersion());
                continue;
            }

            sourceArchetype.GetColumn(componentId, sourceLocation.ChunkIndex)
                .CopyValueTo(sourceLocation.RowIndex, destinationColumn, destinationLocation.RowIndex);
        }

        RemoveRow(sourceArchetype, sourceLocation);
        record.Archetype = destinationArchetype;
        record.Location = destinationLocation;
        StructuralChangeVersion++;
    }

    /// <summary>Removes a component from an entity and performs the required archetype migration.</summary>
    /// <param name="entity">Entity to update.</param>
    /// <typeparam name="T">Component type to remove.</typeparam>
    /// <returns><see langword="true"/> when the component was present and removed.</returns>
    public bool Remove<T>(EntityId entity)
        where T : struct, IComponent
    {
        ThrowIfStructuralMutationLocked(nameof(Remove));
        if (!TryGetRecord(entity, out EntityRecord record))
        {
            return false;
        }

        int componentId = Registry.GetComponentId<T>();
        if (!record.Archetype!.Signature.Contains(componentId))
        {
            return false;
        }

        Archetype sourceArchetype = record.Archetype!;
        ArchetypeRowLocation sourceLocation = record.Location;
        Archetype destinationArchetype = GetOrCreateArchetype(sourceArchetype.Signature.Remove(componentId));
        ArchetypeRowLocation destinationLocation = destinationArchetype.AddEntity(entity);

        foreach (int destinationComponentId in destinationArchetype.ComponentIds)
        {
            sourceArchetype.GetColumn(destinationComponentId, sourceLocation.ChunkIndex)
                .CopyValueTo(
                    sourceLocation.RowIndex,
                    destinationArchetype.GetColumn(destinationComponentId, destinationLocation.ChunkIndex),
                    destinationLocation.RowIndex);
        }

        RemoveRow(sourceArchetype, sourceLocation);
        record.Archetype = destinationArchetype;
        record.Location = destinationLocation;
        _ = AdvanceChangeVersion();
        StructuralChangeVersion++;
        return true;
    }

    internal void AddBoxed(EntityId entity, Type componentType, object component)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        ArgumentNullException.ThrowIfNull(component);
        ThrowIfStructuralMutationLocked(nameof(AddBoxed));

        EntityRecord record = GetRequiredRecord(entity);
        ComponentRegistration registration = Registry.GetRegistration(componentType);
        if (record.Archetype!.Signature.Contains(registration.Id))
        {
            throw new InvalidOperationException(
                $"Entity '{entity}' already contains component '{componentType.FullName}'.");
        }

        Archetype sourceArchetype = record.Archetype;
        ArchetypeRowLocation sourceLocation = record.Location;
        Archetype destinationArchetype = GetOrCreateArchetype(sourceArchetype.Signature.Add(registration.Id));
        ArchetypeRowLocation destinationLocation = destinationArchetype.AddEntity(entity);

        foreach (int componentId in destinationArchetype.ComponentIds)
        {
            IComponentColumn destinationColumn = destinationArchetype.GetColumn(componentId, destinationLocation.ChunkIndex);
            if (componentId == registration.Id)
            {
                destinationColumn.SetBoxed(destinationLocation.RowIndex, component);
                destinationColumn.SetVersion(destinationLocation.RowIndex, AdvanceChangeVersion());
                continue;
            }

            sourceArchetype.GetColumn(componentId, sourceLocation.ChunkIndex)
                .CopyValueTo(sourceLocation.RowIndex, destinationColumn, destinationLocation.RowIndex);
        }

        RemoveRow(sourceArchetype, sourceLocation);
        record.Archetype = destinationArchetype;
        record.Location = destinationLocation;
        StructuralChangeVersion++;
    }

    internal bool Remove(EntityId entity, Type componentType)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        ThrowIfStructuralMutationLocked(nameof(Remove));
        if (!TryGetRecord(entity, out EntityRecord record))
        {
            return false;
        }

        int componentId = Registry.GetRegistration(componentType).Id;
        if (!record.Archetype!.Signature.Contains(componentId))
        {
            return false;
        }

        Archetype sourceArchetype = record.Archetype;
        ArchetypeRowLocation sourceLocation = record.Location;
        Archetype destinationArchetype = GetOrCreateArchetype(sourceArchetype.Signature.Remove(componentId));
        ArchetypeRowLocation destinationLocation = destinationArchetype.AddEntity(entity);

        foreach (int destinationComponentId in destinationArchetype.ComponentIds)
        {
            sourceArchetype.GetColumn(destinationComponentId, sourceLocation.ChunkIndex)
                .CopyValueTo(
                    sourceLocation.RowIndex,
                    destinationArchetype.GetColumn(destinationComponentId, destinationLocation.ChunkIndex),
                    destinationLocation.RowIndex);
        }

        RemoveRow(sourceArchetype, sourceLocation);
        record.Archetype = destinationArchetype;
        record.Location = destinationLocation;
        _ = AdvanceChangeVersion();
        StructuralChangeVersion++;
        return true;
    }

    internal void SetBoxed(EntityId entity, Type componentType, object component)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        ArgumentNullException.ThrowIfNull(component);

        EntityRecord record = GetRequiredRecord(entity);
        ComponentRegistration registration = Registry.GetRegistration(componentType);
        if (!record.Archetype!.Signature.Contains(registration.Id))
        {
            throw new InvalidOperationException(
                $"Entity '{entity}' does not contain component '{componentType.FullName}'.");
        }

        IComponentColumn column = record.Archetype.GetColumn(registration.Id, record.Location.ChunkIndex);
        column.SetBoxed(record.Location.RowIndex, component);
        column.SetVersion(record.Location.RowIndex, AdvanceChangeVersion());
    }

    /// <summary>Gets the latest change version for one entity component.</summary>
    public uint GetComponentVersion<T>(EntityId entity)
        where T : struct, IComponent
    {
        EntityRecord record = GetRequiredRecord(entity);
        int componentId = Registry.GetComponentId<T>();
        if (!record.Archetype!.Signature.Contains(componentId))
        {
            throw new InvalidOperationException(
                $"Entity '{entity}' does not contain component '{typeof(T).FullName}'.");
        }

        return record.Archetype.GetColumn(componentId, record.Location.ChunkIndex).GetVersion(record.Location.RowIndex);
    }

    /// <summary>Creates a query for one required component type.</summary>
    /// <typeparam name="T1">Required component type.</typeparam>
    /// <returns>An allocation-free query view.</returns>
    public ComponentQuery<T1> Query<T1>()
        where T1 : struct, IComponent
    {
        return new ComponentQuery<T1>(this, QueryDescriptionCache<T1>.Description);
    }

    /// <summary>Creates a query for one required component type plus additional filters.</summary>
    /// <param name="description">Additional required and excluded filters.</param>
    /// <typeparam name="T1">Required component type.</typeparam>
    /// <returns>An allocation-free query view.</returns>
    public ComponentQuery<T1> Query<T1>(QueryDescription description)
        where T1 : struct, IComponent
    {
        return new ComponentQuery<T1>(this, CombineDescription(description, typeof(T1)));
    }

    /// <summary>Creates a query for two required component types.</summary>
    /// <typeparam name="T1">First required component type.</typeparam>
    /// <typeparam name="T2">Second required component type.</typeparam>
    /// <returns>An allocation-free query view.</returns>
    public ComponentQuery<T1, T2> Query<T1, T2>()
        where T1 : struct, IComponent
        where T2 : struct, IComponent
    {
        return new ComponentQuery<T1, T2>(this, QueryDescriptionCache<T1, T2>.Description);
    }

    /// <summary>Creates a query for two required component types plus additional filters.</summary>
    /// <param name="description">Additional required and excluded filters.</param>
    /// <typeparam name="T1">First required component type.</typeparam>
    /// <typeparam name="T2">Second required component type.</typeparam>
    /// <returns>An allocation-free query view.</returns>
    public ComponentQuery<T1, T2> Query<T1, T2>(QueryDescription description)
        where T1 : struct, IComponent
        where T2 : struct, IComponent
    {
        return new ComponentQuery<T1, T2>(this, CombineDescription(description, typeof(T1), typeof(T2)));
    }

    /// <summary>Creates a query for three required component types.</summary>
    /// <typeparam name="T1">First required component type.</typeparam>
    /// <typeparam name="T2">Second required component type.</typeparam>
    /// <typeparam name="T3">Third required component type.</typeparam>
    /// <returns>An allocation-free query view.</returns>
    public ComponentQuery<T1, T2, T3> Query<T1, T2, T3>()
        where T1 : struct, IComponent
        where T2 : struct, IComponent
        where T3 : struct, IComponent
    {
        return new ComponentQuery<T1, T2, T3>(this, QueryDescriptionCache<T1, T2, T3>.Description);
    }

    /// <summary>Creates a query for three required component types plus additional filters.</summary>
    /// <param name="description">Additional required and excluded filters.</param>
    /// <typeparam name="T1">First required component type.</typeparam>
    /// <typeparam name="T2">Second required component type.</typeparam>
    /// <typeparam name="T3">Third required component type.</typeparam>
    /// <returns>An allocation-free query view.</returns>
    public ComponentQuery<T1, T2, T3> Query<T1, T2, T3>(QueryDescription description)
        where T1 : struct, IComponent
        where T2 : struct, IComponent
        where T3 : struct, IComponent
    {
        return new ComponentQuery<T1, T2, T3>(this, CombineDescription(description, typeof(T1), typeof(T2), typeof(T3)));
    }

    /// <summary>Creates a deterministic serialized snapshot of the current world.</summary>
    /// <returns>The serialized world snapshot.</returns>
    public SerializedWorld Serialize()
    {
        var entities = new List<SerializedEntity>(Count);
        for (int slotIndex = 0; slotIndex < _entityRecords.Count; slotIndex++)
        {
            EntityRecord record = _entityRecords[slotIndex];
            if (!record.IsAlive)
            {
                continue;
            }

            var entityId = EntityId.FromSlotIndex(slotIndex, record.Generation);
            var payloads = new List<KeyValuePair<int, byte[]>>(record.Archetype!.Signature.Count);

            foreach (int componentId in record.Archetype.ComponentIds)
            {
                ComponentRegistration registration = Registry.GetRegistration(componentId);
                byte[] payload = registration.Serializer.SerializeBoxed(
                    record.Archetype.GetColumn(componentId, record.Location.ChunkIndex).GetBoxed(record.Location.RowIndex));
                payloads.Add(new KeyValuePair<int, byte[]>(componentId, payload));
            }

            entities.Add(new SerializedEntity(entityId, payloads));
        }

        return new SerializedWorld(entities);
    }

    /// <summary>Builds a world instance from a deterministic serialized snapshot.</summary>
    /// <param name="registry">Registry describing all serialized component types.</param>
    /// <param name="serializedWorld">Serialized world snapshot to load.</param>
    /// <returns>A new world containing the serialized entities and components.</returns>
    public static EcsWorld Deserialize(ComponentRegistry registry, SerializedWorld serializedWorld)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(serializedWorld);

        EcsWorld world = new(registry);
        if (serializedWorld.Entities.Count == 0)
        {
            return world;
        }

        int maxSlot = 0;
        foreach (SerializedEntity entity in serializedWorld.Entities)
        {
            if (!entity.EntityId.IsValid)
            {
                throw new InvalidOperationException("Serialized entities must use valid entity ids.");
            }

            maxSlot = Math.Max(maxSlot, entity.EntityId.Slot);
        }

        while (world._entityRecords.Count < maxSlot)
        {
            world._entityRecords.Add(new EntityRecord());
        }

        bool[] occupied = new bool[maxSlot];
        foreach (SerializedEntity serializedEntity in serializedWorld.Entities)
        {
            EntityId entityId = serializedEntity.EntityId;
            int slotIndex = entityId.SlotIndex;
            if (occupied[slotIndex])
            {
                throw new InvalidOperationException($"Serialized world contains duplicate entity slot {entityId.Slot}.");
            }

            occupied[slotIndex] = true;

            Dictionary<int, byte[]> componentPayloads = new(serializedEntity.Components.Count);
            foreach (KeyValuePair<int, byte[]> component in serializedEntity.Components)
            {
                if (!registry.TryGetRegistration(component.Key, out _))
                {
                    throw new InvalidOperationException(
                        $"Serialized entity '{entityId}' references unknown component id {component.Key}.");
                }

                if (!componentPayloads.TryAdd(component.Key, component.Value))
                {
                    throw new InvalidOperationException(
                        $"Serialized entity '{entityId}' contains duplicate component id {component.Key}.");
                }
            }

            var signature = ArchetypeSignature.FromComponentIds(componentPayloads.Keys);
            Archetype archetype = world.GetOrCreateArchetype(signature);
            ArchetypeRowLocation location = archetype.AddEntity(entityId);

            foreach (int componentId in archetype.ComponentIds)
            {
                ComponentRegistration registration = registry.GetRegistration(componentId);
                object value = registration.Serializer.DeserializeBoxed(componentPayloads[componentId]);
                archetype.GetColumn(componentId, location.ChunkIndex).SetBoxed(location.RowIndex, value);
            }

            EntityRecord record = world._entityRecords[slotIndex];
            record.Generation = entityId.Generation;
            record.IsAlive = true;
            record.Location = location;
            record.Archetype = archetype;
            world.Count++;
        }

        for (int slotIndex = maxSlot - 1; slotIndex >= 0; slotIndex--)
        {
            if (!occupied[slotIndex])
            {
                world._freeSlots.Push(slotIndex);
            }
        }

        return world;
    }

    internal CompiledQueryPlan GetCompiledQueryPlan(QueryDescription description, int accessComponentId1)
        => GetCompiledQueryPlanCore(description, new CompiledQueryKey(description, 1, accessComponentId1, 0, 0));

    internal CompiledQueryPlan GetCompiledQueryPlan(QueryDescription description, int accessComponentId1, int accessComponentId2)
        => GetCompiledQueryPlanCore(description, new CompiledQueryKey(description, 2, accessComponentId1, accessComponentId2, 0));

    internal CompiledQueryPlan GetCompiledQueryPlan(QueryDescription description, int accessComponentId1, int accessComponentId2,
        int accessComponentId3)
        => GetCompiledQueryPlanCore(description, new CompiledQueryKey(description, 3, accessComponentId1, accessComponentId2, accessComponentId3));

    internal Archetype[] GetMatchingArchetypes(QueryDescription description)
    {
        ArgumentNullException.ThrowIfNull(description);

        if (!_queryCache.TryGetValue(description, out QueryCacheEntry? cache))
        {
            cache = new QueryCacheEntry(ResolveQuery(description));
            _queryCache.Add(description, cache);
        }

        if (cache.ArchetypeVersion != _archetypeVersion)
        {
            List<Archetype> matches = [];
            foreach (Archetype archetype in _archetypes.Values)
            {
                if (archetype.Signature.Matches(cache.RequiredIds, cache.ExcludedIds))
                {
                    matches.Add(archetype);
                }
            }

            cache.Archetypes = matches.ToArray();
            cache.ArchetypeVersion = _archetypeVersion;
        }

        return cache.Archetypes;
    }

    internal ReadOnlySpan<int> GetComponentIds(EntityId entity)
    {
        EntityRecord record = GetRequiredRecord(entity);
        return record.Archetype!.ComponentIds;
    }

    internal bool Has(EntityId entity, int componentId)
    {
        return TryGetRecord(entity, out EntityRecord record) && record.Archetype!.Signature.Contains(componentId);
    }

    internal uint GetComponentVersion(EntityId entity, int componentId)
    {
        EntityRecord record = GetRequiredRecord(entity);
        if (!record.Archetype!.Signature.Contains(componentId))
        {
            throw new InvalidOperationException($"Entity '{entity}' does not contain component id {componentId}.");
        }

        return record.Archetype.GetColumn(componentId, record.Location.ChunkIndex).GetVersion(record.Location.RowIndex);
    }

    internal uint ReserveChangeVersion()
    {
        return AdvanceChangeVersion();
    }

    internal object GetBoxedComponent(EntityId entity, int componentId)
    {
        EntityRecord record = GetRequiredRecord(entity);
        if (!record.Archetype!.Signature.Contains(componentId))
        {
            throw new InvalidOperationException($"Entity '{entity}' does not contain component id {componentId}.");
        }

        return record.Archetype.GetColumn(componentId, record.Location.ChunkIndex).GetBoxed(record.Location.RowIndex);
    }

    internal void BeginStructuralMutationScope()
    {
        _ = Interlocked.Increment(ref _structuralMutationLockDepth);
    }

    internal void EndStructuralMutationScope()
    {
        _ = Interlocked.Decrement(ref _structuralMutationLockDepth);
    }

    private static QueryDescription CombineDescription(QueryDescription description, params Type[] requiredTypes)
    {
        ArgumentNullException.ThrowIfNull(description);

        var combinedRequired = new Type[description.RequiredTypes.Count + requiredTypes.Length];
        for (int i = 0; i < description.RequiredTypes.Count; i++)
        {
            combinedRequired[i] = description.RequiredTypes[i];
        }

        Array.Copy(requiredTypes, 0, combinedRequired, description.RequiredTypes.Count, requiredTypes.Length);
        return new QueryDescription(combinedRequired, description.ExcludedTypes);
    }

    private ResolvedQueryDescription ResolveQuery(QueryDescription description)
    {
        int[] requiredIds = ResolveComponentIds(description.RequiredTypeArray);
        int[] excludedIds = ResolveComponentIds(description.ExcludedTypeArray);
        return new ResolvedQueryDescription(requiredIds, excludedIds);
    }

    private int[] ResolveComponentIds(Type[] componentTypes)
    {
        if (componentTypes.Length == 0)
        {
            return [];
        }

        int[] ids = new int[componentTypes.Length];
        for (int i = 0; i < componentTypes.Length; i++)
        {
            ids[i] = Registry.GetRegistration(componentTypes[i]).Id;
        }

        Array.Sort(ids);
        return ids;
    }

    private Archetype GetOrCreateArchetype(ArchetypeSignature signature)
    {
        if (_archetypes.TryGetValue(signature, out Archetype? archetype))
        {
            return archetype;
        }

        archetype = new Archetype(signature, Registry);
        _archetypes.Add(signature, archetype);
        _archetypeVersion++;
        return archetype;
    }

    private void RemoveRow(Archetype archetype, ArchetypeRowLocation location)
    {
        archetype.RemoveEntity(location, out bool moved, out EntityId movedEntity, out ArchetypeRowLocation movedLocation);
        if (!moved)
        {
            return;
        }

        EntityRecord movedRecord = _entityRecords[movedEntity.SlotIndex];
        movedRecord.Location = movedLocation;
    }

    private EntityRecord GetRequiredRecord(EntityId entity)
    {
        if (!TryGetRecord(entity, out EntityRecord record))
        {
            throw new InvalidOperationException($"Entity '{entity}' is not alive in this world.");
        }

        return record;
    }

    private bool TryGetRecord(EntityId entity, out EntityRecord record)
    {
        record = null!;
        if (!entity.IsValid)
        {
            return false;
        }

        int slotIndex = entity.SlotIndex;
        if (slotIndex < 0 || slotIndex >= _entityRecords.Count)
        {
            return false;
        }

        record = _entityRecords[slotIndex];
        return record.IsAlive && record.Generation == entity.Generation;
    }

    private uint AdvanceChangeVersion()
    {
        return _nextComponentChangeVersion++;
    }

    private CompiledQueryPlan GetCompiledQueryPlanCore(QueryDescription description, CompiledQueryKey cacheKey)
    {
        ArgumentNullException.ThrowIfNull(description);

        if (!_compiledQueryCache.TryGetValue(cacheKey, out CompiledQueryCacheEntry? cache))
        {
            cache = new CompiledQueryCacheEntry();
            _compiledQueryCache.Add(cacheKey, cache);
        }

        if (cache.ArchetypeVersion != _archetypeVersion)
        {
            Archetype[] matches = GetMatchingArchetypes(description);
            var compiledArchetypes = new QueryArchetypePlan[matches.Length];
            int[] accessComponentIds = cacheKey.GetAccessComponentIds();
            for (int i = 0; i < matches.Length; i++)
            {
                Archetype archetype = matches[i];
                int[] columnIndexes = new int[accessComponentIds.Length];
                for (int j = 0; j < accessComponentIds.Length; j++)
                {
                    columnIndexes[j] = archetype.GetColumnIndex(accessComponentIds[j]);
                }

                compiledArchetypes[i] = new QueryArchetypePlan(archetype, columnIndexes);
            }

            cache.Plan = new CompiledQueryPlan(compiledArchetypes);
            cache.ArchetypeVersion = _archetypeVersion;
        }

        return cache.Plan;
    }

    private void ThrowIfStructuralMutationLocked(string operation)
    {
        if (Volatile.Read(ref _structuralMutationLockDepth) > 0)
        {
            throw new InvalidOperationException(
                $"Structural world operation '{operation}' is not allowed during managed system execution. Queue the work through an entity command buffer instead.");
        }
    }

    private SingletonComponentBox<T> GetSingletonBox<T>()
        where T : struct, IComponent
    {
        int componentId = Registry.GetComponentId<T>();
        if (!_singletonComponents.TryGetValue(componentId, out ISingletonComponentBox? singleton))
        {
            throw new InvalidOperationException(
                $"Singleton component '{typeof(T).FullName}' does not exist in this world.");
        }

        return (SingletonComponentBox<T>)singleton;
    }

    private sealed class EntityRecord
    {
        public int Generation { get; set; }

        public bool IsAlive { get; set; }

        public Archetype? Archetype { get; set; }

        public ArchetypeRowLocation Location { get; set; } = ArchetypeRowLocation.Invalid;
    }

    private sealed class QueryCacheEntry
    {
        public QueryCacheEntry(ResolvedQueryDescription description)
        {
            RequiredIds = description.RequiredIds;
            ExcludedIds = description.ExcludedIds;
            Archetypes = [];
            ArchetypeVersion = -1;
        }

        public int[] RequiredIds { get; }

        public int[] ExcludedIds { get; }

        public Archetype[] Archetypes { get; set; }

        public int ArchetypeVersion { get; set; }
    }

    private sealed class CompiledQueryCacheEntry
    {
        public CompiledQueryCacheEntry()
        {
            Plan = new CompiledQueryPlan([]);
            ArchetypeVersion = -1;
        }

        public CompiledQueryPlan Plan { get; set; }

        public int ArchetypeVersion { get; set; }
    }

    private readonly record struct CompiledQueryKey(
        QueryDescription Description,
        int Count,
        int AccessComponentId1,
        int AccessComponentId2,
        int AccessComponentId3)
    {
        public bool Equals(CompiledQueryKey other)
        {
            return (ReferenceEquals(Description, other.Description) || Description.Equals(other.Description))
                   && Count == other.Count
                   && AccessComponentId1 == other.AccessComponentId1
                   && AccessComponentId2 == other.AccessComponentId2
                   && AccessComponentId3 == other.AccessComponentId3;
        }

        public override int GetHashCode()
        {
            HashCode hash = new();
            hash.Add(Description);
            hash.Add(Count);
            hash.Add(AccessComponentId1);
            hash.Add(AccessComponentId2);
            hash.Add(AccessComponentId3);

            return hash.ToHashCode();
        }

        public int[] GetAccessComponentIds()
        {
            return Count switch
            {
                1 => [AccessComponentId1],
                2 => [AccessComponentId1, AccessComponentId2],
                3 => [AccessComponentId1, AccessComponentId2, AccessComponentId3],
                _ => throw new InvalidOperationException($"Unsupported compiled query access count {Count}.")
            };
        }
    }

    private readonly record struct ResolvedQueryDescription(int[] RequiredIds, int[] ExcludedIds);

    private static class QueryDescriptionCache<T1>
        where T1 : struct, IComponent
    {
        public static readonly QueryDescription Description = new([typeof(T1)]);
    }

    private static class QueryDescriptionCache<T1, T2>
        where T1 : struct, IComponent
        where T2 : struct, IComponent
    {
        public static readonly QueryDescription Description = new([typeof(T1), typeof(T2)]);
    }

    private static class QueryDescriptionCache<T1, T2, T3>
        where T1 : struct, IComponent
        where T2 : struct, IComponent
        where T3 : struct, IComponent
    {
        public static readonly QueryDescription Description = new([typeof(T1), typeof(T2), typeof(T3)]);
    }

    private interface ISingletonComponentBox
    {
    }

    private sealed class SingletonComponentBox<T> : ISingletonComponentBox
        where T : struct, IComponent
    {
        public SingletonComponentBox(T value, uint version)
        {
            Value = value;
            Version = version;
        }

        public T Value;

        public uint Version;
    }
}
