using Rex.Shared.Components.BuiltIn;
using Rex.Shared.Components.Registration;
using Rex.Shared.Entities;
using Rex.Shared.Entities.World;
using Rex.Shared.GameObjects;
using Rex.Shared.IoC;
using Rex.Shared.Prototypes;
using Rex.Shared.Resources;
using Rex.Shared.Serialization.Manager;
using Rex.Shared.Tests.Entities.Support;
using ByRefEventAttribute = Rex.Shared.GameObjects.ByRefEventAttribute;

namespace Rex.Shared.Tests.Systems;

public sealed class EntitySystemManagerTests
{
    public EntitySystemManagerTests()
    {
        IoCManager.Clear();
    }

    [Fact]
    public void Initialize_injects_dependencies_and_dispatches_local_events()
    {
        ComponentRegistry registry = EcsTestSupport.CreateRegistry();
        EcsWorld world = new(registry);
        EntitySystemManager manager = new(world);
        CountingSystem countingSystem = manager.AddSystem<CountingSystem>();
        _ = manager.AddSystem<DependencySystem>();
        manager.Initialize();

        EntityId entity = manager.CreateEntity();
        world.Set(entity, new TransformComponent { X = 1 });

        PingEvent pingEvent = new(5);
        manager.RaiseLocalEvent(entity, ref pingEvent, broadcast: true);

        Assert.True(countingSystem.DependencyResolved);
        Assert.Equal(1, countingSystem.DirectedCount);
        Assert.Equal(1, countingSystem.BroadcastCount);
        Assert.Equal(6, world.Get<TransformComponent>(entity).X);
        Assert.Equal(2, pingEvent.HandlerCount);
    }

    [Fact]
    public void Structural_operations_raise_component_lifecycle_events()
    {
        ComponentRegistry registry = EcsTestSupport.CreateRegistry();
        EcsWorld world = new(registry);
        EntitySystemManager manager = new(world);
        LifecycleSystem lifecycleSystem = manager.AddSystem<LifecycleSystem>();
        manager.Initialize();

        EntityId entity = manager.CreateEntity();
        Assert.Equal(["added", "init", "startup"], lifecycleSystem.Events);

        lifecycleSystem.Events.Clear();
        Assert.True(manager.RemoveComponent<TransformComponent>(entity));
        Assert.Equal(["shutdown", "remove"], lifecycleSystem.Events);

        lifecycleSystem.Events.Clear();
        manager.AddComponent(entity, new TransformComponent());
        lifecycleSystem.Events.Clear();
        Assert.True(manager.DestroyEntity(entity));
        Assert.Equal(["terminating", "shutdown", "remove"], lifecycleSystem.Events);
    }

    [Fact]
    public void Update_flushes_deferred_command_buffers_after_system_execution()
    {
        ComponentRegistry registry = EcsTestSupport.CreateRegistry();
        EcsWorld world = new(registry);
        EntitySystemManager manager = new(world);
        DeferredQueueSystem deferredQueueSystem = manager.AddSystem<DeferredQueueSystem>();
        _ = manager.AddSystem<DeferredListenerSystem>();
        manager.Initialize();

        EntityId entity = manager.CreateEntity();
        world.Set(entity, new TransformComponent { X = 1f });
        deferredQueueSystem.TargetEntity = entity;

        manager.Update(0.016f);

        Assert.Equal(1f, deferredQueueSystem.ObservedBeforeFlush);
        Assert.Equal(1f, deferredQueueSystem.ObservedAfterQueueing);
        Assert.Equal(5f, world.Get<TransformComponent>(entity).X);
        Assert.Equal(1, deferredQueueSystem.DeferredEventCount);
    }

    [Fact]
    public void FlushDeferred_replays_queued_structural_changes_through_lifecycle_events()
    {
        ComponentRegistry registry = EcsTestSupport.CreateRegistry();
        EcsWorld world = new(registry);
        EntitySystemManager manager = new(world);
        LifecycleSystem lifecycleSystem = manager.AddSystem<LifecycleSystem>();
        manager.Initialize();

        EntityId entity = manager.CreateEntity();
        world.Set(entity, new TransformComponent { X = 1f });
        lifecycleSystem.Events.Clear();

        manager.QueueSetComponent(entity, new TransformComponent { X = 9f });
        manager.QueueRemoveComponent<TransformComponent>(entity);
        Assert.True(manager.HasDeferredWork);
        manager.FlushDeferred();

        Assert.False(world.Has<TransformComponent>(entity));
        Assert.Equal(["shutdown", "remove"], lifecycleSystem.Events);

        EntityId secondEntity = manager.CreateEntity();
        lifecycleSystem.Events.Clear();

        manager.QueueDeleteEntity(secondEntity);
        manager.FlushDeferred();

        Assert.False(world.Exists(secondEntity));
        Assert.Equal(["terminating", "shutdown", "remove"], lifecycleSystem.Events);
    }

