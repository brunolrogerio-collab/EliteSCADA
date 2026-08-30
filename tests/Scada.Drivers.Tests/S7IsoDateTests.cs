using Scada.Core.Tags;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoDateTests
{
    [Fact]
    public void Date_UsesWordTransportAndCanonicalDateTime()
    {
        var point = Point();

        point.Validate();

        Assert.Equal(13, (int)S7IsoValueType.Date);
        Assert.Equal(2, point.ByteLength);
        Assert.Equal((byte)0x04, point.S7AnyTransportSize);
        Assert.Equal((ushort)1, point.S7AnyElementCount);
        Assert.Equal(new DateTime(1990, 1, 1), Assert.IsType<DateTime>(
            S7IsoValueCodec.Decode(point, new byte[] { 0x00, 0x00 })));
    }

    [Fact]
    public void Date_CodecPinsEpochCurrentAndMaximumBoundaries()
    {
        var point = Point();
        var current = new DateTime(2026, 8, 29);
        var maximum = new DateTime(2169, 6, 6);

        Assert.Equal(new byte[] { 0x00, 0x00 }, S7IsoValueCodec.Encode(point, new DateTime(1990, 1, 1)));
        Assert.Equal(new byte[] { 0x34, 0x4D }, S7IsoValueCodec.Encode(point, current));
        Assert.Equal(current, Assert.IsType<DateTime>(S7IsoValueCodec.Decode(point, new byte[] { 0x34, 0x4D })));
        Assert.Equal(new byte[] { 0xFF, 0xFF }, S7IsoValueCodec.Encode(point, maximum));
        Assert.Equal(maximum, Assert.IsType<DateTime>(S7IsoValueCodec.Decode(point, new byte[] { 0xFF, 0xFF })));
    }

    [Fact]
    public void Date_WriteRejectsLossyOrOutOfRangeValues()
    {
        var point = Point();

        Assert.Throws<ArgumentException>(() =>
            S7IsoValueCodec.Encode(point, new DateTime(2026, 8, 29, 0, 0, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            S7IsoValueCodec.Encode(point, new DateTime(1989, 12, 31)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            S7IsoValueCodec.Encode(point, new DateTime(2169, 6, 7)));
    }

    [Fact]
    public void Date_RejectsOrderingAndPortableBindingRoundTrips()
    {
        var ordered = Point() with { ValueOrder = S7IsoValueOrder.ByteSwap };
        Assert.Throws<ArgumentException>(ordered.Validate);

        var binding = new S7IsoTagBinding(
            S7IsoTagBinding.CurrentSchemaVersion,
            S7IsoArea.DataBlock,
            ByteOffset: 20,
            S7IsoValueType.Date,
            DbNumber: 4,
            Writable: true);

        var portable = binding.ToPortableAddress();
        Assert.True(S7IsoTagBinding.TryParsePortableAddress(portable, out var parsed, out var error), error);
        Assert.NotNull(parsed);
        Assert.Equal(binding, parsed);
        parsed!.ToPoint(S7IsoTransportTests.Tag(TagDataType.DateTime)).Validate();
    }

    [Fact]
    public async Task Date_TransportRoundTripUsesClassicWordRepresentation()
    {
        await using var server = new TestS7IsoServer();
        server.SetBytes(S7IsoArea.DataBlock, 4, 20, new byte[] { 0x34, 0x4D });
        var point = new S7IsoPoint(
            S7IsoTransportTests.Tag(TagDataType.DateTime),
            S7IsoArea.DataBlock,
            20,
            S7IsoValueType.Date,
            DbNumber: 4,
            Writable: true);
        await using var transport = new S7IsoTransport(S7IsoTransportTests.Options(server.Port));

        var initial = Assert.Single(await transport.ReadAsync(new[] { point }));
        Assert.True(initial.Succeeded);
        Assert.Equal(new DateTime(2026, 8, 29), Assert.IsType<DateTime>(S7IsoValueCodec.Decode(point, initial.Data!)));

        var target = new DateTime(2030, 1, 2);
        var encoded = S7IsoValueCodec.Encode(point, target);
        await transport.WriteAsync(point, encoded);

        Assert.Equal(encoded, server.GetBytes(S7IsoArea.DataBlock, 4, 20, 2));
        var reread = Assert.Single(await transport.ReadAsync(new[] { point }));
        Assert.Equal(target, Assert.IsType<DateTime>(S7IsoValueCodec.Decode(point, reread.Data!)));
    }

    private static S7IsoPoint Point() => new(
        S7IsoTransportTests.Tag(TagDataType.DateTime),
        S7IsoArea.Merker,
        0,
        S7IsoValueType.Date);
}
