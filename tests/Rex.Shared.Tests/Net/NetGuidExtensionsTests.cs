using LiteNetLib.Utils;
using Rex.Shared.Net;

namespace Rex.Shared.Tests.Net;

// Round trip for PutGuid and ReadGuid on LiteNetLib buffers.
public sealed class NetGuidExtensionsTests
{
    public static TheoryData<Guid> Guids =>
    [
        Guid.Empty,
        Guid.Parse("a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11"),
        Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
    ];

    [Theory]
    [MemberData(nameof(Guids))]
    // Writes then reads the same Guid value.
    public void PutGuid_and_ReadGuid_round_trip(Guid value)
    {
        var writer = new NetDataWriter();
        writer.PutGuid(value);

        var reader = new NetDataReader();
        reader.SetSource(writer.Data, 0, writer.Length);

        Guid read = reader.ReadGuid();

        Assert.Equal(value, read);
    }
}