    [Fact]
    public void Directed_read_only_handlers_do_not_advance_component_versions()
    {
        ComponentRegistry registry = EcsTestSupport.CreateRegistry();
        EcsWorld world = new(registry);
        EntitySystemManager manager = new(world);
        ReadOnlyObserverSystem observerSystem = manager.AddSystem<ReadOnlyObserverSystem>();
        manager.Initialize();

        EntityId entity = manager.CreateEntity();
        uint beforeVersion = world.GetComponentVersion<TransformComponent>(entity);

        ReadOnlyPingEvent pingEvent = new();
        manager.RaiseLocalEvent(entity, ref pingEvent);

        Assert.Equal(1, observerSystem.EventCount);
        Assert.Equal(beforeVersion, world.GetComponentVersion<TransformComponent>(entity));
    }

    [Fact]
    public void FlushDeferred_replays_boxed_component_queue_operations()
    {
        ComponentRegistry registry = EcsTestSupport.CreateRegistry();
        EcsWorld world = new(registry);
        EntitySystemManager manager = new(world);
        EntityId entity = manager.CreateEntity();

        manager.QueueRemoveComponent(entity, typeof(TransformComponent));
        manager.FlushDeferred();
        Assert.False(world.Has<TransformComponent>(entity));

        manager.QueueAddComponent(entity, typeof(TransformComponent), new TransformComponent { X = 11f, RotationY = 20f });
        manager.FlushDeferred();

        TransformComponent transform = world.Get<TransformComponent>(entity);
        Assert.Equal(11f, transform.X);
        Assert.Equal(20f, transform.RotationY);

        manager.QueueSetComponent(entity, typeof(TransformComponent), new TransformComponent { X = 14f, RotationY = 25f });
        manager.FlushDeferred();

        transform = world.Get<TransformComponent>(entity);
        Assert.Equal(14f, transform.X);
        Assert.Equal(25f, transform.RotationY);

        manager.QueueRemoveComponent(entity, typeof(TransformComponent));
        manager.FlushDeferred();

        Assert.False(world.Has<TransformComponent>(entity));
    }

    [Fact]
    public void Update_orders_systems_by_declared_phase_before_registration_order()
    {
        ComponentRegistry registry = EcsTestSupport.CreateRegistry();
        EcsWorld world = new(registry);
        EntitySystemManager manager = new(world);
        PhaseRecorderSystem executionOrder = manager.AddSystem<PhaseRecorderSystem>();
        _ = manager.AddSystem<PostPhaseSystem>();
        _ = manager.AddSystem<PrePhaseSystem>();
        manager.Initialize();

        manager.Update(0.016f);
        manager.FrameUpdate(0.016f);

        Assert.Equal(
            ["fixed:pre", "fixed:normal", "fixed:post", "frame:pre", "frame:normal", "frame:post"],
            executionOrder.Events);
    }

    [Fact]
    public void Update_forbids_direct_world_structural_mutation_inside_system_execution()
    {
        ComponentRegistry registry = EcsTestSupport.CreateRegistry();
        EcsWorld world = new(registry);
        EntitySystemManager manager = new(world);
        DirectWorldMutationSystem system = manager.AddSystem<DirectWorldMutationSystem>();
        manager.Initialize();

        manager.Update(0.016f);

        Assert.NotNull(system.Exception);
        Assert.Contains("entity command buffer", system.Exception!.Message, StringComparison.Ordinal);
        Assert.Equal(0, world.Count);
    }

    [Fact]
    public void Initialize_batches_non_conflicting_parallel_systems_together()
    {
        ComponentRegistry registry = EcsTestSupport.CreateRegistry();
        EcsWorld world = new(registry);
        EntitySystemManager manager = new(world);
        _ = manager.AddSystem<ParallelTransformSystem>();
        _ = manager.AddSystem<ParallelHealthSystem>();

        manager.Initialize();

        Assert.Equal(1, manager.UpdateBatchCount);
    }

    [Fact]
    public void Initialize_splits_conflicting_parallel_systems_into_multiple_batches()
    {
        ComponentRegistry registry = EcsTestSupport.CreateRegistry();
        EcsWorld world = new(registry);
        EntitySystemManager manager = new(world);
        _ = manager.AddSystem<ParallelTransformSystem>();
        _ = manager.AddSystem<ParallelTransformSystemB>();

        manager.Initialize();

        Assert.Equal(2, manager.UpdateBatchCount);
    }

