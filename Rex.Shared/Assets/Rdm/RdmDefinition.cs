using Rex.Shared.Serialization.Manager.Attributes;

namespace Rex.Shared.Assets.Rdm;

/// <summary>
/// Canonical metadata document for one Rex Data Model package.
/// </summary>
[DataDefinition]
public sealed partial class RdmDefinition
{
    /// <summary>Current RDM metadata version supported by the engine.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Gets or sets the format version.</summary>
    [DataField("version")]
    public int Version { get; set; } = CurrentVersion;

    /// <summary>Gets or sets the SPDX license identifier for the package.</summary>
    [DataField("license")]
    public string License { get; set; } = string.Empty;

    /// <summary>Gets or sets copyright and attribution text for the package.</summary>
    [DataField("copyright")]
    public string Copyright { get; set; } = string.Empty;

    /// <summary>Gets or sets the authored axis-aligned size of the model in meters.</summary>
    [DataField("size")]
    public RdmSizeDefinition Size { get; set; } = new();

    /// <summary>Gets or sets the declared source assets used by this package.</summary>
    [DataField("sources")]
    public List<RdmSourceDefinition> Sources { get; set; } = [];

    /// <summary>Gets or sets the material definitions used by states in this package.</summary>
    [DataField("materials")]
    public List<RdmMaterialDefinition> Materials { get; set; } = [];

    /// <summary>Gets or sets the named model states contained in the package.</summary>
    [DataField("states")]
    public List<RdmStateDefinition> States { get; set; } = [];

    /// <summary>Gets or sets reusable local package prototypes.</summary>
    [DataField("prototypes")]
    public List<RdmPrototypeDefinition> Prototypes { get; set; } = [];

    /// <summary>Gets or sets optional load parameters that affect import behavior.</summary>
    [DataField("load")]
    public RdmLoadParameters? Load { get; set; }
}

/// <summary>
/// Authored axis-aligned size of an RDM package in meters.
/// </summary>
[DataDefinition]
public sealed partial class RdmSizeDefinition
{
    /// <summary>Gets or sets the authored X extent in meters.</summary>
    [DataField("x")]
    public float X { get; set; }

    /// <summary>Gets or sets the authored Y extent in meters.</summary>
    [DataField("y")]
    public float Y { get; set; }

    /// <summary>Gets or sets the authored Z extent in meters.</summary>
    [DataField("z")]
    public float Z { get; set; }
}

