using Rex.Shared.Serialization.Manager;

namespace Rex.Shared.Assets.Rdm;

/// <summary>
/// Reads and writes canonical text and binary metadata for RDM packages.
/// </summary>
public static class RdmSerializer
{
    private static readonly SerializationManager s_serializationManager = new();

    /// <summary>
    /// Parses a JSON metadata document, validates it and returns a canonicalized object graph.
    /// </summary>
    /// <param name="json">JSON metadata to parse.</param>
    /// <returns>Canonicalized RDM metadata.</returns>
    public static RdmDefinition DeserializeJson(string json)
    {
        DataNode node = DataNodeJsonSerializer.Read(json);
        return DeserializeNode(node);
    }

    /// <summary>
    /// Parses a binary JSON metadata document, validates it and returns a canonicalized object graph.
    /// </summary>
    /// <param name="payload">Binary JSON metadata to parse.</param>
    /// <returns>Canonicalized RDM metadata.</returns>
    public static RdmDefinition DeserializeBinaryJson(ReadOnlySpan<byte> payload)
    {
        DataNode node = DataNodeBinaryJsonSerializer.Read(payload);
        return DeserializeNode(node);
    }

    /// <summary>
    /// Parses one metadata file and dispatches by file extension.
    /// </summary>
    /// <param name="path">Metadata file path.</param>
    /// <returns>Canonicalized RDM metadata.</returns>
    public static RdmDefinition DeserializeFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return Path.GetExtension(path) switch
        {
            ".rdm" => DeserializeJson(File.ReadAllText(path)),
            ".rdmb" => DeserializeBinaryJson(File.ReadAllBytes(path)),
            _ => throw new InvalidOperationException(
                $"RDM metadata file '{path}' must end with .rdm or .rdmb.")
        };
    }

    /// <summary>
    /// Writes a canonical JSON metadata document for an RDM package.
    /// </summary>
    /// <param name="definition">Metadata definition to write.</param>
    /// <returns>Canonical JSON for the definition.</returns>
    public static string SerializeJson(RdmDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        DataNode node = SerializeNode(definition);
        return DataNodeJsonSerializer.Write(node, indented: true);
    }

    /// <summary>
    /// Writes a compact binary JSON metadata document for an RDM package.
    /// </summary>
    /// <param name="definition">Metadata definition to write.</param>
    /// <returns>Binary JSON payload for the definition.</returns>
    public static byte[] SerializeBinaryJson(RdmDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        DataNode node = SerializeNode(definition);
        return DataNodeBinaryJsonSerializer.Write(node);
    }

    private static RdmDefinition DeserializeNode(DataNode node)
    {
        RdmDefinition definition = s_serializationManager.Read<RdmDefinition>(node);
        RdmDefinition canonical = Canonicalize(definition);
        RdmDefinitionValidator.Validate(canonical);
        return canonical;
    }

    private static DataNode SerializeNode(RdmDefinition definition)
    {
        RdmDefinition canonical = Canonicalize(definition);
        RdmDefinitionValidator.Validate(canonical);
        return s_serializationManager.WriteValue(canonical, alwaysWrite: true);
    }

    private static RdmDefinition Canonicalize(RdmDefinition definition)
    {
        RdmDefinition canonical = new()
        {
            Version = definition.Version,
            License = definition.License,
            Copyright = definition.Copyright,
            Size = new RdmSizeDefinition
            {
                X = definition.Size?.X ?? 0.0f,
                Y = definition.Size?.Y ?? 0.0f,
                Z = definition.Size?.Z ?? 0.0f
            },
            Sources = (definition.Sources ?? [])
                .OrderBy(static source => source.Id, StringComparer.Ordinal)
                .Select(CanonicalizeSource)
                .ToList(),
            Materials = (definition.Materials ?? [])
                .OrderBy(static material => material.Name, StringComparer.Ordinal)
                .Select(CanonicalizeMaterial)
                .ToList(),
            States = (definition.States ?? [])
                .OrderBy(static state => state.Name, StringComparer.Ordinal)
                .Select(CanonicalizeState)
                .ToList(),
            Prototypes = (definition.Prototypes ?? [])
                .OrderBy(static prototype => prototype.Kind, StringComparer.Ordinal)
                .ThenBy(static prototype => prototype.Name, StringComparer.Ordinal)
                .Select(CanonicalizePrototype)
                .ToList()
        };

        if (definition.Load != null)
        {
            canonical.Load = new RdmLoadParameters
            {
                Srgb = definition.Load.Srgb,
                GenerateNormals = definition.Load.GenerateNormals,
                GenerateTangents = definition.Load.GenerateTangents,
                OptimizeMeshes = definition.Load.OptimizeMeshes,
                OptimizeAnimations = definition.Load.OptimizeAnimations,
                GpuInstance = definition.Load.GpuInstance,
                CpuReadable = definition.Load.CpuReadable,
                Stream = definition.Load.Stream,
                PreferBinaryJson = definition.Load.PreferBinaryJson
            };
        }

        return canonical;
    }

    private static RdmSourceDefinition CanonicalizeSource(RdmSourceDefinition source)
    {
        return new RdmSourceDefinition
        {
            Id = source.Id,
            Kind = source.Kind,
            Format = source.Format,
            Path = source.Path,
            Entry = source.Entry,
            Scale = source.Scale,
            UpAxis = source.UpAxis,
            ForwardAxis = source.ForwardAxis,
            Metadata = CanonicalizeDictionary(source.Metadata)
        };
    }

    private static RdmMaterialDefinition CanonicalizeMaterial(RdmMaterialDefinition material)
    {
        return new RdmMaterialDefinition
        {
            Name = material.Name,
            Source = material.Source,
            Shader = material.Shader,
            Domain = material.Domain,
            DoubleSided = material.DoubleSided,
            Parameters = CanonicalizeDictionary(material.Parameters),
            Textures = CanonicalizeDictionary(material.Textures)
        };
    }

    private static RdmStateDefinition CanonicalizeState(RdmStateDefinition state)
    {
        return new RdmStateDefinition
        {
            Name = state.Name,
            Source = state.Source,
            SkeletonSource = state.SkeletonSource,
            Flags = CanonicalizeDictionary(state.Flags),
            Lods = (state.Lods ?? [])
                .OrderByDescending(static lod => lod.ScreenCoverage)
                .Select(static lod => new RdmLodDefinition
                {
                    Source = lod.Source,
                    ScreenCoverage = lod.ScreenCoverage
                })
                .ToList(),
            Animations = (state.Animations ?? [])
                .OrderBy(static animation => animation.Name, StringComparer.Ordinal)
                .Select(CanonicalizeAnimation)
                .ToList(),
            Attachments = (state.Attachments ?? [])
                .OrderBy(static attachment => attachment.Name, StringComparer.Ordinal)
                .Select(CanonicalizeAttachment)
                .ToList(),
            Materials = (state.Materials ?? [])
                .OrderBy(static material => material.Slot, StringComparer.Ordinal)
                .Select(static material => new RdmStateMaterialBindingDefinition
                {
                    Slot = material.Slot,
                    Material = material.Material
                })
                .ToList(),
            MorphTargets = (state.MorphTargets ?? [])
                .OrderBy(static morphTarget => morphTarget.Name, StringComparer.Ordinal)
                .Select(static morphTarget => new RdmMorphTargetDefinition
                {
                    Name = morphTarget.Name,
                    DefaultWeight = morphTarget.DefaultWeight,
                    MinWeight = morphTarget.MinWeight,
                    MaxWeight = morphTarget.MaxWeight
                })
                .ToList(),
            Collision = state.Collision == null
                ? null
                : new RdmCollisionDefinition
                {
                    Source = state.Collision.Source,
                    Kind = state.Collision.Kind
                }
        };
    }

    private static RdmAnimationDefinition CanonicalizeAnimation(RdmAnimationDefinition animation)
    {
        return new RdmAnimationDefinition
        {
            Name = animation.Name,
            Source = animation.Source,
            Clip = animation.Clip,
            Loop = animation.Loop,
            RootMotion = animation.RootMotion,
            Events = (animation.Events ?? [])
                .OrderBy(static animationEvent => animationEvent.Time)
                .ThenBy(static animationEvent => animationEvent.Name, StringComparer.Ordinal)
                .Select(static animationEvent => new RdmAnimationEventDefinition
                {
                    Name = animationEvent.Name,
                    Time = animationEvent.Time,
                    Payload = CanonicalizeDictionary(animationEvent.Payload)
                })
                .ToList()
        };
    }

    private static RdmAttachmentDefinition CanonicalizeAttachment(RdmAttachmentDefinition attachment)
    {
        return new RdmAttachmentDefinition
        {
            Name = attachment.Name,
            Node = attachment.Node,
            Bone = attachment.Bone,
            Tags = CanonicalizeDictionary(attachment.Tags)
        };
    }

    private static RdmPrototypeDefinition CanonicalizePrototype(RdmPrototypeDefinition prototype)
    {
        return new RdmPrototypeDefinition
        {
            Name = prototype.Name,
            Kind = prototype.Kind,
            Inherits = prototype.Inherits,
            DefaultState = prototype.DefaultState,
            States = (prototype.States ?? []).OrderBy(static state => state, StringComparer.Ordinal).ToList(),
            Animations = (prototype.Animations ?? []).OrderBy(static animation => animation, StringComparer.Ordinal).ToList(),
            Materials = (prototype.Materials ?? []).OrderBy(static material => material, StringComparer.Ordinal).ToList(),
            Tags = CanonicalizeDictionary(prototype.Tags)
        };
    }

    private static Dictionary<string, string> CanonicalizeDictionary(Dictionary<string, string>? values)
    {
        return (values ?? [])
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
    }
}
