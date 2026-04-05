using ProtoBuf;

namespace Rex.Sandbox.Shared.Net.Transfer;

/// <summary>
/// Bulk payload kinds for the Sandbox sample. A future external game would define its own transfer kinds.
/// </summary>
public enum SandboxBulkDataType : byte
{
    MapData = 1,
    AssetManifest = 2,
    ServerConfig = 3,
    EntityBulkState = 4,
    ResourceFile = 5
}

[ProtoContract]
public sealed class MapData
{
    [ProtoMember(1)] public string MapName { get; set; } = string.Empty;
    [ProtoMember(2)] public int Width { get; set; }
    [ProtoMember(3)] public int Height { get; set; }
    [ProtoMember(4)] public List<MapTile> Tiles { get; set; } = new();
    [ProtoMember(5)] public List<MapEntity> Entities { get; set; } = new();
    [ProtoMember(6)] public Dictionary<string, string> Properties { get; set; } = new();
}

[ProtoContract]
public sealed class MapTile
{
    [ProtoMember(1)] public int X { get; set; }
    [ProtoMember(2)] public int Y { get; set; }
    [ProtoMember(3)] public int TileId { get; set; }
    [ProtoMember(4)] public byte Flags { get; set; }
}

[ProtoContract]
public sealed class MapEntity
{
    [ProtoMember(1)] public int EntityId { get; set; }
    [ProtoMember(2)] public string EntityType { get; set; } = string.Empty;
    [ProtoMember(3)] public float X { get; set; }
    [ProtoMember(4)] public float Y { get; set; }
    [ProtoMember(5)] public float Z { get; set; }
    [ProtoMember(6)] public float RotationY { get; set; }
    [ProtoMember(7)] public Dictionary<string, byte[]> ComponentData { get; set; } = new();
}

[ProtoContract]
public sealed class AssetManifest
{
    [ProtoMember(1)] public int Version { get; set; }
    [ProtoMember(2)] public List<AssetEntry> Assets { get; set; } = new();
}

[ProtoContract]
public sealed class AssetEntry
{
    [ProtoMember(1)] public string Path { get; set; } = string.Empty;
    [ProtoMember(2)] public long Size { get; set; }
    [ProtoMember(3)] public string Hash { get; set; } = string.Empty;
}

[ProtoContract]
public sealed class ServerConfigData
{
    [ProtoMember(1)] public string ServerName { get; set; } = string.Empty;
    [ProtoMember(2)] public int TickRate { get; set; }
    [ProtoMember(3)] public int MaxPlayers { get; set; }
    [ProtoMember(4)] public string MapName { get; set; } = string.Empty;
    [ProtoMember(5)] public Dictionary<string, string> CVars { get; set; } = new();
}

[ProtoContract]
public sealed class EntityBulkState
{
    [ProtoMember(1)] public uint ServerTick { get; set; }
    [ProtoMember(2)] public List<BulkEntityData> Entities { get; set; } = new();
}

[ProtoContract]
public sealed class BulkEntityData
{
    [ProtoMember(1)] public int EntityId { get; set; }
    [ProtoMember(2)] public string EntityType { get; set; } = string.Empty;
    [ProtoMember(3)] public Guid OwnerClientId { get; set; }
    [ProtoMember(4)] public float X { get; set; }
    [ProtoMember(5)] public float Y { get; set; }
    [ProtoMember(6)] public float Z { get; set; }
    [ProtoMember(7)] public float RotationY { get; set; }
    [ProtoMember(8)] public Dictionary<string, byte[]> ComponentData { get; set; } = new();
}
