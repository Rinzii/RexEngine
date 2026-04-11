using ProtoBuf;
using Rex.Shared.Net.Transfer;

namespace Rex.Shared.Tests.Net.Transfer;

// Protobuf net helpers for generic transfer payloads from the engine.
public sealed class ProtoSerializerTests
{
    [Fact]
    // Nested records and maps round trip through bytes.
    public void Serialize_and_Deserialize_array_round_trip()
    {
        var original = new SamplePayload
        {
            Name = "test-payload",
            Revision = 4,
            Chunks =
            [
                new SampleChunk { Index = 0, Label = "chunk-0", Flags = 2 }
            ],
            Metadata = new Dictionary<string, string> { ["mode"] = "generic" }
        };

        byte[] bytes = ProtoSerializer.Serialize(original);
        SamplePayload copy = ProtoSerializer.Deserialize<SamplePayload>(bytes);

        Assert.Equal(original.Name, copy.Name);
        Assert.Equal(original.Revision, copy.Revision);
        _ = Assert.Single(copy.Chunks);
        Assert.Equal(original.Chunks[0].Label, copy.Chunks[0].Label);
        Assert.Equal("generic", copy.Metadata["mode"]);
    }

    [Fact]
    // ReadOnlyMemory overload matches byte array Deserialize.
    public void Deserialize_ReadOnlyMemory_matches_array_overload()
    {
        var manifest = new SampleManifest
        {
            Version = 3,
            Entries =
            [
                new SampleEntry { Path = "/a/b", Size = 99, Hash = "abc" }
            ]
        };
        byte[] bytes = ProtoSerializer.Serialize(manifest);
        SampleManifest fromMemory = ProtoSerializer.Deserialize<SampleManifest>(bytes.AsMemory());
        Assert.Equal(manifest.Version, fromMemory.Version);
        _ = Assert.Single(fromMemory.Entries);
        Assert.Equal("/a/b", fromMemory.Entries[0].Path);
    }

    [Fact]
    // Payloads with string maps round trip through protobuf.
    public void SettingsPayload_round_trip()
    {
        var original = new SettingsPayload
        {
            DisplayName = "test",
            Interval = 45,
            Properties = new Dictionary<string, string> { ["feature"] = "off" }
        };

        SettingsPayload copy = ProtoSerializer.Deserialize<SettingsPayload>(ProtoSerializer.Serialize(original));

        Assert.Equal(original.DisplayName, copy.DisplayName);
        Assert.Equal(original.Interval, copy.Interval);
        Assert.Equal("off", copy.Properties["feature"]);
    }

    [Fact]
    // Nested byte blobs round trip through protobuf.
    public void BlobCollection_round_trip()
    {
        var owner = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var original = new BlobCollection
        {
            Revision = 100u,
            Blobs =
            [
                new BlobEntry
                {
                    EntryId = 3,
                    OwnerId = owner,
                    Data = new Dictionary<string, byte[]> { ["a"] = [1, 2, 3] }
                }
            ]
        };

        BlobCollection copy = ProtoSerializer.Deserialize<BlobCollection>(ProtoSerializer.Serialize(original));

        Assert.Equal(original.Revision, copy.Revision);
        _ = Assert.Single(copy.Blobs);
        Assert.Equal(3, copy.Blobs[0].EntryId);
        Assert.Equal(owner, copy.Blobs[0].OwnerId);
        Assert.Equal(new byte[] { 1, 2, 3 }, copy.Blobs[0].Data["a"]);
    }

    [ProtoContract]
    private sealed class SamplePayload
    {
        [ProtoMember(1)] public string Name { get; set; } = string.Empty;
        [ProtoMember(2)] public int Revision { get; set; }
        [ProtoMember(3)] public List<SampleChunk> Chunks { get; set; } = [];
        [ProtoMember(4)] public Dictionary<string, string> Metadata { get; set; } = [];
    }

    [ProtoContract]
    private sealed class SampleChunk
    {
        [ProtoMember(1)] public int Index { get; set; }
        [ProtoMember(2)] public string Label { get; set; } = string.Empty;
        [ProtoMember(3)] public byte Flags { get; set; }
    }

    [ProtoContract]
    private sealed class SampleManifest
    {
        [ProtoMember(1)] public int Version { get; set; }
        [ProtoMember(2)] public List<SampleEntry> Entries { get; set; } = [];
    }

    [ProtoContract]
    private sealed class SampleEntry
    {
        [ProtoMember(1)] public string Path { get; set; } = string.Empty;
        [ProtoMember(2)] public long Size { get; set; }
        [ProtoMember(3)] public string Hash { get; set; } = string.Empty;
    }

    [ProtoContract]
    private sealed class SettingsPayload
    {
        [ProtoMember(1)] public string DisplayName { get; set; } = string.Empty;
        [ProtoMember(2)] public int Interval { get; set; }
        [ProtoMember(3)] public Dictionary<string, string> Properties { get; set; } = [];
    }

    [ProtoContract]
    private sealed class BlobCollection
    {
        [ProtoMember(1)] public uint Revision { get; set; }
        [ProtoMember(2)] public List<BlobEntry> Blobs { get; set; } = [];
    }

    [ProtoContract]
    private sealed class BlobEntry
    {
        [ProtoMember(1)] public int EntryId { get; set; }
        [ProtoMember(2)] public Guid OwnerId { get; set; }
        [ProtoMember(3)] public Dictionary<string, byte[]> Data { get; set; } = [];
    }
}
