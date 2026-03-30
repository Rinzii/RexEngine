using Rex.Shared.Net.Transfer;

namespace Rex.Shared.Tests.Net.Transfer;

// protobuf-net helpers for bulk DTOs.
public sealed class ProtoSerializerTests
{
    [Fact]
    // MapData nested lists and maps round trip through bytes.
    public void Serialize_and_Deserialize_array_round_trip_MapData()
    {
        var original = new MapData
        {
            MapName = "test-map",
            Width = 4,
            Height = 3,
            Tiles =
            [
                new MapTile { X = 0, Y = 0, TileId = 1, Flags = 2 }
            ],
            Entities =
            [
                new MapEntity
                {
                    EntityId = 5,
                    EntityType = "crate",
                    X = 1f,
                    Y = 2f,
                    Z = 3f,
                    RotationY = 90f
                }
            ],
            Properties = new Dictionary<string, string> { ["mode"] = "deathmatch" }
        };

        var bytes = ProtoSerializer.Serialize(original);
        var copy = ProtoSerializer.Deserialize<MapData>(bytes);

        Assert.Equal(original.MapName, copy.MapName);
        Assert.Equal(original.Width, copy.Width);
        Assert.Equal(original.Height, copy.Height);
        Assert.Single(copy.Tiles);
        Assert.Equal(original.Tiles[0].TileId, copy.Tiles[0].TileId);
        Assert.Single(copy.Entities);
        Assert.Equal("crate", copy.Entities[0].EntityType);
        Assert.Equal("deathmatch", copy.Properties["mode"]);
    }

    [Fact]
    // ReadOnlyMemory overload matches byte array Deserialize.
    public void Deserialize_ReadOnlyMemory_matches_array_overload()
    {
        var manifest = new AssetManifest
        {
            Version = 3,
            Assets =
            [
                new AssetEntry { Path = "/a/b", Size = 99, Hash = "abc" }
            ]
        };
        var bytes = ProtoSerializer.Serialize(manifest);
        var fromMemory = ProtoSerializer.Deserialize<AssetManifest>(bytes.AsMemory());
        Assert.Equal(manifest.Version, fromMemory.Version);
        Assert.Single(fromMemory.Assets);
        Assert.Equal("/a/b", fromMemory.Assets[0].Path);
    }
}
