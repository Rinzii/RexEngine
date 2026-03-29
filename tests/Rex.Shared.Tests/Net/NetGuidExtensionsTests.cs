using LiteNetLib.Utils;
using Rex.Shared.Net;

namespace Rex.Shared.Tests.Net;

public sealed class NetGuidExtensionsTests
{
    public static TheoryData<Guid> Guids => new()
    {
        Guid.Empty,
        Guid.Parse("a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11"),
        Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
    };

    [Theory]
    [MemberData(nameof(Guids))]
    public void PutGuid_and_ReadGuid_round_trip(Guid value)
    {
        var writer = new NetDataWriter();
        writer.PutGuid(value);

        var reader = new NetDataReader();
        reader.SetSource(writer.Data, 0, writer.Length);

        var read = reader.ReadGuid();

        Assert.Equal(value, read);
    }
}
