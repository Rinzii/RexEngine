using Rex.Shared.Resources;

namespace Rex.Shared.Tests.Resources;

public sealed class ResourceManagerTests
{
    [Fact]
    public void BuiltInDirectories_resolve_under_resources_root()
    {
        string root = Path.Combine(Path.GetTempPath(), $"rex-resources-{Guid.NewGuid():N}");

        try
        {
            ResourceManager resourceManager = new(root);
            (string RelativePath, string ActualPath)[] directories =
            [
                (SharedResourceDirectories.Prototypes, resourceManager.PrototypeDirectory),
                (SharedResourceDirectories.Models, resourceManager.ModelDirectory),
                (SharedResourceDirectories.Materials, resourceManager.MaterialDirectory),
                (SharedResourceDirectories.Textures, resourceManager.TextureDirectory),
                (SharedResourceDirectories.Shaders, resourceManager.ShaderDirectory),
                (SharedResourceDirectories.Audio, resourceManager.AudioDirectory),
                (SharedResourceDirectories.Fonts, resourceManager.FontDirectory),
                (SharedResourceDirectories.Localization, resourceManager.LocalizationDirectory),
                (SharedResourceDirectories.Maps, resourceManager.MapDirectory),
                (SharedResourceDirectories.Scenes, resourceManager.SceneDirectory),
                (SharedResourceDirectories.Ui, resourceManager.UiDirectory),
                (SharedResourceDirectories.Vfx, resourceManager.VfxDirectory),
                (SharedResourceDirectories.TestingSamples, resourceManager.TestingSampleDirectory)
            ];

            foreach ((string relativePath, string actualPath) in directories)
            {
                Assert.Equal(
                    Path.GetFullPath(Path.Combine(root, relativePath)),
                    actualPath);
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void EnumerateFiles_reads_testing_sample_directory()
    {
        string root = Path.Combine(Path.GetTempPath(), $"rex-resources-{Guid.NewGuid():N}");
        string samplesDirectory = Path.Combine(root, SharedResourceDirectories.TestingSamples);
        _ = Directory.CreateDirectory(samplesDirectory);
        File.WriteAllText(Path.Combine(samplesDirectory, "sample.txt"), "sample");

        try
        {
            ResourceManager resourceManager = new(root);

            string[] files = resourceManager
                .EnumerateFiles(SharedResourceDirectories.TestingSamples, "*.txt", SearchOption.TopDirectoryOnly)
                .ToArray();

            string file = Assert.Single(files);
            Assert.EndsWith("sample.txt", file, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void GetPath_rejects_paths_outside_resources_root()
    {
        string root = Path.Combine(Path.GetTempPath(), $"rex-resources-{Guid.NewGuid():N}");

        try
        {
            ResourceManager resourceManager = new(root);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => resourceManager.GetPath(Path.Combine("..", "outside.txt")));

            Assert.Contains("outside the shared resources root", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
