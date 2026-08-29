using Scada.Core.Tags;

namespace Scada.Core.Tests;

public sealed class TagBitSemanticsTests
{
    [Theory]
    [InlineData((short)0x0001, 0, true)]
    [InlineData((short)0x0020, 5, true)]
    [InlineData((short)0x0020, 4, false)]
    [InlineData(short.MinValue, 15, true)]
    [InlineData((short)-1, 15, true)]
    public void Project_Int16_UsesFixedWidthTwosComplement(short rawValue, int bitIndex, bool expected)
    {
        var tag = TagDefinition.Create("Word16", "Plant.Word16", TagDataType.Int16);
        var reference = BitReference(tag, bitIndex);
        var source = new TagValue(tag.Id, rawValue, DateTimeOffset.UtcNow, TagQuality.Good, "test");

        var success = TagBitSemantics.TryProject(tag, reference, source, out var projected, out var error);

        Assert.True(success, error);
        Assert.NotNull(projected);
        Assert.Equal(expected, Assert.IsType<bool>(projected!.Value));
    }

    [Theory]
    [InlineData(0x00000001, 0, true)]
    [InlineData(0x00400000, 22, true)]
    [InlineData(int.MinValue, 31, true)]
    [InlineData(-1, 30, true)]
    public void Project_Int32_UsesFixedWidthTwosComplement(int rawValue, int bitIndex, bool expected)
    {
        var tag = TagDefinition.Create("Word32", "Plant.Word32", TagDataType.Int32);
        var reference = BitReference(tag, bitIndex);
        var source = new TagValue(tag.Id, rawValue, DateTimeOffset.UtcNow, TagQuality.Good);

        var success = TagBitSemantics.TryProject(tag, reference, source, out var projected, out var error);

        Assert.True(success, error);
        Assert.Equal(expected, Assert.IsType<bool>(projected!.Value));
    }

    [Theory]
    [InlineData(1L, 0, true)]
    [InlineData(1L << 42, 42, true)]
    [InlineData(long.MinValue, 63, true)]
    [InlineData(-1L, 62, true)]
    public void Project_Int64_UsesFixedWidthTwosComplement(long rawValue, int bitIndex, bool expected)
    {
        var tag = TagDefinition.Create("Word64", "Plant.Word64", TagDataType.Int64);
        var reference = BitReference(tag, bitIndex);
        var source = new TagValue(tag.Id, rawValue, DateTimeOffset.UtcNow, TagQuality.Good);

        var success = TagBitSemantics.TryProject(tag, reference, source, out var projected, out var error);

        Assert.True(success, error);
        Assert.Equal(expected, Assert.IsType<bool>(projected!.Value));
    }

    [Fact]
    public void Project_PreservesAuthoritativeSampleContext()
    {
        var tag = TagDefinition.Create("Status", "Plant.Status", TagDataType.Int32);
        var reference = BitReference(tag, 7);
        var timestamp = DateTimeOffset.Parse("2026-08-29T10:15:00+00:00");
        var sourceTimestamp = timestamp.AddMilliseconds(-20);
        var serverTimestamp = timestamp.AddMilliseconds(-10);
        var source = new TagValue(tag.Id, 1 << 7, timestamp, TagQuality.Uncertain, "plc-a")
        {
            SourceTimestamp = sourceTimestamp,
            ServerTimestamp = serverTimestamp
        };

        var success = TagBitSemantics.TryProject(tag, reference, source, out var projected, out var error);

        Assert.True(success, error);
        Assert.NotNull(projected);
        Assert.True(Assert.IsType<bool>(projected!.Value));
        Assert.Equal(tag.Id, projected.TagId);
        Assert.Equal(timestamp, projected.Timestamp);
        Assert.Equal(TagQuality.Uncertain, projected.Quality);
        Assert.Equal("plc-a", projected.Source);
        Assert.Equal(sourceTimestamp, projected.SourceTimestamp);
        Assert.Equal(serverTimestamp, projected.ServerTimestamp);
    }

    [Fact]
    public void Project_BadSourceWithoutValue_DoesNotInventFalse()
    {
        var tag = TagDefinition.Create("Status", "Plant.Status", TagDataType.Int16);
        var reference = BitReference(tag, 3);
        var source = new TagValue(tag.Id, null, DateTimeOffset.UtcNow, TagQuality.BadCommunication, "plc-a");

        var success = TagBitSemantics.TryProject(tag, reference, source, out var projected, out var error);

        Assert.True(success, error);
        Assert.NotNull(projected);
        Assert.Null(projected!.Value);
        Assert.Equal(TagQuality.BadCommunication, projected.Quality);
    }

    [Theory]
    [InlineData(TagDataType.Int16, -1)]
    [InlineData(TagDataType.Int16, 16)]
    [InlineData(TagDataType.Int32, 32)]
    [InlineData(TagDataType.Int64, 64)]
    [InlineData(TagDataType.Boolean, 0)]
    [InlineData(TagDataType.Float, 0)]
    [InlineData(TagDataType.Double, 0)]
    [InlineData(TagDataType.String, 0)]
    [InlineData(TagDataType.DateTime, 0)]
    [InlineData(TagDataType.Enum, 0)]
    public void ValidateSelector_RejectsInvalidRangesAndTypes(TagDataType dataType, int bitIndex)
    {
        var selector = new TagValueSelector(TagValueSelectorKind.Bit, bitIndex);

        var success = TagBitSemantics.TryValidateSelector(dataType, selector, out var error);

        Assert.False(success);
        Assert.NotNull(error);
    }