/// <summary>
/// Describes one source asset that can be imported into runtime-ready model data.
/// </summary>
[DataDefinition]
public sealed partial class RdmSourceDefinition
{
    /// <summary>Gets or sets the unique source identifier.</summary>
    [DataField("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the import kind for the source.</summary>
    [DataField("kind")]
    public string Kind { get; set; } = "model";

    /// <summary>Gets or sets the declared source file format.</summary>
    [DataField("format")]
    public string Format { get; set; } = "fbx";

    /// <summary>Gets or sets the relative path to the source asset.</summary>
    [DataField("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional source scene or object entry name.</summary>
    [DataField("entry")]
    public string? Entry { get; set; }

    /// <summary>Gets or sets the source import scale.</summary>
    [DataField("scale")]
    public float Scale { get; set; } = 1.0f;

    /// <summary>Gets or sets the authored up axis for import.</summary>
    [DataField("upAxis")]
    public string UpAxis { get; set; } = "y";

    /// <summary>Gets or sets the authored forward axis for import.</summary>
    [DataField("forwardAxis")]
    public string ForwardAxis { get; set; } = "z";

    /// <summary>Gets or sets optional source-specific metadata.</summary>
    [DataField("metadata")]
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Metadata for one named render state inside an RDM package.
/// </summary>
[DataDefinition]
public sealed partial class RdmStateDefinition
{
    /// <summary>Gets or sets the canonical state name.</summary>
    [DataField("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the source id for the primary model scene.</summary>
    [DataField("source")]
    public string Source { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional source id for the shared skeleton.</summary>
    [DataField("skeletonSource")]
    public string? SkeletonSource { get; set; }

    /// <summary>Gets or sets optional string flags for renderer- or gameplay-specific metadata.</summary>
    [DataField("flags")]
    public Dictionary<string, string> Flags { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets optional level-of-detail model sources for this state.</summary>
    [DataField("lods")]
    public List<RdmLodDefinition> Lods { get; set; } = [];

    /// <summary>Gets or sets optional animation clips available for this state.</summary>
    [DataField("animations")]
    public List<RdmAnimationDefinition> Animations { get; set; } = [];

    /// <summary>Gets or sets optional named attachment points for the state.</summary>
    [DataField("attachments")]
    public List<RdmAttachmentDefinition> Attachments { get; set; } = [];

    /// <summary>Gets or sets optional material bindings for this state.</summary>
    [DataField("materials")]
    public List<RdmStateMaterialBindingDefinition> Materials { get; set; } = [];

    /// <summary>Gets or sets optional morph target metadata for this state.</summary>
    [DataField("morphTargets")]
    public List<RdmMorphTargetDefinition> MorphTargets { get; set; } = [];

    /// <summary>Gets or sets optional collision source metadata for this state.</summary>
    [DataField("collision")]
    public RdmCollisionDefinition? Collision { get; set; }
}

/// <summary>
/// Metadata for one level-of-detail source in a model state.
/// </summary>
[DataDefinition]
public sealed partial class RdmLodDefinition
{
    /// <summary>Gets or sets the source id for the LOD asset.</summary>
    [DataField("source")]
    public string Source { get; set; } = string.Empty;

    /// <summary>Gets or sets the normalized screen coverage threshold for this LOD.</summary>
    [DataField("screenCoverage")]
    public float ScreenCoverage { get; set; }
}

/// <summary>
/// Metadata for one named animation clip in a model state.
/// </summary>
[DataDefinition]
public sealed partial class RdmAnimationDefinition
{
    /// <summary>Gets or sets the canonical animation name.</summary>
    [DataField("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the source id storing the animation data.</summary>
    [DataField("source")]
    public string Source { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional clip name inside the source asset.</summary>
    [DataField("clip")]
    public string? Clip { get; set; }

    /// <summary>Gets or sets a value indicating whether the animation loops by default.</summary>
    [DataField("loop")]
    public bool Loop { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether root motion is enabled for the clip.</summary>
    [DataField("rootMotion")]
    public bool RootMotion { get; set; }

    /// <summary>Gets or sets optional animation events keyed by time.</summary>
    [DataField("events")]
    public List<RdmAnimationEventDefinition> Events { get; set; } = [];
}

/// <summary>
/// Metadata for one named animation event inside a clip.
/// </summary>
[DataDefinition]
public sealed partial class RdmAnimationEventDefinition
{
    /// <summary>Gets or sets the event name.</summary>
    [DataField("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the event time in seconds.</summary>
    [DataField("time")]
    public float Time { get; set; }

    /// <summary>Gets or sets optional event payload fields.</summary>
    [DataField("payload")]
    public Dictionary<string, string> Payload { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Metadata for one named attachment point in a model state.
/// </summary>
[DataDefinition]
public sealed partial class RdmAttachmentDefinition
{
    /// <summary>Gets or sets the canonical attachment name.</summary>
    [DataField("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the node name used to resolve the attachment at runtime.</summary>
    [DataField("node")]
    public string Node { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional bone name for skinned attachments.</summary>
    [DataField("bone")]
    public string? Bone { get; set; }

    /// <summary>Gets or sets optional attachment tags.</summary>
    [DataField("tags")]
    public Dictionary<string, string> Tags { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Metadata for one material definition used by model states.
/// </summary>
[DataDefinition]
public sealed partial class RdmMaterialDefinition
{
    /// <summary>Gets or sets the canonical material name.</summary>
    [DataField("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional source id for material import data.</summary>
    [DataField("source")]
    public string? Source { get; set; }

    /// <summary>Gets or sets the shader identifier to use for the material.</summary>
    [DataField("shader")]
    public string Shader { get; set; } = "standard";

    /// <summary>Gets or sets the material domain.</summary>
    [DataField("domain")]
    public string Domain { get; set; } = "opaque";

    /// <summary>Gets or sets whether the material is double-sided.</summary>
    [DataField("doubleSided")]
    public bool DoubleSided { get; set; }

    /// <summary>Gets or sets optional scalar/vector parameter metadata.</summary>
    [DataField("parameters")]
    public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets optional texture slot references.</summary>
    [DataField("textures")]
    public Dictionary<string, string> Textures { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Metadata for one state-local material binding.
/// </summary>
[DataDefinition]
public sealed partial class RdmStateMaterialBindingDefinition
{
    /// <summary>Gets or sets the mesh or slot name to bind.</summary>
    [DataField("slot")]
    public string Slot { get; set; } = string.Empty;

    /// <summary>Gets or sets the material definition name to apply.</summary>
    [DataField("material")]
    public string Material { get; set; } = string.Empty;
}

/// <summary>
/// Metadata for one morph target exposed by a state.
/// </summary>
[DataDefinition]
public sealed partial class RdmMorphTargetDefinition
{
    /// <summary>Gets or sets the morph target name.</summary>
    [DataField("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the default morph weight.</summary>
    [DataField("defaultWeight")]
    public float DefaultWeight { get; set; }

    /// <summary>Gets or sets the minimum morph weight.</summary>
    [DataField("minWeight")]
    public float MinWeight { get; set; }

    /// <summary>Gets or sets the maximum morph weight.</summary>
    [DataField("maxWeight")]
    public float MaxWeight { get; set; } = 1.0f;
}

/// <summary>
/// Metadata for collision geometry associated with a model state.
/// </summary>
[DataDefinition]
public sealed partial class RdmCollisionDefinition
{
    /// <summary>Gets or sets the source id containing collision geometry.</summary>
    [DataField("source")]
    public string Source { get; set; } = string.Empty;

    /// <summary>Gets or sets the collision import kind.</summary>
    [DataField("kind")]
    public string Kind { get; set; } = "triangleMesh";
}

/// <summary>
/// Reusable local package prototype metadata.
/// </summary>
[DataDefinition]
public sealed partial class RdmPrototypeDefinition
{
    /// <summary>Gets or sets the canonical prototype name.</summary>
    [DataField("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the prototype kind.</summary>
    [DataField("kind")]
    public string Kind { get; set; } = "render";

    /// <summary>Gets or sets the optional parent prototype name.</summary>
    [DataField("inherits")]
    public string? Inherits { get; set; }

    /// <summary>Gets or sets the default state name for this prototype.</summary>
    [DataField("defaultState")]
    public string? DefaultState { get; set; }

    /// <summary>Gets or sets the states included by this prototype.</summary>
    [DataField("states")]
    public List<string> States { get; set; } = [];

    /// <summary>Gets or sets the animations included by this prototype.</summary>
    [DataField("animations")]
    public List<string> Animations { get; set; } = [];

    /// <summary>Gets or sets the material definitions included by this prototype.</summary>
    [DataField("materials")]
    public List<string> Materials { get; set; } = [];

    /// <summary>Gets or sets optional prototype tags.</summary>
    [DataField("tags")]
    public Dictionary<string, string> Tags { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Optional load parameters that affect how RDM assets are imported at runtime.
/// </summary>
[DataDefinition]
public sealed partial class RdmLoadParameters
{
    /// <summary>Gets or sets a value indicating whether textures are interpreted as sRGB by default.</summary>
    [DataField("srgb")]
    public bool Srgb { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether missing normals should be generated at load time.</summary>
    [DataField("generateNormals")]
    public bool GenerateNormals { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether missing tangents should be generated at load time.</summary>
    [DataField("generateTangents")]
    public bool GenerateTangents { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether mesh data should be optimized for runtime access.</summary>
    [DataField("optimizeMeshes")]
    public bool OptimizeMeshes { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether animation data should be optimized for runtime playback.</summary>
    [DataField("optimizeAnimations")]
    public bool OptimizeAnimations { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the renderer may treat the asset as GPU-instanceable.</summary>
    [DataField("gpuInstance")]
    public bool GpuInstance { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether CPU-side readable mesh data should be retained after upload.</summary>
    [DataField("cpuReadable")]
    public bool CpuReadable { get; set; }

    /// <summary>Gets or sets a value indicating whether streaming-friendly runtime behavior is preferred.</summary>
    [DataField("stream")]
    public bool Stream { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether binary JSON metadata should be preferred when available.</summary>
    [DataField("preferBinaryJson")]
    public bool PreferBinaryJson { get; set; } = true;
}
