namespace Rex.Shared.Resources;

/// <summary>
/// Resolves shared global resource directories and files rooted under the engine-wide resources folder.
/// </summary>
public sealed class ResourceManager
{
    /// <summary>
    /// Creates a resource manager rooted at one resources directory.
    /// </summary>
    /// <param name="rootDirectory">Root resources directory.</param>
    public ResourceManager(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = Path.GetFullPath(rootDirectory);
    }

    /// <summary>Gets the absolute root resources directory.</summary>
    public string RootDirectory { get; }

    /// <summary>Gets the shared prototype directory path.</summary>
    public string PrototypeDirectory => GetPath(SharedResourceDirectories.Prototypes);

    /// <summary>Gets the shared model directory path.</summary>
    public string ModelDirectory => GetPath(SharedResourceDirectories.Models);

    /// <summary>Gets the shared material directory path.</summary>
    public string MaterialDirectory => GetPath(SharedResourceDirectories.Materials);

    /// <summary>Gets the shared texture directory path.</summary>
    public string TextureDirectory => GetPath(SharedResourceDirectories.Textures);

    /// <summary>Gets the shared shader directory path.</summary>
    public string ShaderDirectory => GetPath(SharedResourceDirectories.Shaders);

    /// <summary>Gets the shared audio directory path.</summary>
    public string AudioDirectory => GetPath(SharedResourceDirectories.Audio);

    /// <summary>Gets the shared font directory path.</summary>
    public string FontDirectory => GetPath(SharedResourceDirectories.Fonts);

    /// <summary>Gets the shared localization directory path.</summary>
    public string LocalizationDirectory => GetPath(SharedResourceDirectories.Localization);

    /// <summary>Gets the shared map directory path.</summary>
    public string MapDirectory => GetPath(SharedResourceDirectories.Maps);

    /// <summary>Gets the shared scene directory path.</summary>
    public string SceneDirectory => GetPath(SharedResourceDirectories.Scenes);

    /// <summary>Gets the shared UI directory path.</summary>
    public string UiDirectory => GetPath(SharedResourceDirectories.Ui);

    /// <summary>Gets the shared VFX directory path.</summary>
    public string VfxDirectory => GetPath(SharedResourceDirectories.Vfx);

    /// <summary>Gets the shared testing samples directory path.</summary>
    public string TestingSampleDirectory => GetPath(SharedResourceDirectories.TestingSamples);

    /// <summary>
    /// Resolves one relative path under the resources root.
    /// </summary>
    /// <param name="relativePath">Relative path rooted under the resources directory.</param>
    /// <returns>Absolute resource path.</returns>
    public string GetPath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        string absolutePath = Path.GetFullPath(Path.Combine(RootDirectory, relativePath));
        string relativeToRoot = Path.GetRelativePath(RootDirectory, absolutePath);
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (relativeToRoot.Equals("..", comparison)
            || relativeToRoot.StartsWith($"..{Path.DirectorySeparatorChar}", comparison)
            || Path.IsPathRooted(relativeToRoot))
        {
            throw new InvalidOperationException(
                $"Resource path '{relativePath}' resolves outside the shared resources root '{RootDirectory}'.");
        }

        return absolutePath;
    }

    /// <summary>
    /// Enumerates files under a relative resource directory.
    /// </summary>
    /// <param name="relativeDirectory">Relative directory rooted under the resources folder.</param>
    /// <param name="searchPattern">Search pattern to match.</param>
    /// <param name="searchOption">Search depth to use.</param>
    /// <returns>Absolute matching file paths.</returns>
    public IEnumerable<string> EnumerateFiles(string relativeDirectory, string searchPattern, SearchOption searchOption = SearchOption.AllDirectories)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativeDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(searchPattern);

        string directory = GetPath(relativeDirectory);
        if (!Directory.Exists(directory))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateFiles(directory, searchPattern, searchOption);
    }
}

/// <summary>
/// Shared top-level directory names under the engine-wide global resources root.
/// </summary>
public static class SharedResourceDirectories
{
    /// <summary>Folder containing prototype definitions.</summary>
    public const string Prototypes = "Prototypes";

    /// <summary>Folder containing model resources.</summary>
    public const string Models = "Models";

    /// <summary>Folder containing shared material definitions and authored material payloads.</summary>
    public const string Materials = "Materials";

    /// <summary>Folder containing shared texture resources.</summary>
    public const string Textures = "Textures";

    /// <summary>Folder containing shared shader sources and compiled shader payloads.</summary>
    public const string Shaders = "Shaders";

    /// <summary>Folder containing shared audio resources.</summary>
    public const string Audio = "Audio";

    /// <summary>Folder containing shared font resources.</summary>
    public const string Fonts = "Fonts";

    /// <summary>Folder containing shared localization resources.</summary>
    public const string Localization = "Localization";

    /// <summary>Folder containing authored map or scene resources.</summary>
    public const string Maps = "Maps";

    /// <summary>Folder containing authored scene resources.</summary>
    public const string Scenes = "Scenes";

    /// <summary>Folder containing shared UI assets and layouts.</summary>
    public const string Ui = "UI";

    /// <summary>Folder containing visual-effects assets.</summary>
    public const string Vfx = "Vfx";

    /// <summary>Folder containing opt-in testing and reference samples.</summary>
    public const string TestingSamples = "TestingSamples";
}
