using System.Runtime.CompilerServices;
using Rex.Shared.Prototypes;
using Rex.Shared.Resources;
using Rex.Shared.Serialization.Manager;

namespace Rex.Shared.Tests.Architecture;

public sealed class EngineBoundaryTests
{
    [Fact]
    public void Core_projects_do_not_reference_sandbox_projects()
    {
        string engineRoot = GetEngineRoot();
        string[] projectPaths = Directory.EnumerateFiles(engineRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(static path => !path.Contains("Rex.Sandbox.", StringComparison.Ordinal))
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}build{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();

        foreach (string projectPath in projectPaths)
        {
            string projectText = File.ReadAllText(projectPath);
            Assert.DoesNotContain("Rex.Sandbox.", projectText, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Shared_resource_load_excludes_sandbox_testing_sample_prototypes()
    {
        ResourceManager resourceManager = new(GetRepositoryResourcesRoot());
        SerializationManager serializationManager = new();
        PrototypeManager prototypeManager = new(serializationManager);
        SharedPrototypeBootstrap.RegisterAll(prototypeManager);

        prototypeManager.LoadResources(resourceManager);

        string sandboxPrototypePath = Path.Combine(
            resourceManager.TestingSampleDirectory,
            "Sandbox",
            SharedResourceDirectories.Prototypes,
            "entities.prototype.json");

        Assert.True(File.Exists(sandboxPrototypePath));
        Assert.False(File.Exists(Path.Combine(resourceManager.PrototypeDirectory, "sandbox", "entities.prototype.json")));
        Assert.False(prototypeManager.TryIndex<EntityPrototype>("sandboxBaseActor", out _));
    }

    private static string GetEngineRoot([CallerFilePath] string callerFilePath = "")
    {
        string callerDirectory = Path.GetDirectoryName(callerFilePath)
            ?? throw new InvalidOperationException("Could not resolve test file directory.");

        return Path.GetFullPath(Path.Combine(callerDirectory, "..", "..", ".."));
    }

    private static string GetRepositoryResourcesRoot([CallerFilePath] string callerFilePath = "")
    {
        string callerDirectory = Path.GetDirectoryName(callerFilePath)
            ?? throw new InvalidOperationException("Could not resolve test file directory.");

        return Path.GetFullPath(Path.Combine(callerDirectory, "..", "..", "..", "Resources"));
    }
}
