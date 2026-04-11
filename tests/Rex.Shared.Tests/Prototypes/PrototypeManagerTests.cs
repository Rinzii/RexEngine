using Rex.Shared.Components.BuiltIn;
using Rex.Shared.Components.Registration;
using Rex.Shared.Entities;
using Rex.Shared.Entities.World;
using Rex.Shared.Prototypes;
using Rex.Shared.Resources;
using Rex.Shared.Serialization.Manager;

namespace Rex.Shared.Tests.Prototypes;

public sealed class PrototypeManagerTests
{
    [Fact]
    public void LoadResources_composes_entity_prototypes_and_spawns_entities()
    {
        string resourceRoot = CreateTempResourceRoot(
                                 /*lang=json,strict*/
                                 """
            [
              {
                "type": "entity",
                "id": "baseEntity",
                "name": "Base Entity",
                "components": {
                  "transform": {
                    "x": 3.5,
                    "rotationY": 45
                  }
                }
              },
              {
                "type": "entity",
                "id": "ownedEntity",
                "parent": "baseEntity",
                "components": {
                  "owner": {
                    "ownerClientId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
                  }
                }
              }
            ]
            """);

        try
        {
            SerializationManager serializationManager = new();
            PrototypeManager prototypeManager = new(serializationManager);
            SharedPrototypeBootstrap.RegisterAll(prototypeManager);

            ResourceManager resourceManager = new(resourceRoot);
            prototypeManager.LoadResources(resourceManager);

            EntityPrototype prototype = prototypeManager.Index<EntityPrototype>("ownedEntity");
            Assert.Equal("ownedEntity", prototype.Id);
            Assert.Equal("baseEntity", prototype.Parent);
            Assert.Contains("transform", prototype.Components.Keys);
            Assert.Contains("owner", prototype.Components.Keys);

            ComponentRegistry registry = new();
            SharedEcsBootstrap.RegisterAll(registry);
            EcsWorld world = new(registry);
            EntityPrototypeSpawner spawner = new(prototypeManager, serializationManager);

            EntityId entity = spawner.Spawn(world, "ownedEntity");

            Assert.True(world.Has<TransformComponent>(entity));
            Assert.True(world.Has<OwnerComponent>(entity));
            Assert.True(world.Has<MetaDataComponent>(entity));
            Assert.Equal(3.5f, world.Get<TransformComponent>(entity).X);
            Assert.Equal(45f, world.Get<TransformComponent>(entity).RotationY);
            Assert.Equal(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"), world.Get<OwnerComponent>(entity).OwnerClientId);
            Assert.Equal("ownedEntity", world.Get<MetaDataComponent>(entity).PrototypeId);
        }
        finally
        {
            Directory.Delete(resourceRoot, recursive: true);
        }
    }

    [Fact]
    public void LoadDirectory_rejects_unknown_prototype_types()
    {
        string resourceRoot = CreateTempResourceRoot(
                                 /*lang=json,strict*/
                                 """
            [
              {
                "type": "unknown",
                "id": "bad"
              }
            ]
            """);

        try
        {
            SerializationManager serializationManager = new();
            PrototypeManager prototypeManager = new(serializationManager);
            SharedPrototypeBootstrap.RegisterAll(prototypeManager);

            string prototypesDirectory = Path.Combine(resourceRoot, SharedResourceDirectories.Prototypes);
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => prototypeManager.LoadDirectory(prototypesDirectory));
            Assert.Contains("unregistered prototype type 'unknown'", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(resourceRoot, recursive: true);
        }
    }

    [Fact]
    public void LoadDirectory_reads_binary_prototype_files()
    {
        string root = Path.Combine(Path.GetTempPath(), $"rex-prototypes-{Guid.NewGuid():N}");
        string prototypeDirectory = Path.Combine(root, SharedResourceDirectories.Prototypes, "base");
        string modelsDirectory = Path.Combine(root, SharedResourceDirectories.Models);
        _ = Directory.CreateDirectory(prototypeDirectory);
        _ = Directory.CreateDirectory(modelsDirectory);

        const string PrototypeJson = /*lang=json,strict*/ """
            [
              {
                "type": "model",
                "id": "baseModel",
                "rdm": "Models/sample/sample.rdm"
              },
              {
                "type": "model",
                "id": "sampleModel",
                "parent": "baseModel",
                "packagePrototype": "sample_render",
                "defaultState": "idle"
              }
            ]
            """;

        try
        {
            DataNode node = DataNodeJsonSerializer.Read(PrototypeJson);
            File.WriteAllBytes(
                Path.Combine(prototypeDirectory, "models.prototype.bjson"),
                DataNodeBinaryJsonSerializer.Write(node));

            SerializationManager serializationManager = new();
            PrototypeManager prototypeManager = new(serializationManager);
            SharedPrototypeBootstrap.RegisterAll(prototypeManager);

            prototypeManager.LoadDirectory(Path.Combine(root, SharedResourceDirectories.Prototypes));

            ModelPrototype prototype = prototypeManager.Index<ModelPrototype>("sampleModel");
            Assert.Equal("Models/sample/sample.rdm", prototype.Rdm);
            Assert.Equal("sample_render", prototype.PackagePrototype);
            Assert.Equal("idle", prototype.DefaultState);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadResources_indexes_typed_map_scene_and_category_prototypes()
    {
        string root = Path.Combine(Path.GetTempPath(), $"rex-prototypes-{Guid.NewGuid():N}");
        string prototypeDirectory = Path.Combine(root, SharedResourceDirectories.Prototypes, "base");
        string mapDirectory = Path.Combine(root, SharedResourceDirectories.Maps, "base");
        _ = Directory.CreateDirectory(prototypeDirectory);
        _ = Directory.CreateDirectory(mapDirectory);
        File.WriteAllText(
            Path.Combine(mapDirectory, "starter.map.json"),
            /*lang=json,strict*/
            """
            {
              "version": 1
            }
            """);
        File.WriteAllText(
            Path.Combine(prototypeDirectory, "entities.prototype.json"),
            /*lang=json,strict*/
            """
            [
              {
                "type": "entity",
                "id": "ownedEntity",
                "components": {}
              }
            ]
            """);
        File.WriteAllText(
            Path.Combine(prototypeDirectory, "maps.prototype.json"),
            /*lang=json,strict*/
            """
            [
              {
                "type": "map",
                "id": "starterMap",
                "map": "Maps/base/starter.map.json",
                "scene": "starterScene"
              }
            ]
            """);
        File.WriteAllText(
            Path.Combine(prototypeDirectory, "scenes.prototype.json"),
            /*lang=json,strict*/
            """
            [
              {
                "type": "scene",
                "id": "starterScene",
                "map": "starterMap",
                "entities": [
                  {
                    "prototype": "ownedEntity",
                    "x": 0,
                    "y": 0,
                    "z": 0,
                    "rotationY": 0
                  }
                ]
              }
            ]
            """);
        File.WriteAllText(
            Path.Combine(prototypeDirectory, "categories.prototype.json"),
            /*lang=json,strict*/
            """
            [
              {
                "type": "prototypeCategory",
                "id": "entity_catalog",
                "prototypeType": "entity",
                "entries": [
                  "ownedEntity"
                ]
              }
            ]
            """);

        try
        {
            SerializationManager serializationManager = new();
            PrototypeManager prototypeManager = new(serializationManager);
            SharedPrototypeBootstrap.RegisterAll(prototypeManager);
            prototypeManager.LoadResources(new ResourceManager(root));

            MapPrototype map = prototypeManager.Index(new MapPrototypeId("starterMap"));
            ScenePrototype scene = prototypeManager.Index(new ScenePrototypeId("starterScene"));
            PrototypeCategoryPrototype category = prototypeManager.Index(new PrototypeCategoryId("entity_catalog"));

            Assert.Equal("starterScene", map.Scene);
            Assert.Equal("starterMap", scene.Map);
            Assert.Equal(["ownedEntity"], category.Entries);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ReloadResources_clears_previous_definitions_and_raises_event()
    {
        string resourceRoot = CreateTempResourceRoot(
                                 /*lang=json,strict*/
                                 """
            [
              {
                "type": "entity",
                "id": "firstEntity",
                "components": {}
              }
            ]
            """);

        try
        {
            SerializationManager serializationManager = new();
            PrototypeManager prototypeManager = new(serializationManager);
            SharedPrototypeBootstrap.RegisterAll(prototypeManager);
            PrototypeReloadedEventArgs? reloadArgs = null;
            prototypeManager.Reloaded += (_, args) => reloadArgs = args;
            ResourceManager resourceManager = new(resourceRoot);

            prototypeManager.LoadResources(resourceManager);
            File.WriteAllText(
                Path.Combine(resourceRoot, SharedResourceDirectories.Prototypes, "base", "entities.prototype.json"),
                /*lang=json,strict*/
                """
                [
                  {
                    "type": "entity",
                    "id": "secondEntity",
                    "components": {}
                  }
                ]
                """);

            prototypeManager.ReloadResources(resourceManager);

            Assert.False(prototypeManager.TryIndex<EntityPrototype>("firstEntity", out _));
            Assert.True(prototypeManager.TryIndex(new EntityPrototypeId("secondEntity"), out EntityPrototype? prototype));
            Assert.Equal("secondEntity", prototype.Id);
            Assert.NotNull(reloadArgs);
            Assert.True(reloadArgs!.Version >= 2);
        }
        finally
        {
            Directory.Delete(resourceRoot, recursive: true);
        }
    }

    [Fact]
    public void Abstract_entity_prototypes_can_compose_but_cannot_spawn()
    {
        string resourceRoot = CreateTempResourceRoot(
                                 /*lang=json,strict*/
                                 """
            [
              {
                "type": "entity",
                "id": "baseEntity",
                "abstract": true,
                "components": {
                  "transform": {
                    "x": 1
                  }
                }
              },
              {
                "type": "entity",
                "id": "childEntity",
                "parent": "baseEntity",
                "components": {
                  "owner": {
                    "ownerClientId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
                  }
                }
              }
            ]
            """);

        try
        {
            SerializationManager serializationManager = new();
            PrototypeManager prototypeManager = new(serializationManager);
            SharedPrototypeBootstrap.RegisterAll(prototypeManager);

            ResourceManager resourceManager = new(resourceRoot);
            prototypeManager.LoadResources(resourceManager);

            EntityPrototype abstractPrototype = prototypeManager.Index<EntityPrototype>("baseEntity");
            Assert.True(abstractPrototype.Abstract);

            ComponentRegistry registry = new();
            SharedEcsBootstrap.RegisterAll(registry);
            EcsWorld world = new(registry);
            EntityPrototypeSpawner spawner = new(prototypeManager, serializationManager);

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() => spawner.Spawn(world, "baseEntity"));
            Assert.Contains("abstract", exception.Message, StringComparison.Ordinal);

            EntityId child = spawner.Spawn(world, "childEntity");
            Assert.True(world.Has<TransformComponent>(child));
            Assert.True(world.Has<OwnerComponent>(child));
            Assert.True(world.Has<MetaDataComponent>(child));
            Assert.Equal("childEntity", world.Get<MetaDataComponent>(child).PrototypeId);
        }
        finally
        {
            Directory.Delete(resourceRoot, recursive: true);
        }
    }

    [Fact]
    public void LoadResources_composes_multiple_parents_in_declared_order()
    {
        string resourceRoot = CreateTempResourceRoot(
                                 /*lang=json,strict*/
                                 """
            [
              {
                "type": "entity",
                "id": "baseTransform",
                "abstract": true,
                "components": {
                  "transform": {
                    "x": 1,
                    "rotationY": 10
                  }
                }
              },
              {
                "type": "entity",
                "id": "baseOwner",
                "abstract": true,
                "components": {
                  "owner": {
                    "ownerClientId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
                  }
                }
              },
              {
                "type": "entity",
                "id": "leafEntity",
                "parents": [
                  "baseTransform",
                  "baseOwner"
                ],
                "components": {
                  "transform": {
                    "x": 4
                  }
                }
              }
            ]
            """);

        try
        {
            SerializationManager serializationManager = new();
            PrototypeManager prototypeManager = new(serializationManager);
            SharedPrototypeBootstrap.RegisterAll(prototypeManager);
            prototypeManager.LoadResources(new ResourceManager(resourceRoot));

            ComponentRegistry registry = new();
            SharedEcsBootstrap.RegisterAll(registry);
            EcsWorld world = new(registry);
            EntityPrototypeSpawner spawner = new(prototypeManager, serializationManager);

            EntityId entity = spawner.Spawn(world, "leafEntity");
            TransformComponent transform = world.Get<TransformComponent>(entity);
            OwnerComponent owner = world.Get<OwnerComponent>(entity);
            MetaDataComponent metadata = world.Get<MetaDataComponent>(entity);

            Assert.Equal(4f, transform.X);
            Assert.Equal(10f, transform.RotationY);
            Assert.Equal(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"), owner.OwnerClientId);
            Assert.Equal("leafEntity", metadata.PrototypeId);
        }
        finally
        {
            Directory.Delete(resourceRoot, recursive: true);
        }
    }

    [Fact]
    public void EnumerateParents_returns_inheritance_chain_in_order()
    {
        string resourceRoot = CreateTempResourceRoot(
                                 /*lang=json,strict*/
                                 """
            [
              {
                "type": "entity",
                "id": "baseEntity",
                "abstract": true,
                "components": {
                  "transform": {
                    "x": 1
                  }
                }
              },
              {
                "type": "entity",
                "id": "midEntity",
                "parent": "baseEntity",
                "components": {}
              },
              {
                "type": "entity",
                "id": "leafEntity",
                "parent": "midEntity",
                "components": {}
              }
            ]
            """);

        try
        {
            SerializationManager serializationManager = new();
            PrototypeManager prototypeManager = new(serializationManager);
            SharedPrototypeBootstrap.RegisterAll(prototypeManager);
            prototypeManager.LoadResources(new ResourceManager(resourceRoot));

            string[] ids = prototypeManager.EnumerateParents<EntityPrototype>("leafEntity", includeSelf: true)
                .Select(static prototype => prototype.Id)
                .ToArray();

            Assert.Equal(["leafEntity", "midEntity", "baseEntity"], ids);
        }
        finally
        {
            Directory.Delete(resourceRoot, recursive: true);
        }
    }

    [Fact]
    public void EnumeratePrototypes_skips_abstract_prototypes()
    {
        string resourceRoot = CreateTempResourceRoot(
                                 /*lang=json,strict*/
                                 """
            [
              {
                "type": "entity",
                "id": "baseEntity",
                "abstract": true,
                "components": {}
              },
              {
                "type": "entity",
                "id": "childEntity",
                "parent": "baseEntity",
                "components": {}
              }
            ]
            """);

        try
        {
            SerializationManager serializationManager = new();
            PrototypeManager prototypeManager = new(serializationManager);
            SharedPrototypeBootstrap.RegisterAll(prototypeManager);
            prototypeManager.LoadResources(new ResourceManager(resourceRoot));

            string[] ids = prototypeManager.EnumeratePrototypes<EntityPrototype>()
                .Select(static prototype => prototype.Id)
                .ToArray();

            Assert.Equal(["childEntity"], ids);
            Assert.True(prototypeManager.HasIndex<EntityPrototype>("baseEntity"));
        }
        finally
        {
            Directory.Delete(resourceRoot, recursive: true);
        }
    }

    private static string CreateTempResourceRoot(string prototypeJson)
    {
        string root = Path.Combine(Path.GetTempPath(), $"rex-prototypes-{Guid.NewGuid():N}");
        string prototypeDirectory = Path.Combine(root, SharedResourceDirectories.Prototypes, "base");
        string modelsDirectory = Path.Combine(root, SharedResourceDirectories.Models);
        _ = Directory.CreateDirectory(prototypeDirectory);
        _ = Directory.CreateDirectory(modelsDirectory);
        File.WriteAllText(Path.Combine(prototypeDirectory, "entities.prototype.json"), prototypeJson);
        return root;
    }
}
