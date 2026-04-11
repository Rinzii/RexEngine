using Rex.Sandbox.Shared.Net.Transfer;
using Rex.Shared.Net.Transfer;

namespace Rex.Sandbox.Shared.Tests.Net.Transfer;

// Protobuf net helpers for Sandbox sample bulk DTOs.
public sealed class SandboxProtoSerializerTests
{
    [Fact]
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

        byte[] bytes = ProtoSerializer.Serialize(original);
        MapData copy = ProtoSerializer.Deserialize<MapData>(bytes);

        Assert.Equal(original.MapName, copy.MapName);
        Assert.Equal(original.Width, copy.Width);
        Assert.Equal(original.Height, copy.Height);
        _ = Assert.Single(copy.Tiles);
        Assert.Equal(original.Tiles[0].TileId, copy.Tiles[0].TileId);
        _ = Assert.Single(copy.Entities);
        Assert.Equal("crate", copy.Entities[0].EntityType);
        Assert.Equal("deathmatch", copy.Properties["mode"]);
    }

    [Fact]
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
        byte[] bytes = ProtoSerializer.Serialize(manifest);
        AssetManifest fromMemory = ProtoSerializer.Deserialize<AssetManifest>(bytes.AsMemory());
        Assert.Equal(manifest.Version, fromMemory.Version);
        _ = Assert.Single(fromMemory.Assets);
        Assert.Equal("/a/b", fromMemory.Assets[0].Path);
    }

    [Fact]
    public void ServerConfigData_round_trip()
    {
        var original = new ServerConfigData
        {
            ServerName = "test",
            TickRate = 45,
            MaxPlayers = 8,
            MapName = "lobby",
            CVars = new Dictionary<string, string> { ["sv_cheats"] = "0" }
        };

        ServerConfigData copy = ProtoSerializer.Deserialize<ServerConfigData>(ProtoSerializer.Serialize(original));

        Assert.Equal(original.ServerName, copy.ServerName);
        Assert.Equal(original.TickRate, copy.TickRate);
        Assert.Equal(original.MaxPlayers, copy.MaxPlayers);
        Assert.Equal(original.MapName, copy.MapName);
        Assert.Equal("0", copy.CVars["sv_cheats"]);
    }

    [Fact]
    public void EntityBulkState_round_trip()
    {
        var owner = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var original = new EntityBulkState
        {
            ServerTick = 100u,
            Entities =
            [
                new BulkEntityData
                {
                    EntityId = 3,
                    EntityType = "box",
                    OwnerClientId = owner,
                    X = 1f,
                    Y = 2f,
                    Z = 3f,
                    RotationY = 45f,
                    ComponentData = new Dictionary<string, byte[]> { ["a"] = [1, 2, 3] }
                }
            ]
        };

        EntityBulkState copy = ProtoSerializer.Deserialize<EntityBulkState>(ProtoSerializer.Serialize(original));

        Assert.Equal(original.ServerTick, copy.ServerTick);
        _ = Assert.Single(copy.Entities);
        Assert.Equal(3, copy.Entities[0].EntityId);
        Assert.Equal(owner, copy.Entities[0].OwnerClientId);
        Assert.Equal(new byte[] { 1, 2, 3 }, copy.Entities[0].ComponentData["a"]);
    }
}
