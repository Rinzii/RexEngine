using Rex.Sandbox.Shared.Components;
using Rex.Sandbox.Shared.Components.Registration;
using Rex.Sandbox.Shared.Net.Messages;
using Rex.Sandbox.Shared.Resources;
using Rex.Sandbox.Shared.Systems;
using Rex.Shared.Components.BuiltIn;
using Rex.Shared.Components.Registration;
using Rex.Shared.Entities;
using Rex.Shared.Entities.Queries;
using Rex.Shared.GameObjects;
using Rex.Shared.GameStates;
using Rex.Shared.Net.Replication;
using Rex.Shared.Prototypes;
using Rex.Shared.Resources;
using Rex.Shared.Serialization.Manager;
using Rex.Shared.Simulation;

namespace Rex.Sandbox.Shared.Simulation;

/// <summary>
/// Simulation tuning for the Sandbox sample, shared between the authoritative world and client prediction.
/// </summary>
public static class MovementConstants
{
    public const float PlanarUnitsPerInputTick = 5f;
}

/// <summary>
/// Stable Sandbox entity type names shared by simulation and replication.
/// Shipping games register their own ids next to these sample strings.
/// </summary>
public static class EntityTypeIds
{
    public const string Player = "player";
}

/// <summary>
/// Authoritative Sandbox world. The sample keeps gameplay rules outside the reusable engine layer.
/// </summary>
public sealed class GameWorld
{
    private readonly DirtyTracker? _dirtyTracker;
    private readonly KeyedEntityStateStore<int, EntityState> _entityStates = new(static entity => entity.EntityId);
    private readonly Dictionary<int, EntityId> _entityHandles = [];
    private readonly Dictionary<int, ReplicatedEntityState> _lastReplicatedStates = [];
    private readonly PrototypeManager _prototypeManager;
    private readonly EcsReplicationSnapshotBuilder _replicationSnapshotBuilder;
    private int _nextEntityId = 1;
    private bool _entitiesDirty = true;
    private int _lastCachedWorldCount = -1;
    private uint _lastCachedWorldChangeVersion = uint.MaxValue;

    public GameWorld(DirtyTracker? dirtyTracker = null, ResourceManager? resourceManager = null)
    {
        _dirtyTracker = dirtyTracker;

        SerializationManager serializationManager = new();
        _prototypeManager = new PrototypeManager(serializationManager);
        SharedPrototypeBootstrap.RegisterAll(_prototypeManager);

        ResourceManager? resolvedResourceManager = resourceManager ?? SandboxResourceLocator.TryCreateDefaultResourceManager();
        if (resolvedResourceManager != null)
        {
            _prototypeManager.LoadResources(resolvedResourceManager);
            _prototypeManager.LoadDirectory(
                Path.Combine(
                    resolvedResourceManager.TestingSampleDirectory,
                    "Sandbox",
                    SharedResourceDirectories.Prototypes));
        }

        ComponentRegistry registry = new();
        SharedEcsBootstrap.RegisterAll(registry);
        SandboxEcsBootstrap.RegisterAll(registry);

        EntityManager = new EntityManager(registry, _prototypeManager, serializationManager);
        _replicationSnapshotBuilder = new EcsReplicationSnapshotBuilder(
            registry,
            [
                typeof(TransformComponent),
                typeof(OwnerComponent),
                typeof(MetaDataComponent),
                typeof(SandboxActorComponent),
                typeof(SandboxModelComponent)
            ]);
        _ = EntityManager.AddSystem<SandboxMovementSystem>();
        EntityManager.InitializeSystems();
    }

    public EntityManager EntityManager { get; }
    public IReadOnlyDictionary<int, EntityState> Entities
    {
        get
        {
            RefreshEntityCache();
            return _entityStates.Entities;
        }
    }

    public uint CurrentTick { get; private set; }

    public int SpawnEntity(Guid ownerClientId, string entityType, float x, float y, float z)
    {
        int entityId = _nextEntityId++;
        string? prototypeId = ResolvePrototypeId(entityType);
        EntityId entity = prototypeId != null
            ? EntityManager.SpawnEntity(prototypeId)
            : EntityManager.CreateEntity();

        _entityHandles[entityId] = entity;
        ApplyRuntimeIdentity(entity, entityId, entityType, prototypeId);
        ApplyTransform(entity, x, y, z, 0f);
        ApplyOwnership(entity, ownerClientId);
        EnsureMoverDefaults(entity);

        _entitiesDirty = true;
        return entityId;
    }

