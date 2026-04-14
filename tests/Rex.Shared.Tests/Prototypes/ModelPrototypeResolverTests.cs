using Rex.Shared.Assets.Rdm;
using Rex.Shared.Prototypes;
using Rex.Shared.Resources;
using Rex.Shared.Serialization.Manager;
using Rex.Shared.Tests.Support;

namespace Rex.Shared.Tests.Prototypes;

public sealed class ModelPrototypeResolverTests
{
    [Fact]
    public void Resolve_repository_manny_model_prototype()
    {
        ResourceManager resourceManager = new(EngineRepositoryPaths.GetResourcesRoot());
        SerializationManager serializationManager = new();
        PrototypeManager prototypeManager = new(serializationManager);
        SharedPrototypeBootstrap.RegisterAll(prototypeManager);
        prototypeManager.LoadResources(resourceManager);

        RdmCatalog catalog = new();
        catalog.LoadResources(resourceManager);

        ModelPrototypeResolver resolver = new(prototypeManager, catalog);
        ResolvedModelPrototype resolved = resolver.Resolve("mannyRefModel");

        Assert.Equal("Models/MannyRef/manny.rdm", resolved.Prototype.Rdm);
        Assert.Equal("humanoid_attachments", resolved.PackagePrototype?.Name);
        Assert.Equal("reference", resolved.DefaultState);
        Assert.Contains(resolved.Package.Definition.Sources, source => source.Format == "fbx");
    }

    [Fact]
    public void Resolve_throws_when_model_prototype_references_missing_package_prototype()
    {
        string root = Path.Combine(Path.GetTempPath(), $"rex-model-prototypes-{Guid.NewGuid():N}");
        string prototypeDirectory = Path.Combine(root, SharedResourceDirectories.Prototypes, "base");
        string packageDirectory = Path.Combine(root, SharedResourceDirectories.Models, "sample");
        string sourceDirectory = Path.Combine(packageDirectory, "source");
        _ = Directory.CreateDirectory(prototypeDirectory);
        _ = Directory.CreateDirectory(sourceDirectory);
        File.WriteAllText(Path.Combine(sourceDirectory, "sample.fbx"), string.Empty);
        File.WriteAllText(
            Path.Combine(packageDirectory, "sample.rdm"),
            /*lang=json,strict*/ """
            {
              "version": 1,
              "license": "CC0-1.0",
              "copyright": "Example",
              "size": {
                "x": 1.0,
                "y": 1.0,
                "z": 1.0
              },
              "sources": [
                {
                  "id": "sample_mesh",
                  "kind": "model",
                  "format": "fbx",
                  "path": "source/sample.fbx"
                }
              ],
              "states": [
                {
                  "name": "idle",
                  "source": "sample_mesh"
                }
              ]
            }
            """);
        File.WriteAllText(
            Path.Combine(prototypeDirectory, "models.prototype.json"),
            /*lang=json,strict*/ """
            [
              {
                "type": "model",
                "id": "badModel",
                "rdm": "Models/sample/sample.rdm",
                "packagePrototype": "missing_proto"
              }
            ]
            """);

        try
        {
            ResourceManager resourceManager = new(root);
            SerializationManager serializationManager = new();
            PrototypeManager prototypeManager = new(serializationManager);
            SharedPrototypeBootstrap.RegisterAll(prototypeManager);
            prototypeManager.LoadResources(resourceManager);

            RdmCatalog catalog = new();
            catalog.LoadResources(resourceManager);

            ModelPrototypeResolver resolver = new(prototypeManager, catalog);
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => resolver.Resolve("badModel"));
            Assert.Contains("missing RDM package prototype", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

}
