using ProtoBuf;

namespace Rex.Shared.Net.Transfer;

/// <summary>
/// Identifies the kind of payload carried by a bulk transfer.
/// </summary>
public enum BulkDataType : byte
{
    /// <summary>
    /// Full map data used for loading a level.
    /// </summary>
    MapData = 1,

    /// <summary>
    /// Asset list and metadata.
    /// </summary>
    AssetManifest = 2,

    /// <summary>
    /// Server-side gameplay settings.
    /// </summary>
    ServerConfig = 3,

    /// <summary>
    /// Large entity state payload.
    /// </summary>
    EntityBulkState = 4,

    /// <summary>
    /// Raw file data.
    /// </summary>
    ResourceFile = 5
}

/// <summary>
/// Bulk payload for a full map load.
/// </summary>
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

/// <summary>
/// One tile entry inside <see cref="MapData"/>.
/// </summary>
[ProtoContract]
public sealed class MapTile
{
    [ProtoMember(1)] public int X { get; set; }
    [ProtoMember(2)] public int Y { get; set; }
    [ProtoMember(3)] public int TileId { get; set; }
    [ProtoMember(4)] public byte Flags { get; set; }
}

/// <summary>
/// One entity entry inside <see cref="MapData"/>.
/// </summary>
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

/// <summary>
/// Bulk payload that lists the assets a client may need.
/// </summary>
[ProtoContract]
public sealed class AssetManifest
{
    [ProtoMember(1)] public int Version { get; set; }
    [ProtoMember(2)] public List<AssetEntry> Assets { get; set; } = new();
}

/// <summary>
/// One asset entry inside <see cref="AssetManifest"/>.
/// </summary>
[ProtoContract]
public sealed class AssetEntry
{
    [ProtoMember(1)] public string Path { get; set; } = string.Empty;
    [ProtoMember(2)] public long Size { get; set; }
    [ProtoMember(3)] public string Hash { get; set; } = string.Empty;
}

/// <summary>
/// Bulk payload for server configuration values.
/// </summary>
[ProtoContract]
public sealed class ServerConfigData
{
    [ProtoMember(1)] public string ServerName { get; set; } = string.Empty;
    [ProtoMember(2)] public int TickRate { get; set; }
    [ProtoMember(3)] public int MaxPlayers { get; set; }
    [ProtoMember(4)] public string MapName { get; set; } = string.Empty;
    [ProtoMember(5)] public Dictionary<string, string> CVars { get; set; } = new();
}

/// <summary>
/// Bulk payload for a large set of entity states.
/// </summary>
[ProtoContract]
public sealed class EntityBulkState
{
    [ProtoMember(1)] public uint ServerTick { get; set; }
    [ProtoMember(2)] public List<BulkEntityData> Entities { get; set; } = new();
}

/// <summary>
/// One entity entry inside <see cref="EntityBulkState"/>.
/// </summary>
[ProtoContract]
public sealed class BulkEntityData
{
    [ProtoMember(1)] public int EntityId { get; set; }
    [ProtoMember(2)] public string EntityType { get; set; } = string.Empty;
    [ProtoMember(3)] public int OwnerClientId { get; set; }
    [ProtoMember(4)] public float X { get; set; }
    [ProtoMember(5)] public float Y { get; set; }
    [ProtoMember(6)] public float Z { get; set; }
    [ProtoMember(7)] public float RotationY { get; set; }
    [ProtoMember(8)] public Dictionary<string, byte[]> ComponentData { get; set; } = new();
}