    [Fact]
    public void Project_FailsClosedForWrongCanonicalIdentityOrClrValueType()
    {
        var tag = TagDefinition.Create("Status", "Plant.Status", TagDataType.Int32);
        var wrongReference = new TagValueReference(Guid.NewGuid(), new TagValueSelector(TagValueSelectorKind.Bit, 0));
        var source = new TagValue(tag.Id, 1, DateTimeOffset.UtcNow, TagQuality.Good);

        Assert.False(TagBitSemantics.TryProject(tag, wrongReference, source, out var wrongIdentityValue, out _));
        Assert.Null(wrongIdentityValue);

        var validReference = BitReference(tag, 0);
        var wrongClrSource = new TagValue(tag.Id, (short)1, DateTimeOffset.UtcNow, TagQuality.Good);

        Assert.False(TagBitSemantics.TryProject(tag, validReference, wrongClrSource, out var wrongTypeValue, out _));
        Assert.Null(wrongTypeValue);
    }

    [Fact]
    public void SetBit_Int16_PreservesEveryUnrelatedBit()
    {
        var tag = TagDefinition.Create("Command", "Plant.Command", TagDataType.Int16);
        var reference = BitReference(tag, 7);
        const short original = unchecked((short)0b1010_0101_0101_0101);

        Assert.True(TagBitSemantics.TrySetBit(tag, reference, original, true, out var setValue, out var setError), setError);
        var set = Assert.IsType<short>(setValue);
        Assert.Equal(unchecked((short)(original | (1 << 7))), set);
        Assert.Equal(
            unchecked((ushort)original) & ~(1u << 7),
            unchecked((ushort)set) & ~(1u << 7));

        Assert.True(TagBitSemantics.TrySetBit(tag, reference, set, false, out var clearedValue, out var clearError), clearError);
        var cleared = Assert.IsType<short>(clearedValue);
        Assert.Equal(unchecked((short)(set & ~(1 << 7))), cleared);
        Assert.Equal(
            unchecked((ushort)set) & ~(1u << 7),
            unchecked((ushort)cleared) & ~(1u << 7));
    }

    [Fact]
    public void SetBit_Int32AndInt64_CanMutateSignBitsWithoutChangingOtherBits()
    {
        var tag32 = TagDefinition.Create("Command32", "Plant.Command32", TagDataType.Int32);
        var tag64 = TagDefinition.Create("Command64", "Plant.Command64", TagDataType.Int64);

        Assert.True(TagBitSemantics.TrySetBit(tag32, BitReference(tag32, 31), 5, true, out var int32Value, out var int32Error), int32Error);
        Assert.Equal(unchecked((int)0x80000005), Assert.IsType<int>(int32Value));

        Assert.True(TagBitSemantics.TrySetBit(tag64, BitReference(tag64, 63), 5L, true, out var int64Value, out var int64Error), int64Error);
        Assert.Equal(unchecked((long)0x8000000000000005UL), Assert.IsType<long>(int64Value));
    }

    [Fact]
    public void SetBit_FailsClosedForReadOnlyMissingOrWrongTypedValues()
    {
        var readOnly = TagDefinition.Create("ReadOnly", "Plant.ReadOnly", TagDataType.Int16, readOnly: true);
        var writable = TagDefinition.Create("Writable", "Plant.Writable", TagDataType.Int16);

        Assert.False(TagBitSemantics.TrySetBit(readOnly, BitReference(readOnly, 0), (short)0, true, out var readOnlyResult, out _));
        Assert.Null(readOnlyResult);

        Assert.False(TagBitSemantics.TrySetBit(writable, BitReference(writable, 0), null, true, out var missingResult, out _));
        Assert.Null(missingResult);

        Assert.False(TagBitSemantics.TrySetBit(writable, BitReference(writable, 0), 0, true, out var wrongTypeResult, out _));
        Assert.Null(wrongTypeResult);
    }

    [Fact]
    public void StableReferenceAndFriendlyDisplay_KeepGuidSelectorAuthority()
    {
        var tagId = Guid.Parse("7f137647-c86e-4ef6-927b-a56d53461ee1");
        var selector = new TagValueSelector(TagValueSelectorKind.Bit, 3);
        var reference = new TagValueReference(tagId, selector);
        var sameReferenceAfterRename = new TagValueReference(tagId, new TagValueSelector(TagValueSelectorKind.Bit, 3));

        Assert.Equal(reference, sameReferenceAfterRename);
        Assert.Equal("Plant.Status.03", TagBitSemantics.FormatDisplayReference("Plant.Status", reference));
        Assert.Equal("Renamed.Status.03", TagBitSemantics.FormatDisplayReference("Renamed.Status", reference));
    }

    private static TagValueReference BitReference(TagDefinition tag, int bitIndex)
        => new(tag.Id, new TagValueSelector(TagValueSelectorKind.Bit, bitIndex));
}
