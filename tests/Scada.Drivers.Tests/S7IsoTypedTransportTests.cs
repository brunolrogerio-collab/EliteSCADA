using Scada.Core.Tags;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoTypedTransportTests
{
    [Fact]
    public async Task DInt_WriteAndReadRoundTripThroughTypedTransport()
    {
        await using var server = new TestS7IsoServer();
        var point = new S7IsoPoint(
            S7IsoTransportTests.Tag(TagDataType.Int32),
            S7IsoArea.DataBlock,
            20,
            S7IsoValueType.Int32,
            DbNumber: 1,
            Writable: true);
        await using var transport = new S7IsoTransport(S7IsoTransportTests.Options(server.Port));

        await transport.WriteAsync(point, S7IsoValueCodec.Encode(point, 0x11223344));
        var read = Assert.Single(await transport.ReadAsync(new[] { point }));

        Assert.Equal(new byte[] { 0x11, 0x22, 0x33, 0x44 }, server.GetBytes(S7IsoArea.DataBlock, 1, 20, 4));
        Assert.True(read.Succeeded);
        Assert.Equal(0x11223344, Assert.IsType<int>(S7IsoValueCodec.Decode(point, read.Data!)));
    }

    [Fact]
    public async Task Real_WriteAndReadRoundTripThroughTypedTransport()
    {
        await using var server = new TestS7IsoServer();
        var point = new S7IsoPoint(
            S7IsoTransportTests.Tag(TagDataType.Float),
            S7IsoArea.DataBlock,
            40,
            S7IsoValueType.Float32,
            DbNumber: 1,
            Writable: true);
        await using var transport = new S7IsoTransport(S7IsoTransportTests.Options(server.Port));

        await transport.WriteAsync(point, S7IsoValueCodec.Encode(point, 12.5f));
        var read = Assert.Single(await transport.ReadAsync(new[] { point }));

        Assert.Equal(new byte[] { 0x41, 0x48, 0x00, 0x00 }, server.GetBytes(S7IsoArea.DataBlock, 1, 40, 4));
        Assert.True(read.Succeeded);
        Assert.Equal(12.5f, Assert.IsType<float>(S7IsoValueCodec.Decode(point, read.Data!)));
    }
}