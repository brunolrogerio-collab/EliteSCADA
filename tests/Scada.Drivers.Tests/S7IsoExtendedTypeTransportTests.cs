using Scada.Core.Tags;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoExtendedTypeTransportTests
{
    [Fact]
    public async Task StringDateTimeInt64AndFloat64_ReadAndWriteThroughByteTransport()
    {
        await using var server = new TestS7IsoServer();
        var stringPoint = new S7IsoPoint(
            S7IsoTransportTests.Tag(TagDataType.String),
            S7IsoArea.DataBlock,
            0,
            S7IsoValueType.String,
            DbNumber: 2,
            Writable: true,
            StringLength: 20);
        var datePoint = new S7IsoPoint(
            S7IsoTransportTests.Tag(TagDataType.DateTime),
            S7IsoArea.DataBlock,
            40,
            S7IsoValueType.DateTime,
            DbNumber: 2,
            Writable: true);
        var int64Point = new S7IsoPoint(
            S7IsoTransportTests.Tag(TagDataType.Int64),
            S7IsoArea.DataBlock,
            60,
            S7IsoValueType.Int64,
            DbNumber: 2,
            Writable: true);
        var float64Point = new S7IsoPoint(
            S7IsoTransportTests.Tag(TagDataType.Double),
            S7IsoArea.DataBlock,
            80,
            S7IsoValueType.Float64,
            DbNumber: 2,
            Writable: true);

        var initialDate = new DateTime(2026, 8, 29, 16, 45, 12, 345, DateTimeKind.Unspecified);
        server.SetBytes(S7IsoArea.DataBlock, 2, 0, S7IsoValueCodec.Encode(stringPoint, "PUMP-A"));
        server.SetBytes(S7IsoArea.DataBlock, 2, 40, S7IsoValueCodec.Encode(datePoint, initialDate));
        server.SetBytes(S7IsoArea.DataBlock, 2, 60, S7IsoValueCodec.Encode(int64Point, 0x0102030405060708L));
        server.SetBytes(S7IsoArea.DataBlock, 2, 80, S7IsoValueCodec.Encode(float64Point, 1234.5d));

        await using var transport = new S7IsoTransport(S7IsoTransportTests.Options(server.Port));
        var points = new[] { stringPoint, datePoint, int64Point, float64Point };
        var results = await transport.ReadAsync(points);

        Assert.Equal(4, results.Count);
        Assert.Equal("PUMP-A", Assert.IsType<string>(S7IsoValueCodec.Decode(stringPoint, results[0].Data!)));
        Assert.Equal(initialDate, Assert.IsType<DateTime>(S7IsoValueCodec.Decode(datePoint, results[1].Data!)));
        Assert.Equal(0x0102030405060708L, Assert.IsType<long>(S7IsoValueCodec.Decode(int64Point, results[2].Data!)));
        Assert.Equal(1234.5d, Assert.IsType<double>(S7IsoValueCodec.Decode(float64Point, results[3].Data!)));

        var nextDate = new DateTime(2030, 1, 2, 3, 4, 5, 678, DateTimeKind.Unspecified);
        await transport.WriteAsync(stringPoint, S7IsoValueCodec.Encode(stringPoint, "PUMP-B"));
        await transport.WriteAsync(datePoint, S7IsoValueCodec.Encode(datePoint, nextDate));
        await transport.WriteAsync(int64Point, S7IsoValueCodec.Encode(int64Point, -1234567890123456789L));
        await transport.WriteAsync(float64Point, S7IsoValueCodec.Encode(float64Point, -0.125d));

        Assert.Equal(
            S7IsoValueCodec.Encode(stringPoint, "PUMP-B"),
            server.GetBytes(S7IsoArea.DataBlock, 2, 0, stringPoint.ByteLength));
        Assert.Equal(
            S7IsoValueCodec.Encode(datePoint, nextDate),
            server.GetBytes(S7IsoArea.DataBlock, 2, 40, datePoint.ByteLength));
        Assert.Equal(
            S7IsoValueCodec.Encode(int64Point, -1234567890123456789L),
            server.GetBytes(S7IsoArea.DataBlock, 2, 60, int64Point.ByteLength));
        Assert.Equal(
            S7IsoValueCodec.Encode(float64Point, -0.125d),
            server.GetBytes(S7IsoArea.DataBlock, 2, 80, float64Point.ByteLength));

        var diagnostics = transport.GetDiagnostics();
        Assert.Equal(5L, diagnostics.RequestAttempts);
        Assert.True(diagnostics.Connected);
    }
}
