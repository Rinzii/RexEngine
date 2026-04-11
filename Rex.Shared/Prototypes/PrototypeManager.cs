using System.Reflection;
using Rex.Shared.Resources;
using Rex.Shared.Serialization.Manager;

namespace Rex.Shared.Prototypes;

/// <summary>
/// Loads and indexes prototype definitions from shared resource files.
/// </summary>
public sealed class PrototypeManager
{
    private readonly ISerializationManager _serializationManager;
    private readonly Dictionary<string, PrototypeRegistration> _registrations = new(StringComparer.Ordinal);
    private readonly Dictionary<PrototypeKey, RawPrototypeDefinition> _rawDefinitions = [];
    private readonly Dictionary<PrototypeKey, IPrototype> _loadedPrototypes = [];
    private readonly HashSet<PrototypeKey> _resolving = [];

    /// <summary>
    /// Creates a prototype manager using one serialization manager.
    /// </summary>
    /// <param name="serializationManager">Serializer used for prototype hydration and composition.</param>
    public PrototypeManager(ISerializationManager serializationManager)
    {
        _serializationManager = serializationManager;
    }

    /// <summary>Raised after the prototype set is loaded or reloaded.</summary>
    public event EventHandler<PrototypeReloadedEventArgs>? Reloaded;

    /// <summary>Gets the current monotonic reload version.</summary>
    public int ReloadVersion { get; private set; }

