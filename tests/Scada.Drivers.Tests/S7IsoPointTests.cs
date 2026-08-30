using Scada.Core.Tags;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoPointTests
{
    [Fact]
    public void DbBoolean_UsesBitAddressAfterCanonicalByteBoundary()
    {
        var point = new S7IsoPoint(
            Tag(TagDataType.Boolean),
            S7IsoArea.DataBlock,
            ByteOffset: 10,
            ValueType: S7IsoValueType.Boolean,
            DbNumber: 7,
            BitOffset: 3);

        point.Validate();

        Assert.Equal(83, point.AddressInBits);
        Assert.Equal((byte)0x01, point.S7AnyTransportSize);
        Assert.Equal((ushort)1, point.S7AnyElementCount);
    }

    [Fact]
    public void NonDbArea_RejectsDbNumber()
    {
        var point = new S7IsoPoint(
            Tag(TagDataType.Int16),
            S7IsoArea.Merker,
            0,
            S7IsoValueType.Int16,
            DbNumber: 1);

        Assert.Throws<ArgumentException>(point.Validate);
    }

    [Fact]
    public void InputArea_RejectsWritableBinding()
    {
        var point = new S7IsoPoint(
            Tag(TagDataType.Int16),
            S7IsoArea.Input,
            0,
            S7IsoValueType.Int16,
            Writable: true);

        Assert.Throws<ArgumentException>(point.Validate);
    }

    [Fact]
    public void WordSwap_RejectsSixteenBitValue()
    {
        var point = new S7IsoPoint(
            Tag(TagDataType.Int16),
            S7IsoArea.Merker,
            0,
            S7IsoValueType.Int16,
            ValueOrder: S7IsoValueOrder.WordSwap);

        Assert.Throws<ArgumentException>(point.Validate);
    }

    [Fact]
    public void WString_UsesByteTransportAndRejectsOrderingOrOversizedFirstCutLength()
    {
        var valid = new S7IsoPoint(
            Tag(TagDataType.String),
            S7IsoArea.DataBlock,
            20,
            S7IsoValueType.WString,
            DbNumber: 2,
            StringLength: 254);
        valid.Validate();

        Assert.Equal(512, valid.ByteLength);
        Assert.Equal((byte)0x02, valid.S7AnyTransportSize);
        Assert.Equal((ushort)512, valid.S7AnyElementCount);

        var ordered = valid with { ValueOrder = S7IsoValueOrder.ByteSwap };
        var oversized = valid with { StringLength = 255 };
        Assert.Throws<ArgumentException>(ordered.Validate);
        Assert.Throws<ArgumentOutOfRangeException>(oversized.Validate);
    }

    [Fact]
    public void UnsignedTypes_MapIntoWiderCanonicalTagTypes()
    {
        new S7IsoPoint(Tag(TagDataType.Int32), S7IsoArea.Merker, 0, S7IsoValueType.UInt16).Validate();
        new S7IsoPoint(Tag(TagDataType.Int64), S7IsoArea.Merker, 0, S7IsoValueType.UInt32).Validate();
    }

    [Fact]
    public void UndefinedProtocolEnums_AreRejectedAtPointValidation()
    {
        var invalidArea = new S7IsoPoint(
            Tag(TagDataType.Int16),
            (S7IsoArea)0xFF,
            0,
            S7IsoValueType.Int16);
        var invalidType = new S7IsoPoint(
            Tag(TagDataType.Int16),
            S7IsoArea.Merker,
            0,
            (S7IsoValueType)999);
        var invalidOrder = new S7IsoPoint(
            Tag(TagDataType.Int16),
            S7IsoArea.Merker,
            0,
            S7IsoValueType.Int16,
            ValueOrder: (S7IsoValueOrder)999);

        Assert.Throws<ArgumentOutOfRangeException>(invalidArea.Validate);
        Assert.Throws<ArgumentOutOfRangeException>(invalidType.Validate);
        Assert.Throws<ArgumentOutOfRangeException>(invalidOrder.Validate);
    }

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
