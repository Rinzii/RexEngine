using Rex.Shared.Assets.Rdm;

namespace Rex.Shared.Tests.Assets;

public sealed class RdmDefinitionValidatorTests
{
    [Fact]
    public void Validate_accepts_valid_definition_with_fbx_and_local_prototypes()
    {
        RdmDefinition definition = CreateValidDefinition();

        RdmDefinitionValidator.Validate(definition);
    }

    [Fact]
    public void Validate_rejects_unknown_source_format()
    {
        RdmDefinition definition = CreateValidDefinition();
        definition.Sources[0].Format = "dae";

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() => RdmDefinitionValidator.Validate(definition));

        Assert.Contains("Source format 'dae' is not supported", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_rejects_missing_material_reference()
    {
        RdmDefinition definition = CreateValidDefinition();
        definition.States[0].Materials[0].Material = "missing_material";

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() => RdmDefinitionValidator.Validate(definition));

        Assert.Contains("Referenced material 'missing_material' does not exist", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_rejects_invalid_prototype_parent()
    {
        RdmDefinition definition = CreateValidDefinition();
        definition.Prototypes[0].Inherits = "missing_prototype";

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() => RdmDefinitionValidator.Validate(definition));

        Assert.Contains("Referenced prototype 'missing_prototype' does not exist", exception.Message, StringComparison.Ordinal);
    }

    private static RdmDefinition CreateValidDefinition()
    {
        return new RdmDefinition
        {
            Version = 1,
            License = "CC-BY-4.0",
            Copyright = "Example Author",
            Size = new RdmSizeDefinition
            {
                X = 1.0f,
                Y = 2.0f,
                Z = 1.0f
            },
            Sources =
            [
                new RdmSourceDefinition
                {
                    Id = "crate_master",
                    Kind = "model",
                    Format = "fbx",
                    Path = "sources/crate_master.fbx"
                },
                new RdmSourceDefinition
                {
                    Id = "crate_open",
                    Kind = "animation",
                    Format = "gltf",
                    Path = "animations/open.gltf"
                },
                new RdmSourceDefinition
                {
                    Id = "crate_collision",
                    Kind = "collision",
                    Format = "gltf",
                    Path = "collision/crate_collision.gltf"
                }
            ],
            Materials =
            [
                new RdmMaterialDefinition
                {
                    Name = "crate_painted",
                    Shader = "standard",
                    Domain = "opaque",
                    Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["roughness"] = "0.8"
                    },
                    Textures = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["albedo"] = "textures/crate_albedo.png"
                    }
                }
            ],
            States =
            [
                new RdmStateDefinition
                {
                    Name = "closed",
                    Source = "crate_master",
                    Flags = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["visibility"] = "world"
                    },
                    Lods =
                    [
                        new RdmLodDefinition
                        {
                            Source = "crate_master",
                            ScreenCoverage = 0.5f
                        }
                    ],
                    Animations =
                    [
                        new RdmAnimationDefinition
                        {
                            Name = "open",
                            Source = "crate_open",
                            Loop = false,
                            Events =
                            [
                                new RdmAnimationEventDefinition
                                {
                                    Name = "latch_release",
                                    Time = 0.125f
                                }
                            ]
                        }
                    ],
                    Attachments =
                    [
                        new RdmAttachmentDefinition
                        {
                            Name = "top",
                            Node = "top_socket"
                        }
                    ],
                    Materials =
                    [
                        new RdmStateMaterialBindingDefinition
                        {
                            Slot = "crate_mesh",
                            Material = "crate_painted"
                        }
                    ],
                    MorphTargets =
                    [
                        new RdmMorphTargetDefinition
                        {
                            Name = "dent",
                            DefaultWeight = 0.0f,
                            MinWeight = 0.0f,
                            MaxWeight = 1.0f
                        }
                    ],
                    Collision = new RdmCollisionDefinition
                    {
                        Source = "crate_collision",
                        Kind = "convexHull"
                    }
                }
            ],
            Prototypes =
            [
                new RdmPrototypeDefinition
                {
                    Name = "crate_render",
                    Kind = "render",
                    DefaultState = "closed",
                    States = ["closed"],
                    Animations = ["open"],
                    Materials = ["crate_painted"]
                }
            ],
            Load = new RdmLoadParameters
            {
                Srgb = true,
                GenerateNormals = true,
                GenerateTangents = true,
                OptimizeMeshes = true,
                OptimizeAnimations = true,
                GpuInstance = true,
                CpuReadable = false,
                Stream = true,
                PreferBinaryJson = true
            }
        };
    }
}
