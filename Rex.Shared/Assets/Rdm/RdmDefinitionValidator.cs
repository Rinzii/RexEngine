namespace Rex.Shared.Assets.Rdm;

/// <summary>
/// Validates Rex Data Model package metadata before the engine attempts to load 3D content.
/// </summary>
public static class RdmDefinitionValidator
{
    private static readonly string[] s_validSourceKinds =
    [
        "model",
        "animation",
        "collision",
        "skeleton",
        "material"
    ];

    private static readonly string[] s_validSourceFormats =
    [
        "fbx",
        "gltf",
        "glb",
        "obj",
        "blend",
        "usd",
        "usda",
        "usdc",
        "usdz"
    ];

    private static readonly string[] s_validCollisionKinds =
    [
        "triangleMesh",
        "convexHull",
        "primitive"
    ];

    private static readonly string[] s_validPrototypeKinds =
    [
        "render",
        "physics",
        "interaction",
        "animationSet",
        "materialSet",
        "attachmentSet"
    ];

    private static readonly string[] s_validMaterialDomains =
    [
        "opaque",
        "masked",
        "transparent",
        "additive"
    ];

    private static readonly Dictionary<string, string[]> s_expectedExtensions = new(StringComparer.Ordinal)
    {
        ["fbx"] = [".fbx"],
        ["gltf"] = [".gltf"],
        ["glb"] = [".glb"],
        ["obj"] = [".obj"],
        ["blend"] = [".blend"],
        ["usd"] = [".usd"],
        ["usda"] = [".usda"],
        ["usdc"] = [".usdc"],
        ["usdz"] = [".usdz"]
    };

    /// <summary>
    /// Throws when an RDM definition is missing required metadata or violates package invariants.
    /// </summary>
    /// <param name="definition">Definition to validate.</param>
    public static void Validate(RdmDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (definition.Version != RdmDefinition.CurrentVersion)
        {
            throw new InvalidOperationException(
                $"RDM version {definition.Version} is not supported. Expected version {RdmDefinition.CurrentVersion}.");
        }

        ValidateNonEmpty(definition.License, nameof(definition.License));
        ValidateNonEmpty(definition.Copyright, nameof(definition.Copyright));
        ValidateSize(definition.Size);
        ValidateSources(definition.Sources);
        ValidateMaterials(definition.Materials, definition.Sources);
        ValidateStates(definition.States, definition.Sources, definition.Materials);
        ValidatePrototypes(definition.Prototypes, definition.States, definition.Materials);
    }

    private static void ValidateSize(RdmSizeDefinition size)
    {
        ArgumentNullException.ThrowIfNull(size);
        ValidatePositive(size.X, nameof(size.X));
        ValidatePositive(size.Y, nameof(size.Y));
        ValidatePositive(size.Z, nameof(size.Z));
    }

