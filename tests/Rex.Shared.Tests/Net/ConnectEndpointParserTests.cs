using Rex.Shared.Net;

namespace Rex.Shared.Tests.Net;

public sealed class ConnectEndpointParserTests
{
    public static TheoryData<string?, int, string?, int> Cases => new()
    {
        { null, 27015, "127.0.0.1", 27015 },
        { "   ", 27015, "127.0.0.1", 27015 },
        { "myhost", 7777, "myhost", 7777 },
        { "192.168.0.1", 27015, "192.168.0.1", 27015 },
        { "192.168.0.1:27016", 27015, "192.168.0.1", 27016 },
        { "myhost:1", 27015, "myhost", 1 },
        { "myhost:65535", 27015, "myhost", 65535 },
        { "[::1]", 27015, "::1", 27015 },
        { "[::1]:27016", 27015, "::1", 27016 },
        { "fe80::1", 27015, "fe80::1", 27015 },
        { "fe80::1%eth0", 27015, "fe80::1%eth0", 27015 },
        { "[z]", 27015, "z", 27015 },
        { ":1234", 27015, ":1234", 27015 },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void TryParse_success_cases_match_expected(
        string? input,
        int defaultPort,
        string? expectedHost,
        int expectedPort)
    {
        var ok = ConnectEndpointParser.TryParse(input, defaultPort, out var host, out var port);

        Assert.True(ok);
        Assert.Equal(expectedHost, host);
        Assert.Equal(expectedPort, port);
    }

    [Theory]
    [InlineData("[")]
    [InlineData("[]")]
    [InlineData("[::1")]
    [InlineData("[::1]x")]
    [InlineData("[::1]:abc")]
    [InlineData("[::1]:0")]
    [InlineData("[::1]:65536")]
    public void TryParse_invalid_inputs_return_false(string input)
    {
        var ok = ConnectEndpointParser.TryParse(input, 27015, out _, out _);

        Assert.False(ok);
    }
}
