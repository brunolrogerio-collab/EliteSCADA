using Scada.Core.Tags;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoValueCodecTests
{
    [Fact]
    public void Int16_NormalAndByteSwap_AreSymmetric()
    {
        var normal = Point(TagDataType.Int16, S7IsoValueType.Int16);
        var swapped = Point(TagDataType.Int16, S7IsoValueType.Int16, S7IsoValueOrder.ByteSwap);

        Assert.Equal(new byte[] { 0x12, 0x34 }, S7IsoValueCodec.Encode(normal, (short)0x1234));
        Assert.Equal(new byte[] { 0x34, 0x12 }, S7IsoValueCodec.Encode(swapped, (short)0x1234));
        Assert.Equal((short)0x1234, S7IsoValueCodec.Decode(swapped, new byte[] { 0x34, 0x12 }));
    }

    [Fact]
    public void Int32_WordAndCombinedSwap_AreDeterministic()
    {
        var word = Point(TagDataType.Int32, S7IsoValueType.Int32, S7IsoValueOrder.WordSwap);
        var combined = Point(TagDataType.Int32, S7IsoValueType.Int32, S7IsoValueOrder.ByteAndWordSwap);

        Assert.Equal(new byte[] { 0x33, 0x44, 0x11, 0x22 }, S7IsoValueCodec.Encode(word, 0x11223344));
        Assert.Equal(new byte[] { 0x44, 0x33, 0x22, 0x11 }, S7IsoValueCodec.Encode(combined, 0x11223344));
        Assert.Equal(0x11223344, Assert.IsType<int>(S7IsoValueCodec.Decode(combined, new byte[] { 0x44, 0x33, 0x22, 0x11 })));
    }

    [Fact]
    public void Float32_RoundTripsNetworkOrder()
    {
        var point = Point(TagDataType.Float, S7IsoValueType.Float32);
        var encoded = S7IsoValueCodec.Encode(point, 12.5f);
        Assert.Equal(12.5f, Assert.IsType<float>(S7IsoValueCodec.Decode(point, encoded)));
    }

    [Fact]
    public void String_UsesSiemensMaximumAndCurrentLengthPrefix()
    {
        var point = new S7IsoPoint(
            Tag(TagDataType.String),
            S7IsoArea.DataBlock,
            20,
            S7IsoValueType.String,
            DbNumber: 1,
            StringLength: 10);

        var encoded = S7IsoValueCodec.Encode(point, "ABC");

        Assert.Equal(12, encoded.Length);
        Assert.Equal((byte)10, encoded[0]);
        Assert.Equal((byte)3, encoded[1]);
        Assert.Equal("ABC", S7IsoValueCodec.Decode(point, encoded));
    }

    [Fact]
    public void WString_UsesWordHeaderAndStrictUtf16BigEndian()
    {
        var point = new S7IsoPoint(
            Tag(TagDataType.String),
            S7IsoArea.DataBlock,
            40,
            S7IsoValueType.WString,
            DbNumber: 1,
            StringLength: 5);

        var encoded = S7IsoValueCodec.Encode(point, "AΩ");

        Assert.Equal(14, encoded.Length);
        Assert.Equal(
            new byte[] { 0x00, 0x05, 0x00, 0x02, 0x00, 0x41, 0x03, 0xA9, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
            encoded);
        Assert.Equal("AΩ", S7IsoValueCodec.Decode(point, encoded));
        Assert.Throws<ArgumentException>(() => S7IsoValueCodec.Encode(point, "\uD800"));
    }

    [Fact]
    public void DateTime_RoundTripsS7BcdLayout()
    {
        var point = new S7IsoPoint(
            Tag(TagDataType.DateTime),
            S7IsoArea.DataBlock,
            30,
            S7IsoValueType.DateTime,
            DbNumber: 1);
        var expected = new DateTime(2026, 8, 29, 14, 35, 42, 123, DateTimeKind.Unspecified);

        var encoded = S7IsoValueCodec.Encode(point, expected);
        var decoded = Assert.IsType<DateTime>(S7IsoValueCodec.Decode(point, encoded));

        Assert.Equal(new byte[] { 0x26, 0x08, 0x29, 0x14, 0x35, 0x42, 0x12, 0x37 }, encoded);
        Assert.Equal(expected, decoded);
    }

    [Fact]
    public void UInt32_DecodesIntoCanonicalInt64WithoutSignLoss()
    {
        var point = Point(TagDataType.Int64, S7IsoValueType.UInt32);

        var decoded = S7IsoValueCodec.Decode(point, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });

        Assert.Equal(4_294_967_295L, Assert.IsType<long>(decoded));
    }

    [Fact]
    public void UnsignedWrites_RejectNegativeAndOverflowInsteadOfTruncating()
    {
        var bytePoint = Point(TagDataType.Int16, S7IsoValueType.Byte);
        var uint16Point = Point(TagDataType.Int32, S7IsoValueType.UInt16);
        var uint32Point = Point(TagDataType.Int64, S7IsoValueType.UInt32);

        Assert.Equal(new byte[] { 0xFF }, S7IsoValueCodec.Encode(bytePoint, (short)255));
        Assert.Throws<OverflowException>(() => S7IsoValueCodec.Encode(bytePoint, (short)-1));
        Assert.Throws<OverflowException>(() => S7IsoValueCodec.Encode(bytePoint, 256));

        Assert.Equal(new byte[] { 0xFF, 0xFF }, S7IsoValueCodec.Encode(uint16Point, 65_535));
        Assert.Throws<OverflowException>(() => S7IsoValueCodec.Encode(uint16Point, -1));
        Assert.Throws<OverflowException>(() => S7IsoValueCodec.Encode(uint16Point, 65_536));

        Assert.Equal(
            new byte[] { 0xFF, 0xFF, 0xFF, 0xFF },
            S7IsoValueCodec.Encode(uint32Point, 4_294_967_295L));
        Assert.Throws<OverflowException>(() => S7IsoValueCodec.Encode(uint32Point, -1L));
        Assert.Throws<OverflowException>(() => S7IsoValueCodec.Encode(uint32Point, 4_294_967_296L));
    }

    private static S7IsoPoint Point(
        TagDataType tagType,
        S7IsoValueType valueType,
        S7IsoValueOrder order = S7IsoValueOrder.Normal) =>
        new(Tag(tagType), S7IsoArea.Merker, 0, valueType, ValueOrder: order);

    private static TagDefinition Tag(TagDataType type) => new(
        Guid.NewGuid(),
        "T",
        $"PLC.{Guid.NewGuid():N}",
        type,
        "s7",
        null,
        null,
        false);
}
