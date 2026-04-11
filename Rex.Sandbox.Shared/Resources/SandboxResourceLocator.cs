using Rex.Shared.Resources;

namespace Rex.Sandbox.Shared.Resources;

/// <summary>
/// Locates the shared global resources root used by the sandbox sample.
/// </summary>
public static class SandboxResourceLocator
{
    /// <summary>
    /// Attempts to create a shared resource manager rooted at the global resources directory.
    /// </summary>
    /// <returns>A resource manager when a candidate resources root resolves. <see langword="null"/> when none of the candidates succeed.</returns>
    public static ResourceManager? TryCreateDefaultResourceManager()
    {
        foreach (string baseDirectory in GetCandidateBaseDirectories())
        {
            string? resourceRoot = TryFindResourceRoot(baseDirectory);
            if (resourceRoot != null)
            {
                return new ResourceManager(resourceRoot);
            }
        }

        return null;
    }

    private static IEnumerable<string> GetCandidateBaseDirectories()
    {
        yield return AppContext.BaseDirectory;
        yield return Directory.GetCurrentDirectory();
    }

    private static string? TryFindResourceRoot(string startDirectory)
    {
        string currentDirectory = Path.GetFullPath(startDirectory);
        while (true)
        {
            string directResourceRoot = Path.Combine(currentDirectory, "Resources");
            if (IsResourceRoot(directResourceRoot))
            {
                return directResourceRoot;
            }

            string nestedResourceRoot = Path.Combine(currentDirectory, "RexEngine", "Resources");
            if (IsResourceRoot(nestedResourceRoot))
            {
                return nestedResourceRoot;
            }

            DirectoryInfo? parent = Directory.GetParent(currentDirectory);
            if (parent == null)
            {
                return null;
            }

            currentDirectory = parent.FullName;
        }
    }

    private static bool IsResourceRoot(string candidate)
    {
        return Directory.Exists(candidate)
               && File.Exists(Path.Combine(candidate, "README.md"))
               && Directory.Exists(Path.Combine(candidate, SharedResourceDirectories.Prototypes))
               && Directory.Exists(Path.Combine(candidate, SharedResourceDirectories.Models));
    }
}
