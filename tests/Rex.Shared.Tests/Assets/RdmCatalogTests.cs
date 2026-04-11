using System.Runtime.CompilerServices;
using Rex.Shared.Assets.Rdm;
using Rex.Shared.Resources;

namespace Rex.Shared.Tests.Assets;

public sealed class RdmCatalogTests
{
    [Fact]
    public void LoadResources_indexes_temp_rdm_package_and_validates_files()
    {
        string root = Path.Combine(Path.GetTempPath(), $"rex-rdm-{Guid.NewGuid():N}");
        string packageDirectory = Path.Combine(root, SharedResourceDirectories.Models, "sample");
        string sourceDirectory = Path.Combine(packageDirectory, "source");
        string textureDirectory = Path.Combine(packageDirectory, "textures");
        _ = Directory.CreateDirectory(sourceDirectory);
        _ = Directory.CreateDirectory(textureDirectory);
        File.WriteAllText(Path.Combine(sourceDirectory, "sample.fbx"), string.Empty);
        File.WriteAllText(Path.Combine(textureDirectory, "sample_albedo.png"), string.Empty);
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
              "materials": [
                {
                  "name": "sample_material",
                  "shader": "standard",
                  "textures": {
                    "albedo": "textures/sample_albedo.png"
                  }
                }
              ],
              "states": [
                {
                  "name": "idle",
                  "source": "sample_mesh",
                  "materials": [
                    {
                      "slot": "body",
                      "material": "sample_material"
                    }
                  ]
                }
              ],
              "prototypes": [
                {
                  "name": "sample_render",
                  "kind": "render",
                  "defaultState": "idle",
                  "states": [
                    "idle"
                  ],
                  "materials": [
                    "sample_material"
                  ]
                }
              ]
            }
            """);

        try
        {
            ResourceManager resourceManager = new(root);
            RdmCatalog catalog = new();
            catalog.LoadResources(resourceManager);

            RdmPackage package = catalog.Index("Models/sample/sample.rdm");
            Assert.Equal("Models/sample/sample.rdm", package.ResourcePath);
            Assert.Equal("sample_mesh", Assert.Single(package.Definition.Sources).Id);
            Assert.Equal("sample_render", Assert.Single(package.Definition.Prototypes).Name);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadResources_indexes_repository_manny_reference_package()
    {
        ResourceManager resourceManager = new(GetRepositoryResourcesRoot());
        RdmCatalog catalog = new();

        catalog.LoadResources(resourceManager);

        RdmPackage package = catalog.Index("Models/MannyRef/manny.rdm");
        Assert.Contains(package.Definition.Sources, source => source.Format == "fbx");
        Assert.Contains(package.Definition.Prototypes, prototype => prototype.Name == "humanoid_attachments");
        Assert.Contains(package.Definition.States, state => state.Name == "reference");
    }

    private static string GetRepositoryResourcesRoot([CallerFilePath] string callerFilePath = "")
    {
        string callerDirectory = Path.GetDirectoryName(callerFilePath)
            ?? throw new InvalidOperationException("Could not resolve test file directory.");

        return Path.GetFullPath(Path.Combine(callerDirectory, "..", "..", "..", "Resources"));
    }
}
