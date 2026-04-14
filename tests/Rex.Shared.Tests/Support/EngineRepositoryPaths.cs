using System.Reflection;
using System.Runtime.CompilerServices;

namespace Rex.Shared.Tests.Support;

/// <summary>
/// Resolves the on-disk Rex engine root for tests that read <c>Rex.sln</c> or the <c>Resources</c> tree.
/// </summary>
/// <remarks>
/// Deterministic CI builds can supply <see cref="CallerFilePathAttribute"/> values such as
/// <c>/_/tests/Rex.Shared.Tests/...</c>, which break naive <c>..</c> walks. Prefer the test assembly
/// location or <c>GITHUB_WORKSPACE</c> when those point at a tree that contains <c>Rex.sln</c>.
/// </remarks>
internal static class EngineRepositoryPaths
{
    public static string GetEngineRoot([CallerFilePath] string callerFilePath = "")
    {
        string? fromWorkspace = TryGetFromGitHubWorkspace();
        if (fromWorkspace is not null)
        {
            return fromWorkspace;
        }

        string? assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string? fromAssembly = TryFindDirectoryUpwardsContainingFile(assemblyDir, "Rex.sln");
        if (fromAssembly is not null)
        {
            return fromAssembly;
        }

        string? fromCaller = TryFindRootFromCallerFile(callerFilePath);
        if (fromCaller is not null)
        {
            return fromCaller;
        }

        throw new InvalidOperationException(
            "Could not locate Rex engine root (expected Rex.sln). Run tests from a checkout or set GITHUB_WORKSPACE.");
    }

    public static string GetResourcesRoot([CallerFilePath] string callerFilePath = "") =>
        Path.Combine(GetEngineRoot(callerFilePath), "Resources");

    private static string? TryGetFromGitHubWorkspace()
    {
        string? workspace = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE");
        if (string.IsNullOrWhiteSpace(workspace))
        {
            return null;
        }

        string trimmed = workspace.Trim();
        return File.Exists(Path.Combine(trimmed, "Rex.sln")) ? trimmed : null;
    }

    private static string? TryFindDirectoryUpwardsContainingFile(string? startDirectory, string fileName)
    {
        if (string.IsNullOrEmpty(startDirectory))
        {
            return null;
        }

        for (DirectoryInfo? dir = new(startDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, fileName)))
            {
                return dir.FullName;
            }
        }

        return null;
    }

    private static string? TryFindRootFromCallerFile(string callerFilePath)
    {
        if (string.IsNullOrEmpty(callerFilePath) || IsSyntheticSourcePath(callerFilePath))
        {
            return null;
        }

        string? callerDirectory = Path.GetDirectoryName(callerFilePath);
        if (string.IsNullOrEmpty(callerDirectory))
        {
            return null;
        }

        string candidate = Path.GetFullPath(Path.Combine(callerDirectory, "..", "..", ".."));
        return File.Exists(Path.Combine(candidate, "Rex.sln")) ? candidate : null;
    }

    private static bool IsSyntheticSourcePath(string callerFilePath) =>
        callerFilePath.StartsWith("/_/", StringComparison.Ordinal);
}