    /// <summary>
    /// Registers every prototype type found in an assembly.
    /// </summary>
    /// <param name="assembly">Assembly to scan.</param>
    public void RegisterAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        foreach (Type type in assembly.GetTypes())
        {
            if (type.IsAbstract || !typeof(IPrototype).IsAssignableFrom(type))
            {
                continue;
            }

            if (!type.IsDefined(typeof(PrototypeAttribute), inherit: false))
            {
                continue;
            }

            RegisterPrototype(type);
        }
    }

    /// <summary>
    /// Registers one prototype type.
    /// </summary>
    /// <typeparam name="T">Prototype type to register.</typeparam>
    public void RegisterPrototype<T>()
        where T : class, IPrototype
    {
        RegisterPrototype(typeof(T));
    }

    /// <summary>
    /// Registers one prototype type.
    /// </summary>
    /// <param name="prototypeType">Prototype CLR type.</param>
    public void RegisterPrototype(Type prototypeType)
    {
        ArgumentNullException.ThrowIfNull(prototypeType);

        if (!typeof(IPrototype).IsAssignableFrom(prototypeType))
        {
            throw new InvalidOperationException(
                $"Prototype type '{prototypeType.FullName}' must implement '{typeof(IPrototype).FullName}'.");
        }

        PrototypeAttribute attribute = prototypeType.GetCustomAttribute<PrototypeAttribute>(inherit: false)
            ?? throw new InvalidOperationException(
                $"Prototype type '{prototypeType.FullName}' must be annotated with '{typeof(PrototypeAttribute).FullName}'.");

        string typeName = string.IsNullOrWhiteSpace(attribute.Type)
            ? PrototypeUtility.CalculatePrototypeName(prototypeType.Name)
            : attribute.Type;

        if (_registrations.TryGetValue(typeName, out PrototypeRegistration? existing)
            && existing.PrototypeType != prototypeType)
        {
            throw new InvalidOperationException(
                $"Prototype type name '{typeName}' is already registered for '{existing.PrototypeType.FullName}'.");
        }

        _registrations[typeName] = new PrototypeRegistration(typeName, prototypeType);
    }

    /// <summary>
    /// Enumerates the registered prototype kind names.
    /// </summary>
    public IEnumerable<string> GetPrototypeKinds()
    {
        return _registrations.Keys.OrderBy(static key => key, StringComparer.Ordinal);
    }

    /// <summary>
    /// Attempts to resolve one registered prototype kind name into its CLR type.
    /// </summary>
    public bool TryGetKindType(string typeName, out Type? prototypeType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);

        if (_registrations.TryGetValue(typeName, out PrototypeRegistration? registration))
        {
            prototypeType = registration.PrototypeType;
            return true;
        }

        prototypeType = null;
        return false;
    }

    /// <summary>
    /// Loads prototype files from one absolute directory.
    /// </summary>
    /// <param name="directory">Directory containing prototype JSON files.</param>
    public void LoadDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        LoadDirectoryCore(directory);
        PublishReload([directory]);
    }

    /// <summary>
    /// Loads prototypes from the shared resources prototype directory.
    /// </summary>
    /// <param name="resourceManager">Resource manager rooted at the engine resources folder.</param>
    public void LoadResources(ResourceManager resourceManager)
    {
        ArgumentNullException.ThrowIfNull(resourceManager);
        LoadDirectoryCore(resourceManager.PrototypeDirectory);
        ValidateResourceBackedPrototypes(resourceManager);
        PublishReload([resourceManager.PrototypeDirectory]);
    }

    /// <summary>
    /// Clears the current prototype set and loads a directory again.
    /// </summary>
    /// <param name="directory">Prototype directory to reload.</param>
    public void ReloadDirectory(string directory)
    {
        ClearLoadedPrototypes();
        LoadDirectory(directory);
    }

    /// <summary>
    /// Clears the current prototype set and reloads the shared resource directory.
    /// </summary>
    /// <param name="resourceManager">Resource manager rooted at the global resources folder.</param>
    public void ReloadResources(ResourceManager resourceManager)
    {
        ArgumentNullException.ThrowIfNull(resourceManager);
        ClearLoadedPrototypes();
        LoadResources(resourceManager);
    }

    /// <summary>
    /// Loads one prototype JSON file.
    /// </summary>
    /// <param name="path">Prototype file path.</param>
    public void LoadFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        DataNode rootNode = path.EndsWith(".prototype.bjson", StringComparison.OrdinalIgnoreCase)
            ? DataNodeBinaryJsonSerializer.Read(File.ReadAllBytes(path))
            : DataNodeJsonSerializer.ReadFile(path);
        if (rootNode is not SequenceDataNode sequenceNode)
        {
            throw new InvalidOperationException(
                $"Prototype file '{path}' must contain a top-level sequence.");
        }

        foreach (DataNode child in sequenceNode.Sequence)
        {
            if (child is not MappingDataNode mappingNode)
            {
                throw new InvalidOperationException(
                    $"Prototype file '{path}' contains a non-mapping prototype entry.");
            }

            AddRawPrototype(path, mappingNode);
        }
    }

    /// <summary>
    /// Gets one prototype by id.
    /// </summary>
    /// <typeparam name="T">Prototype CLR type.</typeparam>
    /// <param name="id">Prototype id.</param>
    /// <returns>Resolved prototype instance.</returns>
    public T Index<T>(string id)
        where T : class, IPrototype
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        PrototypeKey key = new(typeof(T), id);
        if (!_loadedPrototypes.TryGetValue(key, out IPrototype? prototype))
        {
            prototype = ResolvePrototype(key);
        }

        return (T)prototype;
    }

    /// <summary>Gets one entity prototype by typed id.</summary>
    public EntityPrototype Index(EntityPrototypeId id) => Index<EntityPrototype>(id.Value);

    /// <summary>Gets one model prototype by typed id.</summary>
    public ModelPrototype Index(ModelPrototypeId id) => Index<ModelPrototype>(id.Value);

    /// <summary>Gets one map prototype by typed id.</summary>
    public MapPrototype Index(MapPrototypeId id) => Index<MapPrototype>(id.Value);

    /// <summary>Gets one scene prototype by typed id.</summary>
    public ScenePrototype Index(ScenePrototypeId id) => Index<ScenePrototype>(id.Value);

    /// <summary>Gets one prototype category by typed id.</summary>
    public PrototypeCategoryPrototype Index(PrototypeCategoryId id) => Index<PrototypeCategoryPrototype>(id.Value);

    /// <summary>
    /// Gets one prototype by kind and id.
    /// </summary>
    public IPrototype Index(Type prototypeType, string id)
    {
        ArgumentNullException.ThrowIfNull(prototypeType);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        PrototypeKey key = new(prototypeType, id);
        if (!_loadedPrototypes.TryGetValue(key, out IPrototype? prototype))
        {
            prototype = ResolvePrototype(key);
        }

        return prototype;
    }

    /// <summary>
    /// Checks whether one prototype id exists for one CLR prototype kind.
    /// </summary>
    public bool HasIndex<T>(string id)
        where T : class, IPrototype
    {
        return TryIndex<T>(id, out _);
    }

    /// <summary>
    /// Attempts to get one prototype by id.
    /// </summary>
    /// <typeparam name="T">Prototype CLR type.</typeparam>
    /// <param name="id">Prototype id.</param>
    /// <param name="prototype">Resolved prototype instance when present.</param>
    /// <returns><see langword="true"/> when the prototype exists.</returns>
    public bool TryIndex<T>(string id, out T prototype)
        where T : class, IPrototype
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        PrototypeKey key = new(typeof(T), id);
        if (_loadedPrototypes.TryGetValue(key, out IPrototype? loaded))
        {
            prototype = (T)loaded;
            return true;
        }

        if (!_rawDefinitions.ContainsKey(key))
        {
            prototype = null!;
            return false;
        }

        prototype = (T)ResolvePrototype(key);
        return true;
    }

    /// <summary>Attempts to get one entity prototype by typed id.</summary>
    public bool TryIndex(EntityPrototypeId id, out EntityPrototype prototype) => TryIndex(id.Value, out prototype);

    /// <summary>Attempts to get one model prototype by typed id.</summary>
    public bool TryIndex(ModelPrototypeId id, out ModelPrototype prototype) => TryIndex(id.Value, out prototype);

    /// <summary>Attempts to get one map prototype by typed id.</summary>
    public bool TryIndex(MapPrototypeId id, out MapPrototype prototype) => TryIndex(id.Value, out prototype);

    /// <summary>Attempts to get one scene prototype by typed id.</summary>
    public bool TryIndex(ScenePrototypeId id, out ScenePrototype prototype) => TryIndex(id.Value, out prototype);

    /// <summary>Attempts to get one prototype category by typed id.</summary>
    public bool TryIndex(PrototypeCategoryId id, out PrototypeCategoryPrototype prototype) => TryIndex(id.Value, out prototype);

    /// <summary>
    /// Attempts to get one prototype by kind and id.
    /// </summary>
    public bool TryIndex(Type prototypeType, string id, out IPrototype? prototype)
    {
        ArgumentNullException.ThrowIfNull(prototypeType);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        PrototypeKey key = new(prototypeType, id);
        if (_loadedPrototypes.TryGetValue(key, out IPrototype? loaded))
        {
            prototype = loaded;
            return true;
        }

        if (!_rawDefinitions.ContainsKey(key))
        {
            prototype = null;
            return false;
        }

        prototype = ResolvePrototype(key);
        return true;
    }

    /// <summary>
    /// Enumerates loaded prototypes of one type.
    /// </summary>
    /// <typeparam name="T">Prototype CLR type.</typeparam>
    /// <returns>Loaded prototypes of the requested type.</returns>
    public IEnumerable<T> EnumeratePrototypes<T>()
        where T : class, IPrototype
    {
        ResolveAll();
        return _loadedPrototypes.Values
            .OfType<T>()
            .Where(static prototype => !prototype.Abstract);
    }

    /// <summary>
    /// Enumerates loaded prototypes of one kind.
    /// </summary>
    public IEnumerable<IPrototype> EnumeratePrototypes(Type prototypeType)
    {
        ArgumentNullException.ThrowIfNull(prototypeType);

        ResolveAll();
        return _loadedPrototypes
            .Where(pair => pair.Key.PrototypeType == prototypeType && !pair.Value.Abstract)
            .Select(static pair => pair.Value);
    }

    /// <summary>
    /// Enumerates parent prototypes for one inheriting prototype.
    /// </summary>
    /// <typeparam name="T">Prototype CLR type.</typeparam>
    /// <param name="id">Prototype id to walk from.</param>
    /// <param name="includeSelf">Whether to yield the starting prototype first.</param>
    /// <returns>Prototype inheritance chain from the immediate parent upward.</returns>
    public IEnumerable<T> EnumerateParents<T>(string id, bool includeSelf = false)
        where T : class, IPrototype, IInheritingPrototype
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (!TryIndex(id, out T prototype))
        {
            yield break;
        }

        if (includeSelf)
        {
            yield return prototype!;
        }

        Queue<string> queue = new(GetParentIds(prototype!));
        HashSet<string> visited = new(StringComparer.Ordinal);
        while (queue.TryDequeue(out string? parentId))
        {
            if (string.IsNullOrWhiteSpace(parentId) || !visited.Add(parentId))
            {
                continue;
            }

            if (!TryIndex(parentId, out T parent))
            {
                continue;
            }

            yield return parent;

            foreach (string inheritedParent in GetParentIds(parent))
            {
                queue.Enqueue(inheritedParent);
            }
        }
    }

    private void AddRawPrototype(string sourcePath, MappingDataNode mappingNode)
    {
        string typeName = ReadRequiredScalar(mappingNode, "type", sourcePath);
        if (!_registrations.TryGetValue(typeName, out PrototypeRegistration? registration))
        {
            throw new InvalidOperationException(
                $"Prototype file '{sourcePath}' references unregistered prototype type '{typeName}'.");
        }

        string id = ReadRequiredScalar(mappingNode, "id", sourcePath);
        string[] parents = ReadParentIds(mappingNode, sourcePath);

        PrototypeKey key = new(registration.PrototypeType, id);
        if (_rawDefinitions.ContainsKey(key))
        {
            throw new InvalidOperationException(
                $"Prototype '{registration.TypeName}:{id}' is defined more than once.");
        }

        _rawDefinitions.Add(key, new RawPrototypeDefinition(registration, id, parents, (MappingDataNode)mappingNode.Clone(), sourcePath));
    }

    private void LoadDirectoryCore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(directory, "*.prototype.json", SearchOption.AllDirectories)
                     .Concat(Directory.EnumerateFiles(directory, "*.prototype.bjson", SearchOption.AllDirectories))
                     .OrderBy(static path => path, StringComparer.Ordinal))
        {
            LoadFile(file);
        }

        ResolveAll();
    }

    private void ClearLoadedPrototypes()
    {
        _rawDefinitions.Clear();
        _loadedPrototypes.Clear();
        _resolving.Clear();
    }

    private void ResolveAll()
    {
        foreach (PrototypeKey key in _rawDefinitions.Keys.ToArray())
        {
            _ = ResolvePrototype(key);
        }
    }

    private void ValidateResourceBackedPrototypes(ResourceManager resourceManager)
    {
        foreach (MapPrototype map in EnumeratePrototypes<MapPrototype>())
        {
            EnsureResourceExists(resourceManager, map.Map, $"map prototype '{map.Id}'");

            if (!string.IsNullOrWhiteSpace(map.Scene))
            {
                _ = Index<ScenePrototype>(map.Scene);
            }
        }

        foreach (ScenePrototype scene in EnumeratePrototypes<ScenePrototype>())
        {
            if (!string.IsNullOrWhiteSpace(scene.Map))
            {
                _ = Index<MapPrototype>(scene.Map);
            }

            foreach (SceneEntityPlacement entity in scene.Entities)
            {
                _ = Index<EntityPrototype>(entity.Prototype);
            }
        }

        foreach (ModelPrototype model in EnumeratePrototypes<ModelPrototype>())
        {
            EnsureResourceExists(resourceManager, model.Rdm, $"model prototype '{model.Id}'");
        }

        foreach (PrototypeCategoryPrototype category in EnumeratePrototypes<PrototypeCategoryPrototype>())
        {
            if (!_registrations.ContainsKey(category.PrototypeType))
            {
                throw new InvalidOperationException(
                    $"Prototype category '{category.Id}' references unknown prototype type '{category.PrototypeType}'.");
            }
        }
    }

    private static void EnsureResourceExists(ResourceManager resourceManager, string relativePath, string owner)
    {
        string absolutePath = resourceManager.GetPath(relativePath);
        if (!File.Exists(absolutePath))
        {
            throw new InvalidOperationException(
                $"The {owner} references missing resource '{relativePath}'.");
        }
    }

    private void PublishReload(IReadOnlyList<string> sources)
    {
        // Monotonic version lets listeners invalidate caches without diffing the full prototype graph.
        ReloadVersion++;
        Reloaded?.Invoke(this, new PrototypeReloadedEventArgs(ReloadVersion, sources));
    }

    private IPrototype ResolvePrototype(PrototypeKey key)
    {
        if (_loadedPrototypes.TryGetValue(key, out IPrototype? existing))
        {
            return existing;
        }

        if (!_rawDefinitions.TryGetValue(key, out RawPrototypeDefinition? rawDefinition))
        {
            throw new InvalidOperationException(
                $"Prototype '{key.PrototypeType.FullName}:{key.Id}' has not been loaded.");
        }

        // Depth-first resolve uses this set to catch cyclic parent chains before stack blowups.
        if (!_resolving.Add(key))
        {
            throw new InvalidOperationException(
                $"Prototype inheritance cycle detected while resolving '{rawDefinition.Registration.TypeName}:{rawDefinition.Id}'.");
        }

        try
        {
            List<DataNode> composeNodes = [];
            foreach (string parentId in rawDefinition.Parents)
            {
                PrototypeKey parentKey = new(key.PrototypeType, parentId);
                _ = ResolvePrototype(parentKey);
                composeNodes.Add(_serializationManager.WriteValue(parentKey.PrototypeType, _loadedPrototypes[parentKey], alwaysWrite: true));
            }

            composeNodes.Add(rawDefinition.Node.Clone());
            DataNode composedNode = composeNodes.Count == 1
                ? composeNodes[0]
                : _serializationManager.Compose(key.PrototypeType, composeNodes.ToArray());

            object? prototypeObject = _serializationManager.Read(key.PrototypeType, composedNode);
            if (prototypeObject is not IPrototype prototype)
            {
                throw new InvalidOperationException(
                    $"Prototype type '{key.PrototypeType.FullName}' did not deserialize into an IPrototype instance.");
            }

            ResetInheritedAbstractFlag(rawDefinition, prototypeObject);

            if (!string.Equals(prototype.Id, rawDefinition.Id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Prototype '{rawDefinition.Registration.TypeName}:{rawDefinition.Id}' deserialized with mismatched id '{prototype.Id}'.");
            }

            _loadedPrototypes.Add(key, prototype);
            return prototype;
        }
        finally
        {
            _ = _resolving.Remove(key);
        }
    }

    private static void ResetInheritedAbstractFlag(RawPrototypeDefinition rawDefinition, object prototypeObject)
    {
        if (rawDefinition.Parents.Length == 0 || rawDefinition.Node.TryGet("abstract", out _))
        {
            return;
        }

        PropertyInfo? property = prototypeObject.GetType().GetProperty(nameof(IPrototype.Abstract));
        if (property is { CanWrite: true, PropertyType: not null } && property.PropertyType == typeof(bool))
        {
            property.SetValue(prototypeObject, false);
        }
    }

    private static string ReadRequiredScalar(MappingDataNode mappingNode, string key, string sourcePath)
    {
        if (!mappingNode.TryGet(key, out DataNode node) || node is not ValueDataNode valueNode || string.IsNullOrWhiteSpace(valueNode.Value))
        {
            throw new InvalidOperationException(
                $"Prototype file '{sourcePath}' is missing required scalar field '{key}'.");
        }

        return valueNode.Value;
    }

    private static string[] ReadParentIds(MappingDataNode mappingNode, string sourcePath)
    {
        List<string> parents = [];
        if (mappingNode.TryGet("parent", out DataNode parentNode))
        {
            switch (parentNode)
            {
                case ValueDataNode valueNode when !string.IsNullOrWhiteSpace(valueNode.Value):
                    parents.Add(valueNode.Value);
                    break;
                case SequenceDataNode sequenceNode:
                    parents.AddRange(ReadParentSequence(sequenceNode, sourcePath, "parent"));
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Prototype file '{sourcePath}' field 'parent' must be a scalar or sequence.");
            }
        }

        if (mappingNode.TryGet("parents", out DataNode parentsNode))
        {
            if (parentsNode is not SequenceDataNode sequenceNode)
            {
                throw new InvalidOperationException(
                    $"Prototype file '{sourcePath}' field 'parents' must be a sequence.");
            }

            parents.AddRange(ReadParentSequence(sequenceNode, sourcePath, "parents"));
        }

        return parents.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static IEnumerable<string> ReadParentSequence(SequenceDataNode sequenceNode, string sourcePath, string fieldName)
    {
        foreach (DataNode child in sequenceNode.Sequence)
        {
            if (child is not ValueDataNode valueNode || string.IsNullOrWhiteSpace(valueNode.Value))
            {
                throw new InvalidOperationException(
                    $"Prototype file '{sourcePath}' field '{fieldName}' must contain only non-empty scalar ids.");
            }

            yield return valueNode.Value;
        }
    }

    private static IReadOnlyList<string> GetParentIds(IInheritingPrototype prototype)
    {
        return prototype.Parents ?? Array.Empty<string>();
    }

    private readonly record struct PrototypeKey(Type PrototypeType, string Id);

    private sealed record PrototypeRegistration(string TypeName, Type PrototypeType);

    private sealed record RawPrototypeDefinition(
        PrototypeRegistration Registration,
        string Id,
        string[] Parents,
        MappingDataNode Node,
        string SourcePath);
}