    [Fact]
    public void Initialize_does_not_batch_parallel_systems_with_explicit_ordering_dependencies()
    {
        ComponentRegistry registry = EcsTestSupport.CreateRegistry();
        EcsWorld world = new(registry);
        EntitySystemManager manager = new(world);
        _ = manager.AddSystem<OrderedParallelBeforeSystem>();
        _ = manager.AddSystem<OrderedParallelAfterSystem>();

        manager.Initialize();

        Assert.Equal(2, manager.UpdateBatchCount);
    }

    [Fact]
    public void Parallel_update_rejects_synchronous_local_events()
    {
        ComponentRegistry registry = EcsTestSupport.CreateRegistry();
        EcsWorld world = new(registry);
        EntitySystemManager manager = new(world);
        ParallelEventSourceSystem sourceSystem = manager.AddSystem<ParallelEventSourceSystem>();
        _ = manager.AddSystem<ParallelEventCompanionSystem>();
        manager.Initialize();

        manager.Update(0.016f);

        Assert.NotNull(sourceSystem.Exception);
        Assert.Contains("parallel system execution", sourceSystem.Exception!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parallel_update_preserves_deferred_command_playback_in_system_order()
    {
        ComponentRegistry registry = EcsTestSupport.CreateRegistry();
        EcsWorld world = new(registry);
        EntitySystemManager manager = new(world);
        ParallelQueuedOwnerSystemA systemA = manager.AddSystem<ParallelQueuedOwnerSystemA>();
        ParallelQueuedOwnerSystemB systemB = manager.AddSystem<ParallelQueuedOwnerSystemB>();
        manager.Initialize();

        EntityId entity = manager.CreateEntity();
        manager.AddComponent(entity, new OwnerComponent { OwnerClientId = Guid.Empty });

        ParallelQueueOrderingCoordinator coordinator = new();
        var firstOwner = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var secondOwner = Guid.Parse("ffffffff-1111-2222-3333-444444444444");
        systemA.Coordinator = coordinator;
        systemA.TargetEntity = entity;
        systemA.OwnerClientId = firstOwner;
        systemA.DelayMilliseconds = 40;
        systemB.Coordinator = coordinator;
        systemB.TargetEntity = entity;
        systemB.OwnerClientId = secondOwner;

        Assert.Equal(1, manager.UpdateBatchCount);

        manager.Update(0.016f);

        Assert.Equal(secondOwner, world.Get<OwnerComponent>(entity).OwnerClientId);
    }

    [Fact]
    public void Singleton_components_can_be_accessed_through_manager()
    {
        ComponentRegistry registry = EcsTestSupport.CreateRegistry();
        EcsWorld world = new(registry);
        EntitySystemManager manager = new(world);

        Assert.False(manager.HasSingleton<OwnerComponent>());

        manager.AddSingleton(new OwnerComponent
        {
            OwnerClientId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")
        });

        Assert.True(manager.HasSingleton<OwnerComponent>());
        Assert.Equal(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"), manager.GetSingleton<OwnerComponent>().OwnerClientId);

        ref OwnerComponent owner = ref manager.GetMutableSingletonRef<OwnerComponent>();
        owner.OwnerClientId = Guid.Parse("ffffffff-1111-2222-3333-444444444444");

        Assert.Equal(Guid.Parse("ffffffff-1111-2222-3333-444444444444"), manager.GetSingleton<OwnerComponent>().OwnerClientId);
        Assert.True(manager.RemoveSingleton<OwnerComponent>());
        Assert.False(manager.HasSingleton<OwnerComponent>());
    }

    [Fact]
    public void CreateEntity_adds_baseline_transform_and_metadata()
    {
        ComponentRegistry registry = EcsTestSupport.CreateRegistry();
        EcsWorld world = new(registry);
        EntitySystemManager manager = new(world);

        EntityId entity = manager.CreateEntity();

        Assert.True(world.Has<TransformComponent>(entity));
        Assert.True(world.Has<MetaDataComponent>(entity));

        MetaDataComponent metadata = world.Get<MetaDataComponent>(entity);
        Assert.Equal($"Entity {entity.Slot}", metadata.EntityName);
        Assert.Null(metadata.PrototypeId);
        Assert.Null(metadata.EntityDescription);
    }

    [Fact]
    public void FlushDeferred_can_create_entity_and_return_handle_via_callback()
    {
        ComponentRegistry registry = EcsTestSupport.CreateRegistry();
        EcsWorld world = new(registry);
        EntitySystemManager manager = new(world);
        EntityId created = EntityId.Invalid;

        manager.QueueCreateEntity(entity => created = entity);
        Assert.True(manager.HasDeferredWork);
        Assert.False(world.Exists(created));

        manager.FlushDeferred();

        Assert.NotEqual(EntityId.Invalid, created);
        Assert.True(world.Exists(created));
        Assert.Equal(1, world.Count);
    }

    [Fact]
    public void FlushDeferred_can_target_created_entity_placeholder_inside_same_buffer()
    {
        ComponentRegistry registry = EcsTestSupport.CreateRegistry();
        EcsWorld world = new(registry);
        EntitySystemManager manager = new(world);
        EntityId created = EntityId.Invalid;

        EntityCommandBuffer commandBuffer = manager.CreateCommandBuffer();
        DeferredEntity deferredEntity = commandBuffer.CreateDeferredEntity(entity => created = entity);
        commandBuffer.AddComponent(
            deferredEntity,
            new OwnerComponent { OwnerClientId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee") });
        commandBuffer.SetComponent(deferredEntity, new TransformComponent { X = 9f, RotationY = 30f });
        manager.EnqueueCommandBuffer(commandBuffer);

        manager.FlushDeferred();

        Assert.NotEqual(EntityId.Invalid, created);
        Assert.True(world.Exists(created));
        TransformComponent transform = world.Get<TransformComponent>(created);
        OwnerComponent owner = world.Get<OwnerComponent>(created);
        Assert.Equal(9f, transform.X);
        Assert.Equal(30f, transform.RotationY);
        Assert.Equal(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"), owner.OwnerClientId);
    }

    [Fact]
    public void FlushDeferred_can_target_created_entity_placeholder_with_boxed_components()
    {
        ComponentRegistry registry = EcsTestSupport.CreateRegistry();
        EcsWorld world = new(registry);
        EntitySystemManager manager = new(world);
        EntityId created = EntityId.Invalid;

        EntityCommandBuffer commandBuffer = manager.CreateCommandBuffer();
        DeferredEntity deferredEntity = commandBuffer.CreateDeferredEntity(entity => created = entity);
        commandBuffer.SetComponent(
            deferredEntity,
            typeof(MetaDataComponent),
            new MetaDataComponent
            {
                PrototypeId = "queued",
                EntityName = "Queued Entity",
                EntityDescription = "Created through one deferred buffer."
            });
        commandBuffer.AddComponent(
            deferredEntity,
            typeof(OwnerComponent),
            new OwnerComponent { OwnerClientId = Guid.Parse("ffffffff-1111-2222-3333-444444444444") });
        commandBuffer.RemoveComponent(deferredEntity, typeof(OwnerComponent));
        manager.EnqueueCommandBuffer(commandBuffer);

        manager.FlushDeferred();

        Assert.NotEqual(EntityId.Invalid, created);
        Assert.True(world.Exists(created));
        MetaDataComponent metadata = world.Get<MetaDataComponent>(created);
        Assert.False(world.Has<OwnerComponent>(created));
        Assert.Equal("queued", metadata.PrototypeId);
        Assert.Equal("Queued Entity", metadata.EntityName);
        Assert.Equal("Created through one deferred buffer.", metadata.EntityDescription);
    }

    [Fact]
    public void FlushDeferred_can_spawn_prototype_and_return_handle_via_callback()
    {
        string resourceRoot = CreateTempResourceRoot(
            /*lang=json,strict*/
            """
            [
              {
                "type": "entity",
                "id": "queuedEntity",
                "components": {
                  "transform": {
                    "x": 8.5,
                    "rotationY": 15
                  }
                }
              }
            ]
            """);

        try
        {
            EntitySystemManager manager = CreatePrototypeEnabledManager(resourceRoot);
            EntityId spawned = EntityId.Invalid;

            manager.QueueSpawnEntity("queuedEntity", entity => spawned = entity);
            manager.FlushDeferred();

            Assert.NotEqual(EntityId.Invalid, spawned);
            Assert.True(manager.World.Exists(spawned));
            TransformComponent transform = manager.World.Get<TransformComponent>(spawned);
            MetaDataComponent metadata = manager.World.Get<MetaDataComponent>(spawned);
            Assert.Equal(8.5f, transform.X);
            Assert.Equal(15f, transform.RotationY);
            Assert.Equal("queuedEntity", metadata.PrototypeId);
            Assert.Equal("queuedEntity", metadata.EntityName);
        }
        finally
        {
            Directory.Delete(resourceRoot, recursive: true);
        }
    }

    [Fact]
    public void FlushDeferred_can_target_spawned_entity_placeholder_inside_same_buffer()
    {
        string resourceRoot = CreateTempResourceRoot(
            /*lang=json,strict*/
            """
            [
              {
                "type": "entity",
                "id": "queuedEntity",
                "components": {
                  "transform": {
                    "x": 2,
                    "rotationY": 10
                  }
                }
              }
            ]
            """);

        try
        {
            EntitySystemManager manager = CreatePrototypeEnabledManager(resourceRoot);
            EntityId spawned = EntityId.Invalid;

            EntityCommandBuffer commandBuffer = manager.CreateCommandBuffer();
            DeferredEntity deferredEntity = commandBuffer.SpawnDeferredEntity("queuedEntity", entity => spawned = entity);
            commandBuffer.SetComponent(deferredEntity, new TransformComponent { X = 12f, RotationY = 60f });
            manager.EnqueueCommandBuffer(commandBuffer);

            manager.FlushDeferred();

            Assert.NotEqual(EntityId.Invalid, spawned);
            TransformComponent transform = manager.World.Get<TransformComponent>(spawned);
            Assert.Equal(12f, transform.X);
            Assert.Equal(60f, transform.RotationY);
        }
        finally
        {
            Directory.Delete(resourceRoot, recursive: true);
        }
    }

    [Fact]
    public void FlushDeferred_can_target_spawned_entity_placeholder_with_boxed_components()
    {
        string resourceRoot = CreateTempResourceRoot(
            /*lang=json,strict*/
            """
            [
              {
                "type": "entity",
                "id": "queuedEntity",
                "components": {
                  "transform": {
                    "x": 2,
                    "rotationY": 10
                  }
                }
              }
            ]
            """);

        try
        {
            EntitySystemManager manager = CreatePrototypeEnabledManager(resourceRoot);
            EntityId spawned = EntityId.Invalid;

            EntityCommandBuffer commandBuffer = manager.CreateCommandBuffer();
            DeferredEntity deferredEntity = commandBuffer.SpawnDeferredEntity("queuedEntity", entity => spawned = entity);
            commandBuffer.RemoveComponent(deferredEntity, typeof(TransformComponent));
            commandBuffer.AddComponent(
                deferredEntity,
                typeof(TransformComponent),
                new TransformComponent { X = 18f, RotationY = 90f });
            manager.EnqueueCommandBuffer(commandBuffer);

            manager.FlushDeferred();

            Assert.NotEqual(EntityId.Invalid, spawned);
            TransformComponent transform = manager.World.Get<TransformComponent>(spawned);
            Assert.Equal(18f, transform.X);
            Assert.Equal(90f, transform.RotationY);
        }
        finally
        {
            Directory.Delete(resourceRoot, recursive: true);
        }
    }

    [Fact]
    public void Enqueued_command_buffers_cannot_be_modified_or_reused()
    {
        ComponentRegistry registry = EcsTestSupport.CreateRegistry();
        EcsWorld world = new(registry);
        EntitySystemManager manager = new(world);

        EntityCommandBuffer commandBuffer = manager.CreateCommandBuffer();
        commandBuffer.CreateEntity();
        manager.EnqueueCommandBuffer(commandBuffer);

        _ = Assert.Throws<InvalidOperationException>(() => commandBuffer.CreateEntity());
        _ = Assert.Throws<InvalidOperationException>(() => manager.EnqueueCommandBuffer(commandBuffer));

        manager.FlushDeferred();

        _ = Assert.Throws<InvalidOperationException>(() => commandBuffer.CreateEntity());
        Assert.Equal(1, world.Count);
    }

    [Fact]
    public void Shutdown_then_initialize_does_not_duplicate_subscriptions()
    {
        ComponentRegistry registry = EcsTestSupport.CreateRegistry();
        EcsWorld world = new(registry);
        EntitySystemManager manager = new(world);
        CountingSystem countingSystem = manager.AddSystem<CountingSystem>();
        _ = manager.AddSystem<DependencySystem>();

        manager.Initialize();
        manager.Shutdown();
        manager.Initialize();

        EntityId entity = manager.CreateEntity();
        PingEvent pingEvent = new(5);
        manager.RaiseLocalEvent(entity, ref pingEvent, broadcast: true);

        Assert.Equal(1, countingSystem.DirectedCount);
        Assert.Equal(1, countingSystem.BroadcastCount);
    }

    [Fact]
    public void Directed_dispatch_rejects_immediate_structural_mutation_inside_handlers()
    {
        ComponentRegistry registry = EcsTestSupport.CreateRegistry();
        EcsWorld world = new(registry);
        EntitySystemManager manager = new(world);
        RemovingHandlerSystem removingHandlerSystem = manager.AddSystem<RemovingHandlerSystem>();
        manager.Initialize();

        EntityId entity = manager.CreateEntity();
        PingEvent pingEvent = new(3);
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => manager.RaiseLocalEvent(entity, ref pingEvent));

        Assert.Contains("entity command buffer", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, removingHandlerSystem.FirstHandlerCount);
        Assert.Equal(0, removingHandlerSystem.SecondHandlerCount);
        Assert.True(world.Has<TransformComponent>(entity));
    }

    [ByRefEvent]
    private struct PingEvent
    {
        public PingEvent(float amount)
        {
            Amount = amount;
            HandlerCount = 0;
        }

        public float Amount { get; }

        public int HandlerCount { get; set; }
    }

    private sealed class DependencySystem : EntitySystem
    {
        public int TouchCount { get; private set; }

        public void Touch()
        {
            TouchCount++;
        }
    }

    private sealed class CountingSystem : EntitySystem
    {
        [Dependency]
        private readonly DependencySystem _dependencySystem = default!;

        public bool DependencyResolved { get; private set; }

        public int DirectedCount { get; private set; }

        public int BroadcastCount { get; private set; }

        public override void Initialize()
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            DependencyResolved = _dependencySystem != null;
            SubscribeLocalEvent<TransformComponent, PingEvent>(OnPing);
            SubscribeLocalEvent<PingEvent>(OnPingBroadcast);
        }

        private void OnPing(EntityId entity, ref TransformComponent component, ref PingEvent args)
        {
            _ = entity;
            DirectedCount++;
            component.X += args.Amount;
            args.HandlerCount++;
            _dependencySystem.Touch();
        }

        private void OnPingBroadcast(ref PingEvent args)
        {
            BroadcastCount++;
            args.HandlerCount++;
        }
    }

    private sealed class LifecycleSystem : EntitySystem
    {
        public List<string> Events { get; } = [];

        public override void Initialize()
        {
            SubscribeReadOnlyLocalEvent<TransformComponent, ComponentAddedEvent>(OnAdded);
            SubscribeReadOnlyLocalEvent<TransformComponent, ComponentInitEvent>(OnInit);
            SubscribeReadOnlyLocalEvent<TransformComponent, ComponentStartupEvent>(OnStartup);
            SubscribeReadOnlyLocalEvent<TransformComponent, ComponentShutdownEvent>(OnShutdown);
            SubscribeReadOnlyLocalEvent<TransformComponent, ComponentRemoveEvent>(OnRemove);
            SubscribeReadOnlyLocalEvent<TransformComponent, EntityTerminatingEvent>(OnTerminating);
        }

        private void OnAdded(EntityId entity, in TransformComponent component, ref ComponentAddedEvent args)
        {
            _ = entity;
            _ = component;
            _ = args;
            Events.Add("added");
        }

        private void OnInit(EntityId entity, in TransformComponent component, ref ComponentInitEvent args)
        {
            _ = entity;
            _ = component;
            _ = args;
            Events.Add("init");
        }

        private void OnStartup(EntityId entity, in TransformComponent component, ref ComponentStartupEvent args)
        {
            _ = entity;
            _ = component;
            _ = args;
            Events.Add("startup");
        }

        private void OnShutdown(EntityId entity, in TransformComponent component, ref ComponentShutdownEvent args)
        {
            _ = entity;
            _ = component;
            _ = args;
            Events.Add("shutdown");
        }

        private void OnRemove(EntityId entity, in TransformComponent component, ref ComponentRemoveEvent args)
        {
            _ = entity;
            _ = component;
            _ = args;
            Events.Add("remove");
        }

        private void OnTerminating(EntityId entity, in TransformComponent component, ref EntityTerminatingEvent args)
        {
            _ = entity;
            _ = component;
            _ = args;
            Events.Add("terminating");
        }
    }

    private sealed class DeferredQueueSystem : EntitySystem
    {
        public EntityId TargetEntity { get; set; }

        public float ObservedBeforeFlush { get; private set; }

        public float ObservedAfterQueueing { get; private set; }

        public int DeferredEventCount { get; private set; }

        public override void Update(float frameTime)
        {
            _ = frameTime;
            if (TargetEntity == EntityId.Invalid || DeferredEventCount != 0)
            {
                return;
            }

            ObservedBeforeFlush = World.Get<TransformComponent>(TargetEntity).X;

            EntityCommandBuffer commandBuffer = CreateCommandBuffer();
            commandBuffer.SetComponent(TargetEntity, new TransformComponent { X = 2f });
            commandBuffer.RaiseLocalEvent(TargetEntity, new PingEvent(3f));
            EnqueueCommandBuffer(commandBuffer);

            ObservedAfterQueueing = World.Get<TransformComponent>(TargetEntity).X;
        }

        public void OnDeferredPing(EntityId entity, ref TransformComponent component, ref PingEvent args)
        {
            _ = entity;
            component.X += args.Amount;
            args.HandlerCount++;
            DeferredEventCount += args.HandlerCount;
        }
    }

    private sealed class DeferredListenerSystem : EntitySystem
    {
        public override void Initialize()
        {
            SubscribeLocalEvent<TransformComponent, PingEvent>(OnDeferredPing);
        }

        private void OnDeferredPing(EntityId entity, ref TransformComponent component, ref PingEvent args)
        {
            DeferredQueueSystem deferredQueueSystem = GetEntitySystem<DeferredQueueSystem>();
            deferredQueueSystem.OnDeferredPing(entity, ref component, ref args);
        }
    }

    [ByRefEvent]
    private struct ReadOnlyPingEvent;

    private sealed class ReadOnlyObserverSystem : EntitySystem
    {
        public int EventCount { get; private set; }

        public override void Initialize()
        {
            SubscribeReadOnlyLocalEvent<TransformComponent, ReadOnlyPingEvent>(OnPing);
        }

        private void OnPing(EntityId entity, in TransformComponent component, ref ReadOnlyPingEvent args)
        {
            _ = entity;
            _ = component;
            _ = args;
            EventCount++;
        }
    }

    private sealed class RemovingHandlerSystem : EntitySystem
    {
        public int FirstHandlerCount { get; private set; }

        public int SecondHandlerCount { get; private set; }

        public override void Initialize()
        {
            SubscribeLocalEvent<TransformComponent, PingEvent>(OnFirst, order: 0);
            SubscribeLocalEvent<TransformComponent, PingEvent>(OnSecond, order: 1);
        }

        private void OnFirst(EntityId entity, ref TransformComponent component, ref PingEvent args)
        {
            _ = component;
            _ = args;
            FirstHandlerCount++;
            _ = World.Remove<TransformComponent>(entity);
        }

        private void OnSecond(EntityId entity, ref TransformComponent component, ref PingEvent args)
        {
            _ = entity;
            _ = component;
            _ = args;
            SecondHandlerCount++;
        }
    }

    private sealed class PhaseRecorderSystem : EntitySystem
    {
        public List<string> Events { get; } = [];

        public override void Update(float frameTime)
        {
            _ = frameTime;
            Events.Add("fixed:normal");
        }

        public override void FrameUpdate(float frameTime)
        {
            _ = frameTime;
            Events.Add("frame:normal");
        }
    }

    private sealed class PrePhaseSystem : EntitySystem
    {
        protected internal override SystemUpdatePhase UpdatePhase => SystemUpdatePhase.PreUpdate;

        protected internal override FrameUpdatePhase FrameUpdatePhase => FrameUpdatePhase.PreFrame;

        public override void Update(float frameTime)
        {
            _ = frameTime;
            GetEntitySystem<PhaseRecorderSystem>().Events.Add("fixed:pre");
        }

        public override void FrameUpdate(float frameTime)
        {
            _ = frameTime;
            GetEntitySystem<PhaseRecorderSystem>().Events.Add("frame:pre");
        }
    }

    private sealed class PostPhaseSystem : EntitySystem
    {
        protected internal override SystemUpdatePhase UpdatePhase => SystemUpdatePhase.PostUpdate;

        protected internal override FrameUpdatePhase FrameUpdatePhase => FrameUpdatePhase.PostFrame;

        public override void Update(float frameTime)
        {
            _ = frameTime;
            GetEntitySystem<PhaseRecorderSystem>().Events.Add("fixed:post");
        }

        public override void FrameUpdate(float frameTime)
        {
            _ = frameTime;
            GetEntitySystem<PhaseRecorderSystem>().Events.Add("frame:post");
        }
    }

    private sealed class DirectWorldMutationSystem : EntitySystem
    {
        public Exception? Exception { get; private set; }

        public override void Update(float frameTime)
        {
            _ = frameTime;
            if (Exception != null)
            {
                return;
            }

            try
            {
                _ = World.CreateEntity();
            }
            catch (Exception exception)
            {
                Exception = exception;
            }
        }
    }

    private sealed class ParallelTransformSystem : EntitySystem
    {
        protected internal override bool SupportsParallelExecution => true;

        public override void Initialize()
        {
            DeclareComponentWrite<TransformComponent>();
        }
    }

    private sealed class ParallelTransformSystemB : EntitySystem
    {
        protected internal override bool SupportsParallelExecution => true;

        public override void Initialize()
        {
            DeclareComponentWrite<TransformComponent>();
        }
    }

    private sealed class ParallelHealthSystem : EntitySystem
    {
        protected internal override bool SupportsParallelExecution => true;

        public override void Initialize()
        {
            DeclareComponentWrite<HealthComponent>();
        }
    }

    private sealed class OrderedParallelBeforeSystem : EntitySystem
    {
        protected internal override bool SupportsParallelExecution => true;

        public override void Initialize()
        {
            DeclareComponentWrite<TransformComponent>();
        }
    }

    private sealed class OrderedParallelAfterSystem : EntitySystem
    {
        protected internal override bool SupportsParallelExecution => true;

        public override void Initialize()
        {
            UpdatesAfter.Add(typeof(OrderedParallelBeforeSystem));
            DeclareComponentWrite<HealthComponent>();
        }
    }

    private sealed class ParallelEventSourceSystem : EntitySystem
    {
        public Exception? Exception { get; private set; }

        protected internal override bool SupportsParallelExecution => true;

        public override void Initialize()
        {
            DeclareComponentWrite<TransformComponent>();
        }

        public override void Update(float frameTime)
        {
            _ = frameTime;
            if (Exception != null)
            {
                return;
            }

            try
            {
                PingEvent pingEvent = new(1f);
                RaiseLocalEvent(ref pingEvent);
            }
            catch (Exception exception)
            {
                Exception = exception;
            }
        }
    }

    private sealed class ParallelEventCompanionSystem : EntitySystem
    {
        protected internal override bool SupportsParallelExecution => true;

        public override void Initialize()
        {
            DeclareComponentWrite<HealthComponent>();
        }
    }

    private sealed class ParallelQueueOrderingCoordinator
    {
        private int _startedSystems;

        public void MarkStarted()
        {
            _ = Interlocked.Increment(ref _startedSystems);
        }

        public void WaitForPeer()
        {
            _ = SpinWait.SpinUntil(() => Volatile.Read(ref _startedSystems) >= 2, 200);
        }
    }

    private abstract class ParallelQueuedOwnerSystemBase : EntitySystem
    {
        public ParallelQueueOrderingCoordinator? Coordinator { get; set; }

        public EntityId TargetEntity { get; set; }

        public Guid OwnerClientId { get; set; }

        public int DelayMilliseconds { get; set; }

        protected internal override bool SupportsParallelExecution => true;

        public override void Initialize()
        {
            DeclareComponentWrite<TransformComponent>();
        }

        public override void Update(float frameTime)
        {
            _ = frameTime;
            Coordinator?.MarkStarted();
            Coordinator?.WaitForPeer();
            if (DelayMilliseconds > 0)
            {
                Thread.Sleep(DelayMilliseconds);
            }

            QueueSetComponent(TargetEntity, new OwnerComponent { OwnerClientId = OwnerClientId });
        }
    }

    private sealed class ParallelQueuedOwnerSystemA : ParallelQueuedOwnerSystemBase
    {
    }

    private sealed class ParallelQueuedOwnerSystemB : ParallelQueuedOwnerSystemBase
    {
        public override void Initialize()
        {
            DeclareComponentWrite<HealthComponent>();
        }
    }

    private static EntitySystemManager CreatePrototypeEnabledManager(string resourceRoot)
    {
        SerializationManager serializationManager = new();
        PrototypeManager prototypeManager = new(serializationManager);
        SharedPrototypeBootstrap.RegisterAll(prototypeManager);
        prototypeManager.LoadResources(new ResourceManager(resourceRoot));

        ComponentRegistry registry = EcsTestSupport.CreateRegistry();
        EcsWorld world = new(registry);
        EntityPrototypeSpawner spawner = new(prototypeManager, serializationManager);
        return new EntitySystemManager(world, spawner);
    }

    private static string CreateTempResourceRoot(string prototypeJson)
    {
        string root = Path.Combine(Path.GetTempPath(), $"rex-system-tests-{Guid.NewGuid():N}");
        string prototypeDirectory = Path.Combine(root, SharedResourceDirectories.Prototypes, "base");
        _ = Directory.CreateDirectory(prototypeDirectory);
        File.WriteAllText(Path.Combine(prototypeDirectory, "entities.prototype.json"), prototypeJson);
        return root;
    }
}
