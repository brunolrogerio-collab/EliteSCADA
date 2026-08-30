using Scada.Core.Tags;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoValueCodecTests
{
    [Fact]
    public void Int16_NormalAndByteSwap_AreSymmetric()
    {
        var normal = Point(S7IsoValueType.Int16, TagDataType.Int16, order: S7IsoValueOrder.Normal);
        var swapped = normal with { ValueOrder = S7IsoValueOrder.ByteSwap };

        Assert.Equal(new byte[] { 0x12, 0x34 }, S7IsoValueCodec.Encode(normal, (short)0x1234));
        Assert.Equal(new byte[] { 0x34, 0x12 }, S7IsoValueCodec.Encode(swapped, (short)0x1234));
        Assert.Equal((short)0x1234, S7IsoValueCodec.Decode(swapped, new byte[] { 0x34, 0x12 }));
    }

    [Fact]
    public void Int32_WordAndCombinedSwap_AreDeterministic()
    {
        var tag = Tag(TagDataType.Int32);
        var wordSwap = new S7IsoPoint(tag, S7IsoArea.Merker, 0, S7IsoValueType.Int32, ValueOrder: S7IsoValueOrder.WordSwap);
        var combined = wordSwap with { ValueOrder = S7IsoValueOrder.ByteAndWordSwap };

        Assert.Equal(new byte[] { 0x33, 0x44, 0x11, 0x22 }, S7IsoValueCodec.Encode(wordSwap, 0x11223344));
        Assert.Equal(new byte[] { 0x44, 0x33, 0x22, 0x11 }, S7IsoValueCodec.Encode(combined, 0x11223344));
        Assert.Equal(0x11223344, S7IsoValueCodec.Decode(combined, new byte[] { 0x44, 0x33, 0x22, 0x11 }));
    }

    [Fact]
    public void Float32_RoundTripsNetworkOrder()
    {
        var point = Point(S7IsoValueType.Float32, TagDataType.Float);
        var encoded = S7IsoValueCodec.Encode(point, 12.5f);
        Assert.Equal(new byte[] { 0x41, 0x48, 0x00, 0x00 }, encoded);
        Assert.Equal(12.5f, S7IsoValueCodec.Decode(point, encoded));
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
        Assert.Equal(new byte[] { 10, 3, (byte)'A', (byte)'B', (byte)'C' }, encoded[..5]);
        Assert.Equal("ABC", S7IsoValueCodec.Decode(point, encoded));
        Assert.Throws<ArgumentException>(() => S7IsoValueCodec.Encode(point, "Ω"));
    }

    [Fact]
    public void String_DecodeRejectsPhysicalMaximumThatDiffersFromBinding()
    {
        var point = new S7IsoPoint(
            Tag(TagDataType.String),
            S7IsoArea.DataBlock,
            20,
            S7IsoValueType.String,
            DbNumber: 1,
            StringLength: 10);
        var physical = S7IsoValueCodec.Encode(point, "ABC");
        physical[0] = 8;

        Assert.Throws<FormatException>(() => S7IsoValueCodec.Decode(point, physical));
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
        Assert.ThrowsAny<ArgumentException>(() => S7IsoValueCodec.Encode(point, "\uD800"));
    }

    [Fact]
    public void WString_DecodeRejectsPhysicalMaximumThatDiffersFromBinding()
    {
        var point = new S7IsoPoint(
            Tag(TagDataType.String),
            S7IsoArea.DataBlock,
            40,
            S7IsoValueType.WString,
            DbNumber: 1,
            StringLength: 5);
        var physical = S7IsoValueCodec.Encode(point, "AΩ");
        physical[0] = 0x00;
        physical[1] = 0x04;

        Assert.Throws<FormatException>(() => S7IsoValueCodec.Decode(point, physical));
    }

    [Fact]
    public void DateTime_RoundTripsS7BcdLayout()
    {
        var point = Point(S7IsoValueType.DateTime, TagDataType.DateTime);
        var value = new DateTime(2026, 8, 29, 14, 35, 42, 123, DateTimeKind.Unspecified);

        var encoded = S7IsoValueCodec.Encode(point, value);
        Assert.Equal(new byte[] { 0x26, 0x08, 0x29, 0x14, 0x35, 0x42, 0x12, 0x37 }, encoded);
        Assert.Equal(value, S7IsoValueCodec.Decode(point, encoded));
    }

    [Fact]
    public void DateTime_DecodeRejectsInvalidMillisecondNibbleOrWeekday()
    {
        var point = Point(S7IsoValueType.DateTime, TagDataType.DateTime);

        Assert.Throws<FormatException>(() =>
            S7IsoValueCodec.Decode(point, new byte[] { 0x26, 0x08, 0x29, 0x14, 0x35, 0x42, 0x12, 0xA7 }));
        Assert.Throws<FormatException>(() =>
            S7IsoValueCodec.Decode(point, new byte[] { 0x26, 0x08, 0x29, 0x14, 0x35, 0x42, 0x12, 0x30 }));
    }

    [Fact]
    public void UInt32_DecodesIntoCanonicalInt64WithoutSignLoss()
    {
        var point = Point(S7IsoValueType.UInt32, TagDataType.Int64);
        var raw = new byte[] { 0xFF, 0xFF, 0xFF, 0xFE };
        Assert.Equal(4_294_967_294L, S7IsoValueCodec.Decode(point, raw));
        Assert.Equal(raw, S7IsoValueCodec.Encode(point, 4_294_967_294L));
    }

    [Fact]
    public void UnsignedWrites_RejectNegativeAndOverflowInsteadOfTruncating()
    {
        var bytePoint = Point(S7IsoValueType.Byte, TagDataType.Int16);
        var wordPoint = Point(S7IsoValueType.UInt16, TagDataType.Int32);
        var dwordPoint = Point(S7IsoValueType.UInt32, TagDataType.Int64);

        Assert.Throws<OverflowException>(() => S7IsoValueCodec.Encode(bytePoint, -1));
        Assert.Throws<OverflowException>(() => S7IsoValueCodec.Encode(bytePoint, 256));
        Assert.Throws<OverflowException>(() => S7IsoValueCodec.Encode(wordPoint, -1));
        Assert.Throws<OverflowException>(() => S7IsoValueCodec.Encode(wordPoint, 65_536));
        Assert.Throws<OverflowException>(() => S7IsoValueCodec.Encode(dwordPoint, -1L));
        Assert.Throws<OverflowException>(() => S7IsoValueCodec.Encode(dwordPoint, 4_294_967_296L));
    }

    private static S7IsoPoint Point(
        S7IsoValueType type,
        TagDataType tagType,
        S7IsoValueOrder order = S7IsoValueOrder.Normal) =>
        new(Tag(tagType), S7IsoArea.Merker, 0, type, ValueOrder: order);

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
