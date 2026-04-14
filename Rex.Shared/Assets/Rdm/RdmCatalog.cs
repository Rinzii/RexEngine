using Rex.Shared.Resources;

namespace Rex.Shared.Assets.Rdm;

/// <summary>
/// Loads and indexes authored RDM metadata packages from shared model resources.
/// </summary>
public sealed class RdmCatalog
{
    private readonly Dictionary<string, RdmPackage> _packages = new(StringComparer.Ordinal);

    /// <summary>
    /// Loads every RDM package from the shared resources model directory.
    /// </summary>
    /// <param name="resourceManager">Resource manager rooted at the engine resources folder.</param>
    public void LoadResources(ResourceManager resourceManager)
    {
        ArgumentNullException.ThrowIfNull(resourceManager);
        LoadDirectory(resourceManager.ModelDirectory, resourceManager.RootDirectory);
    }

    /// <summary>
    /// Loads every RDM package under one directory.
    /// </summary>
    /// <param name="directory">Directory containing RDM metadata files.</param>
    public void LoadDirectory(string directory)
    {
        LoadDirectory(directory, directory);
    }

    /// <summary>
    /// Loads every RDM package under one directory using one explicit indexing root.
    /// </summary>
    /// <param name="directory">Directory containing RDM metadata files.</param>
    /// <param name="indexRootDirectory">Directory used to compute stable resource paths.</param>
    public void LoadDirectory(string directory, string indexRootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(indexRootDirectory);

        string fullDirectory = Path.GetFullPath(directory);
        string fullIndexRoot = Path.GetFullPath(indexRootDirectory);
        if (!Directory.Exists(fullDirectory))
        {
            return;
        }

        foreach (string path in EnumerateMetadataFiles(fullDirectory))
        {
            LoadFileInternal(path, fullIndexRoot);
        }
    }

    /// <summary>
    /// Loads one RDM metadata file.
    /// </summary>
    /// <param name="path">Absolute or relative file path.</param>
    public void LoadFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException($"Could not resolve the directory for '{path}'.");
        LoadFileInternal(fullPath, directory);
    }

    /// <summary>
    /// Resolves one indexed RDM package by resource path.
    /// </summary>
    /// <param name="resourcePath">Stable resource path for the RDM metadata file.</param>
    /// <returns>Resolved package.</returns>
    public RdmPackage Index(string resourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourcePath);
        string normalizedPath = NormalizePath(resourcePath);

        if (!_packages.TryGetValue(normalizedPath, out RdmPackage? package))
        {
            throw new InvalidOperationException($"RDM package '{resourcePath}' has not been loaded.");
        }

        return package;
    }

    /// <summary>
    /// Attempts to resolve one indexed RDM package by resource path.
    /// </summary>
    /// <param name="resourcePath">Stable resource path for the RDM metadata file.</param>
    /// <param name="package">Resolved package when present.</param>
    /// <returns><see langword="true"/> when the package exists.</returns>
    public bool TryIndex(string resourcePath, out RdmPackage package)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourcePath);
        return _packages.TryGetValue(NormalizePath(resourcePath), out package!);
    }

    /// <summary>
    /// Enumerates every loaded RDM package.
    /// </summary>
    /// <returns>Loaded packages.</returns>
    public IEnumerable<RdmPackage> EnumeratePackages()
    {
        return _packages.Values.OrderBy(static package => package.ResourcePath, StringComparer.Ordinal);
    }

    private void LoadFileInternal(string path, string indexRootDirectory)
    {
        ValidateMetadataFilePath(path);

        string fullPath = Path.GetFullPath(path);
        string fullIndexRoot = Path.GetFullPath(indexRootDirectory);
        string packageDirectory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException($"Could not resolve package directory for '{path}'.");

        RdmDefinition definition = RdmSerializer.DeserializeFile(fullPath);
        ValidatePackageFilesExist(definition, packageDirectory, fullPath);

        string resourcePath = NormalizePath(Path.GetRelativePath(fullIndexRoot, fullPath));
        if (resourcePath.StartsWith("../", StringComparison.Ordinal) || resourcePath == "..")
        {
            throw new InvalidOperationException(
                $"RDM package '{path}' is not rooted under index directory '{indexRootDirectory}'.");
        }

        if (_packages.ContainsKey(resourcePath))
        {
            throw new InvalidOperationException($"RDM package '{resourcePath}' is defined more than once.");
        }

        _packages.Add(resourcePath, new RdmPackage(resourcePath, fullPath, packageDirectory, definition));
    }

    private static IEnumerable<string> EnumerateMetadataFiles(string directory)
    {
        return Directory.EnumerateFiles(directory, "*.rdm", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(directory, "*.rdmb", SearchOption.AllDirectories))
            .OrderBy(static path => path, StringComparer.Ordinal);
    }

    private static void ValidatePackageFilesExist(RdmDefinition definition, string packageDirectory, string sourcePath)
    {
        foreach (RdmSourceDefinition source in definition.Sources)
        {
            ValidateRelativePackageFile(packageDirectory, source.Path, sourcePath);
        }

        foreach (RdmMaterialDefinition material in definition.Materials)
        {
            foreach (string texturePath in material.Textures.Values)
            {
                ValidateRelativePackageFile(packageDirectory, texturePath, sourcePath);
            }
        }
    }

    private static void ValidateRelativePackageFile(string packageDirectory, string relativePath, string sourcePath)
    {
        string candidatePath = Path.Combine(packageDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(candidatePath))
        {
            throw new InvalidOperationException(
                $"RDM package '{sourcePath}' references missing file '{relativePath}'.");
        }
    }

    private static void ValidateMetadataFilePath(string path)
    {
        if (!path.EndsWith(".rdm", StringComparison.OrdinalIgnoreCase)
            && !path.EndsWith(".rdmb", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"RDM metadata file '{path}' must end with .rdm or .rdmb.");
        }
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }
}

/// <summary>
/// One authored RDM package loaded from shared resources.
/// </summary>
public sealed class RdmPackage
{
    /// <summary>
    /// Creates one loaded RDM package.
    /// </summary>
    /// <param name="resourcePath">Stable resource path for the metadata file.</param>
    /// <param name="absolutePath">Absolute path to the metadata file.</param>
    /// <param name="packageDirectory">Absolute path to the package directory.</param>
    /// <param name="definition">Deserialized package definition.</param>
    public RdmPackage(string resourcePath, string absolutePath, string packageDirectory, RdmDefinition definition)
    {
        ResourcePath = resourcePath ?? throw new ArgumentNullException(nameof(resourcePath));
        AbsolutePath = absolutePath ?? throw new ArgumentNullException(nameof(absolutePath));
        PackageDirectory = packageDirectory ?? throw new ArgumentNullException(nameof(packageDirectory));
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    /// <summary>Gets the stable resource path for the metadata file.</summary>
    public string ResourcePath { get; }

    /// <summary>Gets the absolute path to the metadata file.</summary>
    public string AbsolutePath { get; }

    /// <summary>Gets the absolute path to the package directory.</summary>
    public string PackageDirectory { get; }

    /// <summary>Gets the authored RDM metadata definition.</summary>
    public RdmDefinition Definition { get; }
}
