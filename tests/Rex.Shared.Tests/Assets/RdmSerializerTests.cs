using Rex.Shared.Assets.Rdm;

namespace Rex.Shared.Tests.Assets;

public sealed class RdmSerializerTests
{
    [Fact]
    public void SerializeJson_orders_sources_states_materials_and_prototypes_canonically()
    {
        RdmDefinition definition = new()
        {
            Version = 1,
            License = "CC0-1.0",
            Copyright = "Example",
            Size = new RdmSizeDefinition
            {
                X = 1.0f,
                Y = 1.0f,
                Z = 1.0f
            },
            Sources =
            [
                new RdmSourceDefinition
                {
                    Id = "z_source",
                    Kind = "model",
                    Format = "fbx",
                    Path = "sources/z_source.fbx"
                },
                new RdmSourceDefinition
                {
                    Id = "a_source",
                    Kind = "model",
                    Format = "gltf",
                    Path = "sources/a_source.gltf"
                }
            ],
            Materials =
            [
                new RdmMaterialDefinition
                {
                    Name = "z_material",
                    Shader = "standard"
                },
                new RdmMaterialDefinition
                {
                    Name = "a_material",
                    Shader = "standard"
                }
            ],
            States =
            [
                new RdmStateDefinition
                {
                    Name = "open",
                    Source = "z_source"
                },
                new RdmStateDefinition
                {
                    Name = "closed",
                    Source = "a_source",
                    Lods =
                    [
                        new RdmLodDefinition
                        {
                            Source = "a_source",
                            ScreenCoverage = 0.25f
                        },
                        new RdmLodDefinition
                        {
                            Source = "a_source",
                            ScreenCoverage = 0.5f
                        }
                    ],
                    Animations =
                    [
                        new RdmAnimationDefinition
                        {
                            Name = "shake",
                            Source = "a_source"
                        },
                        new RdmAnimationDefinition
                        {
                            Name = "open",
                            Source = "a_source",
                            Loop = false
                        }
                    ],
                    Attachments =
                    [
                        new RdmAttachmentDefinition
                        {
                            Name = "top",
                            Node = "top_socket"
                        },
                        new RdmAttachmentDefinition
                        {
                            Name = "handle",
                            Node = "handle_socket"
                        }
                    ]
                }
            ],
            Prototypes =
            [
                new RdmPrototypeDefinition
                {
                    Name = "z_proto",
                    Kind = "render"
                },
                new RdmPrototypeDefinition
                {
                    Name = "a_proto",
                    Kind = "attachmentSet"
                }
            ]
        };

        string json = RdmSerializer.SerializeJson(definition);
        RdmDefinition canonical = RdmSerializer.DeserializeJson(json);

        Assert.Equal(["a_source", "z_source"], canonical.Sources.Select(static source => source.Id).ToArray());
        Assert.Equal(["a_material", "z_material"], canonical.Materials.Select(static material => material.Name).ToArray());
        Assert.Equal(["closed", "open"], canonical.States.Select(static state => state.Name).ToArray());
        Assert.Equal([0.5f, 0.25f], canonical.States[0].Lods.Select(static lod => lod.ScreenCoverage).ToArray());
        Assert.Equal(["open", "shake"], canonical.States[0].Animations.Select(static animation => animation.Name).ToArray());
        Assert.Equal(["attachmentSet", "render"], canonical.Prototypes.Select(static prototype => prototype.Kind).ToArray());
    }

    [Fact]
    public void BinaryJson_round_trips_valid_metadata()
    {
        const string Json = /*lang=json,strict*/ """
            {
              "version": 1,
              "license": "CC-BY-4.0",
              "copyright": "Example",
              "size": {
                "x": 1.0,
                "y": 2.0,
                "z": 1.0
              },
              "sources": [
                {
                  "id": "crate_master",
                  "kind": "model",
                  "format": "fbx",
                  "path": "sources/crate_master.fbx"
                }
              ],
              "states": [
                {
                  "name": "closed",
                  "source": "crate_master"
                }
              ],
              "prototypes": [
                {
                  "name": "crate_render",
                  "kind": "render",
                  "defaultState": "closed"
                }
              ]
            }
            """;

        RdmDefinition fromJson = RdmSerializer.DeserializeJson(Json);
        byte[] binary = RdmSerializer.SerializeBinaryJson(fromJson);
        RdmDefinition fromBinary = RdmSerializer.DeserializeBinaryJson(binary);

        Assert.Equal(1, fromBinary.Version);
        Assert.Equal("CC-BY-4.0", fromBinary.License);
        _ = Assert.Single(fromBinary.Sources);
        Assert.Equal("fbx", fromBinary.Sources[0].Format);
        _ = Assert.Single(fromBinary.States);
        Assert.Equal("closed", fromBinary.States[0].Name);
        _ = Assert.Single(fromBinary.Prototypes);
        Assert.Equal("crate_render", fromBinary.Prototypes[0].Name);
    }

    [Fact]
    public void DeserializeBinaryJson_rejects_unsupported_version()
    {
        RdmDefinition definition = new()
        {
            Version = 1,
            License = "CC-BY-4.0",
            Copyright = "Example",
            Size = new RdmSizeDefinition
            {
                X = 1,
                Y = 1,
                Z = 1
            },
            Sources =
            [
                new RdmSourceDefinition
                {
                    Id = "crate_master",
                    Kind = "model",
                    Format = "fbx",
                    Path = "sources/crate_master.fbx"
                }
            ],
            States =
            [
                new RdmStateDefinition
                {
                    Name = "closed",
                    Source = "crate_master"
                }
            ]
        };

        byte[] binary = RdmSerializer.SerializeBinaryJson(definition);
        binary[4] = 2;

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() => RdmSerializer.DeserializeBinaryJson(binary));

        Assert.Contains("Binary JSON version 2 is not supported", exception.Message, StringComparison.Ordinal);
    }
}
