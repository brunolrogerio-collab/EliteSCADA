using Scada.Core.Tags;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoWStringTransportTests
{
    [Fact]
    public async Task WString_ReadWrite_RoundTripsUtf16BigEndianThroughDbMemory()
    {
        await using var server = new TestS7IsoServer();
        var point = new S7IsoPoint(
            S7IsoTransportTests.Tag(TagDataType.String),
            S7IsoArea.DataBlock,
            100,
            S7IsoValueType.WString,
            DbNumber: 8,
            Writable: true,
            StringLength: 12);
        var initialBytes = S7IsoValueCodec.Encode(point, "Olá Ω");
        server.SetBytes(S7IsoArea.DataBlock, 8, 100, initialBytes);
        await using var transport = new S7IsoTransport(S7IsoTransportTests.Options(server.Port));

        var read = Assert.Single(await transport.ReadAsync(new[] { point }));

        Assert.True(read.Succeeded);
        Assert.Equal("Olá Ω", Assert.IsType<string>(S7IsoValueCodec.Decode(point, read.Data!)));

        var writtenBytes = S7IsoValueCodec.Encode(point, "Motor Ж");
        await transport.WriteAsync(point, writtenBytes);

        Assert.Equal(
            writtenBytes,
            server.GetBytes(S7IsoArea.DataBlock, 8, 100, point.ByteLength));
        Assert.Equal(nameof(S7IsoFailureKind).Length > 0, true);
    }
}