    public void DestroyEntity(int entityId)
    {
        if (!_entityHandles.TryGetValue(entityId, out EntityId entity))
        {
            return;
        }

        _ = _entityHandles.Remove(entityId);
        _ = EntityManager.DeleteEntity(entity);
        _entitiesDirty = true;
    }

    public void ProcessInput(int entityId, PlayerInputMessage input)
    {
        if (!_entityHandles.TryGetValue(entityId, out EntityId entity) || !EntityManager.Exists(entity))
        {
            return;
        }

        SandboxPlayerInputEvent playerInputEvent = new(
            input.Tick,
            input.MoveX,
            input.MoveY,
            input.LookX,
            input.LookY,
            input.ActionFlags);
        EntityManager.EventBus.RaiseLocalEvent(entity, ref playerInputEvent);

        _entitiesDirty = true;
    }

    public void Tick(float deltaTime)
    {
        EntityManager.Update(deltaTime);
        CurrentTick++;
        // Marks dirty and removed net ids for this tick from the serialized replication view, not from ad hoc calls elsewhere.
        RecordReplicationChangesForCurrentTick();
    }

    public WorldSnapshotMessage BuildSnapshot(uint serverTick, uint lastProcessedInputTick)
    {
        RefreshEntityCache();
        // Full frames always carry every net id that still maps to a live ECS row plus an empty remove list.
        return new WorldSnapshotMessage(
            serverTick,
            lastProcessedInputTick,
            _replicationSnapshotBuilder.Build(EntityManager.World, _entityHandles),
            isFullSnapshot: true,
            removedEntityIds: []);
    }

    public WorldSnapshotMessage BuildDeltaSnapshot(uint serverTick, uint lastProcessedInputTick,
        HashSet<int> dirtyEntityIds, HashSet<int> removedEntityIds)
    {
        RefreshEntityCache();

        // Dirty ids are upserts. Removed ids are explicit tombstones from the tracker across the same ack window.
        List<KeyValuePair<int, EntityId>> dirtyEntities = [];
        foreach (int entityId in dirtyEntityIds.OrderBy(static value => value))
        {
            if (_entityHandles.TryGetValue(entityId, out EntityId entity))
            {
                dirtyEntities.Add(new KeyValuePair<int, EntityId>(entityId, entity));
            }
        }

        IReadOnlyList<ReplicatedEntityState> entities = _replicationSnapshotBuilder.Build(EntityManager.World, dirtyEntities);
        return new WorldSnapshotMessage(
            serverTick,
            lastProcessedInputTick,
            entities,
            isFullSnapshot: false,
            removedEntityIds: [.. removedEntityIds.OrderBy(static value => value)]);
    }

    private void ApplyRuntimeIdentity(EntityId entity, int entityId, string entityType, string? prototypeId)
    {
        SandboxActorComponent actor = EntityManager.HasComponent<SandboxActorComponent>(entity)
            ? EntityManager.GetComponent<SandboxActorComponent>(entity)
            : default;

        actor.NetEntityId = entityId;
        actor.EntityType = string.IsNullOrWhiteSpace(actor.EntityType) ? entityType : actor.EntityType;
        actor.PrototypeId = prototypeId ?? actor.PrototypeId;

        if (EntityManager.HasComponent<SandboxActorComponent>(entity))
        {
            EntityManager.SetComponent(entity, actor);
        }
        else
        {
            EntityManager.AddComponent(entity, actor);
        }
    }

    private void ApplyTransform(EntityId entity, float x, float y, float z, float rotationY)
    {
        TransformComponent transform = EntityManager.HasComponent<TransformComponent>(entity)
            ? EntityManager.GetComponent<TransformComponent>(entity)
            : default;

        transform.X = x;
        transform.Y = y;
        transform.Z = z;
        transform.RotationY = rotationY;

        if (EntityManager.HasComponent<TransformComponent>(entity))
        {
            EntityManager.SetComponent(entity, transform);
        }
        else
        {
            EntityManager.AddComponent(entity, transform);
        }
    }

