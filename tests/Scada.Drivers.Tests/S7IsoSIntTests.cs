using Scada.Core.Tags;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoSIntTests
{
    [Fact]
    public void SInt_UsesOneByteRawTransportAndCanonicalInt16()
    {
        var point = new S7IsoPoint(
            S7IsoTransportTests.Tag(TagDataType.Int16),
            S7IsoArea.Merker,
            0,
            S7IsoValueType.SInt);

        point.Validate();

        Assert.Equal(12, (int)S7IsoValueType.SInt);
        Assert.Equal(1, point.ByteLength);
        Assert.Equal((byte)0x02, point.S7AnyTransportSize);
        Assert.Equal((ushort)1, point.S7AnyElementCount);
        Assert.Equal((short)-128, Assert.IsType<short>(S7IsoValueCodec.Decode(point, new byte[] { 0x80 })));
        Assert.Equal((short)-1, Assert.IsType<short>(S7IsoValueCodec.Decode(point, new byte[] { 0xFF })));
        Assert.Equal(new byte[] { 0x80 }, S7IsoValueCodec.Encode(point, (short)-128));
        Assert.Equal(new byte[] { 0x7F }, S7IsoValueCodec.Encode(point, (short)127));
        Assert.Throws<OverflowException>(() => S7IsoValueCodec.Encode(point, -129));
        Assert.Throws<OverflowException>(() => S7IsoValueCodec.Encode(point, 128));
    }

    [Fact]
    public void SInt_RejectsByteOrderingBecauseItIsSingleByte()
    {
        var point = new S7IsoPoint(
            S7IsoTransportTests.Tag(TagDataType.Int16),
            S7IsoArea.Merker,
            0,
            S7IsoValueType.SInt,
            ValueOrder: S7IsoValueOrder.ByteSwap);

        Assert.Throws<ArgumentException>(point.Validate);
    }

    [Fact]
    public void SInt_PortableBindingRoundTripsWithoutChangingSchemaVersion()
    {
        var binding = new S7IsoTagBinding(
            S7IsoTagBinding.CurrentSchemaVersion,
            S7IsoArea.DataBlock,
            ByteOffset: 9,
            S7IsoValueType.SInt,
            DbNumber: 3,
            Writable: true);

        var portable = binding.ToPortableAddress();
        var parsed = S7IsoTagBinding.TryParsePortableAddress(portable, out var roundTrip, out var error);

        Assert.True(parsed, error);
        Assert.NotNull(roundTrip);
        Assert.Equal(S7IsoTagBinding.CurrentSchemaVersion, roundTrip!.SchemaVersion);
        Assert.Equal(S7IsoValueType.SInt, roundTrip.ValueType);
        Assert.Equal(binding, roundTrip);
        roundTrip.ToPoint(S7IsoTransportTests.Tag(TagDataType.Int16)).Validate();
    }

    [Fact]
    public async Task SInt_TransportRoundTripPreservesSignedValue()
    {
        await using var server = new TestS7IsoServer();
        server.SetBytes(S7IsoArea.Merker, 0, 10, new byte[] { 0xFE });
        var point = new S7IsoPoint(
            S7IsoTransportTests.Tag(TagDataType.Int16),
            S7IsoArea.Merker,
            10,
            S7IsoValueType.SInt,
            Writable: true);
        await using var transport = new S7IsoTransport(S7IsoTransportTests.Options(server.Port));

        var read = Assert.Single(await transport.ReadAsync(new[] { point }));
        Assert.True(read.Succeeded);
        Assert.Equal((short)-2, Assert.IsType<short>(S7IsoValueCodec.Decode(point, read.Data!)));

        await transport.WriteAsync(point, S7IsoValueCodec.Encode(point, (short)-100));

        Assert.Equal(new byte[] { 0x9C }, server.GetBytes(S7IsoArea.Merker, 0, 10, 1));
        var reread = Assert.Single(await transport.ReadAsync(new[] { point }));
        Assert.Equal((short)-100, Assert.IsType<short>(S7IsoValueCodec.Decode(point, reread.Data!)));
    }
}