    private static void ValidateSources(List<RdmSourceDefinition> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (sources.Count == 0)
        {
            throw new InvalidOperationException("RDM packages must define at least one source asset.");
        }

        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (RdmSourceDefinition source in sources)
        {
            ArgumentNullException.ThrowIfNull(source);
            ValidateStateName(source.Id, nameof(source.Id));
            if (!ids.Add(source.Id))
            {
                throw new InvalidOperationException($"Source '{source.Id}' is defined more than once.");
            }

            if (!s_validSourceKinds.Contains(source.Kind, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Source kind '{source.Kind}' is not supported. Expected one of: {string.Join(", ", s_validSourceKinds)}.");
            }

            if (!s_validSourceFormats.Contains(source.Format, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Source format '{source.Format}' is not supported. Expected one of: {string.Join(", ", s_validSourceFormats)}.");
            }

            ValidateRelativePath(source.Path, nameof(source.Path));
            string extension = Path.GetExtension(source.Path);
            if (!s_expectedExtensions[source.Format].Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Source '{source.Id}' format '{source.Format}' does not match file extension '{extension}'.");
            }

            if (source.Scale <= 0.0f || float.IsNaN(source.Scale) || float.IsInfinity(source.Scale))
            {
                throw new InvalidOperationException($"Source '{source.Id}' scale must be finite and greater than zero.");
            }

            ValidateAxis(source.UpAxis, nameof(source.UpAxis));
            ValidateAxis(source.ForwardAxis, nameof(source.ForwardAxis));
            ValidateMetadata(source.Metadata, nameof(source.Metadata));
        }
    }

    private static void ValidateStates(
        List<RdmStateDefinition> states,
        List<RdmSourceDefinition> sources,
        List<RdmMaterialDefinition> materials)
    {
        ArgumentNullException.ThrowIfNull(states);
        if (states.Count == 0)
        {
            throw new InvalidOperationException("RDM packages must define at least one state.");
        }

        HashSet<string> stateNames = new(StringComparer.Ordinal);
        var sourceIds = sources.Select(static source => source.Id).ToHashSet(StringComparer.Ordinal);
        var materialNames = materials.Select(static material => material.Name).ToHashSet(StringComparer.Ordinal);
        foreach (RdmStateDefinition state in states)
        {
            ArgumentNullException.ThrowIfNull(state);
            ValidateStateName(state.Name, nameof(state.Name));
            if (!stateNames.Add(state.Name))
            {
                throw new InvalidOperationException($"State '{state.Name}' is defined more than once.");
            }

            ValidateSourceReference(state.Source, sourceIds, nameof(state.Source));
            ValidateOptionalSourceReference(state.SkeletonSource, sourceIds, nameof(state.SkeletonSource));
            ValidateMetadata(state.Flags, nameof(state.Flags));
            ValidateLods(state.Lods, sourceIds);
            ValidateAnimations(state.Animations, sourceIds);
            ValidateAttachments(state.Attachments);
            ValidateMorphTargets(state.MorphTargets);
            ValidateStateMaterials(state.Materials, materialNames);
            ValidateCollision(state.Collision, sourceIds);
        }
    }

    private static void ValidateMaterials(List<RdmMaterialDefinition> materials, List<RdmSourceDefinition> sources)
    {
        ArgumentNullException.ThrowIfNull(materials);

        HashSet<string> names = new(StringComparer.Ordinal);
        var sourceIds = sources.Select(static source => source.Id).ToHashSet(StringComparer.Ordinal);
        foreach (RdmMaterialDefinition material in materials)
        {
            ArgumentNullException.ThrowIfNull(material);
            ValidateStateName(material.Name, nameof(material.Name));
            if (!names.Add(material.Name))
            {
                throw new InvalidOperationException($"Material '{material.Name}' is defined more than once.");
            }

            if (!string.IsNullOrWhiteSpace(material.Source))
            {
                ValidateSourceReference(material.Source, sourceIds, nameof(material.Source));
            }

            ValidateNonEmpty(material.Shader, nameof(material.Shader));
            if (!s_validMaterialDomains.Contains(material.Domain, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Material domain '{material.Domain}' is not supported. Expected one of: {string.Join(", ", s_validMaterialDomains)}.");
            }

            ValidateMetadata(material.Parameters, nameof(material.Parameters));
            ValidateTexturePaths(material.Textures);
        }
    }

    private static void ValidatePrototypes(
        List<RdmPrototypeDefinition> prototypes,
        List<RdmStateDefinition> states,
        List<RdmMaterialDefinition> materials)
    {
        ArgumentNullException.ThrowIfNull(prototypes);

        var stateNames = states.Select(static state => state.Name).ToHashSet(StringComparer.Ordinal);
        var materialNames = materials.Select(static material => material.Name).ToHashSet(StringComparer.Ordinal);
        var animationNames = states
            .SelectMany(static state => state.Animations)
            .Select(static animation => animation.Name)
            .ToHashSet(StringComparer.Ordinal);

        HashSet<string> prototypeNames = new(StringComparer.Ordinal);
        foreach (RdmPrototypeDefinition prototype in prototypes)
        {
            ArgumentNullException.ThrowIfNull(prototype);
            ValidateStateName(prototype.Name, nameof(prototype.Name));
            if (!prototypeNames.Add(prototype.Name))
            {
                throw new InvalidOperationException($"Prototype '{prototype.Name}' is defined more than once.");
            }

            if (!s_validPrototypeKinds.Contains(prototype.Kind, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Prototype kind '{prototype.Kind}' is not supported. Expected one of: {string.Join(", ", s_validPrototypeKinds)}.");
            }

            if (!string.IsNullOrWhiteSpace(prototype.DefaultState))
            {
                ValidateReference(prototype.DefaultState, stateNames, "state", nameof(prototype.DefaultState));
            }

            ValidateNamedReferences(prototype.States, stateNames, "state");
            ValidateNamedReferences(prototype.Animations, animationNames, "animation");
            ValidateNamedReferences(prototype.Materials, materialNames, "material");
            ValidateMetadata(prototype.Tags, nameof(prototype.Tags));
        }

        foreach (RdmPrototypeDefinition prototype in prototypes)
        {
            if (!string.IsNullOrWhiteSpace(prototype.Inherits))
            {
                ValidateReference(prototype.Inherits, prototypeNames, "prototype", nameof(prototype.Inherits));
            }
        }
    }

    private static void ValidateLods(List<RdmLodDefinition> lods, HashSet<string> sourceIds)
    {
        ArgumentNullException.ThrowIfNull(lods);

        float previousCoverage = float.PositiveInfinity;
        foreach (RdmLodDefinition lod in lods)
        {
            ArgumentNullException.ThrowIfNull(lod);
            ValidateSourceReference(lod.Source, sourceIds, nameof(lod.Source));
            if (lod.ScreenCoverage is <= 0.0f or > 1.0f)
            {
                throw new InvalidOperationException("LOD screen coverage must be greater than zero and less than or equal to one.");
            }

            if (lod.ScreenCoverage >= previousCoverage)
            {
                throw new InvalidOperationException("LOD screen coverage values must be strictly descending.");
            }

            previousCoverage = lod.ScreenCoverage;
        }
    }

    private static void ValidateAnimations(List<RdmAnimationDefinition> animations, HashSet<string> sourceIds)
    {
        ArgumentNullException.ThrowIfNull(animations);

        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (RdmAnimationDefinition animation in animations)
        {
            ArgumentNullException.ThrowIfNull(animation);
            ValidateStateName(animation.Name, nameof(animation.Name));
            if (!names.Add(animation.Name))
            {
                throw new InvalidOperationException(
                    $"Animation '{animation.Name}' is defined more than once in the same state.");
            }

            ValidateSourceReference(animation.Source, sourceIds, nameof(animation.Source));
            if (animation.Events.Any(static animationEvent => animationEvent.Time < 0.0f || float.IsNaN(animationEvent.Time) || float.IsInfinity(animationEvent.Time)))
            {
                throw new InvalidOperationException(
                    $"Animation '{animation.Name}' contains an invalid event time.");
            }
        }
    }

    private static void ValidateAttachments(List<RdmAttachmentDefinition> attachments)
    {
        ArgumentNullException.ThrowIfNull(attachments);

        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (RdmAttachmentDefinition attachment in attachments)
        {
            ArgumentNullException.ThrowIfNull(attachment);
            ValidateStateName(attachment.Name, nameof(attachment.Name));
            if (!names.Add(attachment.Name))
            {
                throw new InvalidOperationException(
                    $"Attachment '{attachment.Name}' is defined more than once in the same state.");
            }

            ValidateNonEmpty(attachment.Node, nameof(attachment.Node));
            if (!string.IsNullOrWhiteSpace(attachment.Bone))
            {
                ValidateNonEmpty(attachment.Bone, nameof(attachment.Bone));
            }

            ValidateMetadata(attachment.Tags, nameof(attachment.Tags));
        }
    }

    private static void ValidateMorphTargets(List<RdmMorphTargetDefinition> morphTargets)
    {
        ArgumentNullException.ThrowIfNull(morphTargets);

        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (RdmMorphTargetDefinition morphTarget in morphTargets)
        {
            ArgumentNullException.ThrowIfNull(morphTarget);
            ValidateStateName(morphTarget.Name, nameof(morphTarget.Name));
            if (!names.Add(morphTarget.Name))
            {
                throw new InvalidOperationException(
                    $"Morph target '{morphTarget.Name}' is defined more than once in the same state.");
            }

            if (float.IsNaN(morphTarget.DefaultWeight) || float.IsInfinity(morphTarget.DefaultWeight))
            {
                throw new InvalidOperationException(
                    $"Morph target '{morphTarget.Name}' has an invalid default weight.");
            }

            if (morphTarget.MinWeight > morphTarget.MaxWeight)
            {
                throw new InvalidOperationException(
                    $"Morph target '{morphTarget.Name}' has a minimum weight greater than its maximum weight.");
            }
        }
    }

    private static void ValidateStateMaterials(List<RdmStateMaterialBindingDefinition> materials, HashSet<string> materialNames)
    {
        ArgumentNullException.ThrowIfNull(materials);

        HashSet<string> slots = new(StringComparer.Ordinal);
        foreach (RdmStateMaterialBindingDefinition material in materials)
        {
            ArgumentNullException.ThrowIfNull(material);
            ValidateNonEmpty(material.Slot, nameof(material.Slot));
            if (!slots.Add(material.Slot))
            {
                throw new InvalidOperationException(
                    $"Material slot '{material.Slot}' is defined more than once in the same state.");
            }

            ValidateReference(material.Material, materialNames, "material", nameof(material.Material));
        }
    }

    private static void ValidateCollision(RdmCollisionDefinition? collision, HashSet<string> sourceIds)
    {
        if (collision == null)
        {
            return;
        }

        ValidateSourceReference(collision.Source, sourceIds, nameof(collision.Source));
        if (!s_validCollisionKinds.Contains(collision.Kind, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Collision kind '{collision.Kind}' is not supported. Expected one of: {string.Join(", ", s_validCollisionKinds)}.");
        }
    }

    private static void ValidateTexturePaths(Dictionary<string, string> textures)
    {
        ArgumentNullException.ThrowIfNull(textures);

        foreach ((string slot, string path) in textures)
        {
            ValidateNonEmpty(slot, nameof(textures));
            ValidateRelativePath(path, nameof(textures));
        }
    }

    private static void ValidateNamedReferences(IEnumerable<string> names, HashSet<string> knownNames, string label)
    {
        foreach (string name in names)
        {
            ValidateReference(name, knownNames, label, label);
        }
    }

    private static void ValidateSourceReference(string sourceId, HashSet<string> sourceIds, string paramName)
    {
        ValidateReference(sourceId, sourceIds, "source", paramName);
    }

    private static void ValidateOptionalSourceReference(string? sourceId, HashSet<string> sourceIds, string paramName)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            return;
        }

        ValidateSourceReference(sourceId, sourceIds, paramName);
    }

    private static void ValidateReference(string name, HashSet<string> knownNames, string label, string paramName)
    {
        ValidateNonEmpty(name, paramName);
        if (!knownNames.Contains(name))
        {
            throw new InvalidOperationException(
                $"Referenced {label} '{name}' does not exist.");
        }
    }

    private static void ValidateMetadata(Dictionary<string, string> metadata, string paramName)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        foreach ((string key, string value) in metadata)
        {
            ValidateNonEmpty(key, paramName);
            ValidateNonEmpty(value, paramName);
        }
    }

    private static void ValidateRelativePath(string path, string paramName)
    {
        ValidateNonEmpty(path, paramName);

        if (path.Contains('\\', StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Path '{path}' must use forward slashes.");
        }

        if (Path.IsPathRooted(path))
        {
            throw new InvalidOperationException($"Path '{path}' must be relative to the RDM package root.");
        }

        string[] segments = path.Split('/', StringSplitOptions.None);
        foreach (string segment in segments)
        {
            if (segment.Length == 0 || segment == "." || segment == "..")
            {
                throw new InvalidOperationException(
                    $"Path '{path}' contains an invalid segment '{segment}'.");
            }
        }
    }

    private static void ValidateStateName(string value, string paramName)
    {
        ValidateNonEmpty(value, paramName);

        foreach (char character in value)
        {
            if ((character >= 'a' && character <= 'z') || char.IsAsciiDigit(character) || character is '_' or '-')
            {
                continue;
            }

            throw new InvalidOperationException(
                $"Value '{value}' must only contain lowercase ASCII letters, digits, underscores, or dashes.");
        }
    }

    private static void ValidateAxis(string axis, string _)
    {
        if (axis is "x" or "y" or "z" or "-x" or "-y" or "-z")
        {
            return;
        }

        throw new InvalidOperationException(
            $"Axis '{axis}' is not supported. Expected one of x, y, z, -x, -y, -z.");
    }

    private static void ValidateNonEmpty(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be null or whitespace.", paramName);
        }
    }

    private static void ValidatePositive(float value, string paramName)
    {
        if (value <= 0.0f || float.IsNaN(value) || float.IsInfinity(value))
        {
            throw new ArgumentOutOfRangeException(paramName, value, "Value must be finite and greater than zero.");
        }
    }
}