    private void ApplyOwnership(EntityId entity, Guid ownerClientId)
    {
        if (ownerClientId == Guid.Empty)
        {
            return;
        }

        OwnerComponent owner = EntityManager.HasComponent<OwnerComponent>(entity)
            ? EntityManager.GetComponent<OwnerComponent>(entity)
            : default;
        owner.OwnerClientId = ownerClientId;

        if (EntityManager.HasComponent<OwnerComponent>(entity))
        {
            EntityManager.SetComponent(entity, owner);
        }
        else
        {
            EntityManager.AddComponent(entity, owner);
        }
    }

    private void EnsureMoverDefaults(EntityId entity)
    {
        if (EntityManager.HasComponent<SandboxMoverComponent>(entity))
        {
            return;
        }

        EntityManager.AddComponent(entity, new SandboxMoverComponent
        {
            PlanarUnitsPerInputTick = MovementConstants.PlanarUnitsPerInputTick
        });
    }

    private string? ResolvePrototypeId(string entityType)
    {
        return _prototypeManager.TryIndex<EntityPrototype>(entityType, out _)
            ? entityType
            : null;
    }

    private void RecordReplicationChangesForCurrentTick()
    {
        if (_dirtyTracker == null)
        {
            return;
        }

        // Compares protobuf-backed component bundles per net id against the snapshot taken at the end of the prior tick.
        RefreshEntityCache();
        IReadOnlyList<ReplicatedEntityState> replicatedStates = _replicationSnapshotBuilder.Build(EntityManager.World, _entityHandles);
        Dictionary<int, ReplicatedEntityState> currentStates = [];
        foreach (ReplicatedEntityState replicatedState in replicatedStates)
        {
            currentStates.Add(replicatedState.EntityId, replicatedState);
            if (!_lastReplicatedStates.TryGetValue(replicatedState.EntityId, out ReplicatedEntityState? previousState)
                || !ReplicatedEntityStatesEqual(previousState, replicatedState))
            {
                _dirtyTracker.MarkDirty(replicatedState.EntityId, CurrentTick);
            }
        }

        // Any net id that vanished from the live world since the last capture is a removal for delta subscribers.
        foreach (int previousEntityId in _lastReplicatedStates.Keys)
        {
            if (!currentStates.ContainsKey(previousEntityId))
            {
                _dirtyTracker.MarkRemoved(previousEntityId, CurrentTick);
            }
        }

        _lastReplicatedStates.Clear();
        foreach (KeyValuePair<int, ReplicatedEntityState> pair in currentStates)
        {
            _lastReplicatedStates.Add(pair.Key, pair.Value);
        }
    }

    private void RefreshEntityCache()
    {
        // Cheap skip when ECS membership and structural version match the last time we walked the actor query.
        if (!_entitiesDirty
            && _lastCachedWorldCount == EntityManager.World.Count
            && _lastCachedWorldChangeVersion == EntityManager.World.CurrentChangeVersion)
        {
            return;
        }

        _entityHandles.Clear();
        List<EntityState> orderedStates = [];

        ComponentQueryEnumerator<SandboxActorComponent, TransformComponent> query =
            EntityManager.World.Query<SandboxActorComponent, TransformComponent>().GetEnumerator();
        while (query.MoveNext())
        {
            EntityId entity = query.Entity;
            ref readonly SandboxActorComponent actor = ref query.Component1;
            ref readonly TransformComponent transform = ref query.Component2;

            _entityHandles[actor.NetEntityId] = entity;
            orderedStates.Add(new EntityState(
                actor.NetEntityId,
                transform.X,
                transform.Y,
                transform.Z,
                transform.RotationY));
        }

        _entityStates.ReplaceAll(orderedStates);
        _entitiesDirty = false;
        _lastCachedWorldCount = EntityManager.World.Count;
        _lastCachedWorldChangeVersion = EntityManager.World.CurrentChangeVersion;
    }

    private static bool ReplicatedEntityStatesEqual(ReplicatedEntityState left, ReplicatedEntityState right)
    {
        if (left.EntityId != right.EntityId || left.Components.Count != right.Components.Count)
        {
            return false;
        }

        for (int componentIndex = 0; componentIndex < left.Components.Count; componentIndex++)
        {
            ReplicatedComponentState leftComponent = left.Components[componentIndex];
            ReplicatedComponentState rightComponent = right.Components[componentIndex];
            if (leftComponent.ComponentId != rightComponent.ComponentId
                || !leftComponent.Payload.AsSpan().SequenceEqual(rightComponent.Payload))
            {
                return false;
            }
        }

        return true;
    }
}
