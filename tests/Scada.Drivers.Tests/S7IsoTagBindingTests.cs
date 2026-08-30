using Scada.Core.Tags;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoTagBindingTests
{
    [Fact]
    public void Settings_RoundTripPortableAddress_AndMaterializeTypedPoint()
    {
        var settings = new Dictionary<string, string>
        {
            ["area"] = nameof(S7IsoArea.DataBlock),
            ["dbNumber"] = "7",
            ["byteOffset"] = "12",
            ["valueType"] = nameof(S7IsoValueType.Float32),
            ["writable"] = "true",
            ["valueOrder"] = nameof(S7IsoValueOrder.WordSwap)
        };

        Assert.True(S7IsoTagBinding.TryCreateFromSettings(settings, out var binding, out var issues));
        Assert.Empty(issues);
        Assert.NotNull(binding);

        var normalizedSettings = binding!.ToSettings();
        Assert.Equal(nameof(S7IsoArea.DataBlock), normalizedSettings["area"]);
        Assert.Equal("7", normalizedSettings["dbNumber"]);
        Assert.Equal("12", normalizedSettings["byteOffset"]);
        Assert.Equal(nameof(S7IsoValueType.Float32), normalizedSettings["valueType"]);
        Assert.Equal("true", normalizedSettings["writable"]);
        Assert.Equal(nameof(S7IsoValueOrder.WordSwap), normalizedSettings["valueOrder"]);
        Assert.True(S7IsoTagBinding.TryCreateFromSettings(normalizedSettings, out var fromNormalizedSettings, out var normalizedIssues));
        Assert.Empty(normalizedIssues);
        Assert.Equal(binding, fromNormalizedSettings);

        var portable = binding.ToPortableAddress();
        Assert.StartsWith("s7iso:v1;", portable);
        Assert.True(S7IsoTagBinding.TryParsePortableAddress(portable, out var parsed, out var parseError), parseError);
        Assert.Equal(binding, parsed);

        var tag = TagDefinition.Create(
            "Flow",
            "PLC.Flow",
            TagDataType.Float,
            source: "siemens.s7.iso",
            readOnly: false);
        var point = parsed!.ToPoint(tag);

        Assert.Equal(S7IsoArea.DataBlock, point.Area);
        Assert.Equal((ushort)7, point.DbNumber);
        Assert.Equal(12, point.ByteOffset);
        Assert.Equal(S7IsoValueType.Float32, point.ValueType);
        Assert.Equal(S7IsoValueOrder.WordSwap, point.ValueOrder);
        Assert.True(point.Writable);
    }

    [Fact]
    public void WString_RoundTripsThroughVersionOnePortableBinding()
    {
        var binding = new S7IsoTagBinding(
            S7IsoTagBinding.CurrentSchemaVersion,
            S7IsoArea.DataBlock,
            80,
            S7IsoValueType.WString,
            DbNumber: 4,
            Writable: true,
            StringLength: 32);

        var portable = binding.ToPortableAddress();

        Assert.Contains("type=WString", portable, StringComparison.Ordinal);
        Assert.Contains("string=32", portable, StringComparison.Ordinal);
        Assert.True(S7IsoTagBinding.TryParsePortableAddress(portable, out var parsed, out var error), error);
        Assert.Equal(binding, parsed);

        var point = parsed!.ToPoint(TagDefinition.Create(
            "WideText",
            "PLC.WideText",
            TagDataType.String,
            source: "siemens.s7.iso",
            readOnly: false));
        Assert.Equal(68, point.ByteLength);
        Assert.Equal((ushort)68, point.S7AnyElementCount);
    }

    [Theory]
    [InlineData("s7iso:v2;area=Merker;db=0;byte=0;bit=0;type=Boolean;string=0;writable=false;order=Normal")]
    [InlineData("s7iso:v1;area=Merker;db=0;byte=0;bit=0;type=Boolean;string=0;writable=false;order=Normal;vendorMagic=1")]
    public void PortableAddress_RejectsUnsupportedSchemaContent(string portable)
    {
        Assert.False(S7IsoTagBinding.TryParsePortableAddress(portable, out var binding, out var error));
        Assert.Null(binding);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Settings_RejectProtocolShapeErrorsBeforeRuntime()
    {
        var settings = new Dictionary<string, string>
        {
            ["area"] = nameof(S7IsoArea.Merker),
            ["dbNumber"] = "3",
            ["byteOffset"] = "10",
            ["bitOffset"] = "2",
            ["valueType"] = nameof(S7IsoValueType.Int16),
            ["valueOrder"] = nameof(S7IsoValueOrder.WordSwap)
        };

        Assert.False(S7IsoTagBinding.TryCreateFromSettings(settings, out var binding, out var issues));
        Assert.Null(binding);
        Assert.Contains(issues, issue => issue.FieldKey == "dbNumber");
        Assert.Contains(issues, issue => issue.FieldKey == "bitOffset");
        Assert.Contains(issues, issue => issue.FieldKey == "valueOrder");
    }

    [Fact]
    public void Settings_RejectPayloadThatCrossesS7AnyAddressSpace()
    {
        var settings = new Dictionary<string, string>
        {
            ["area"] = nameof(S7IsoArea.DataBlock),
            ["dbNumber"] = "1",
            ["byteOffset"] = "2097150",
            ["valueType"] = nameof(S7IsoValueType.Float64)
        };

        Assert.False(S7IsoTagBinding.TryCreateFromSettings(settings, out var binding, out var issues));
        Assert.Null(binding);
        Assert.Contains(issues, issue => issue.FieldKey == "byteOffset");
    }

    [Fact]
    public void DirectInvalidBinding_CannotBeSerializedAsEngineeringTruth()
    {
        var invalid = new S7IsoTagBinding(
            S7IsoTagBinding.CurrentSchemaVersion,
            (S7IsoArea)0xFF,
            0,
            S7IsoValueType.Int16);

        Assert.Throws<ArgumentException>(() => invalid.ToSettings());
        Assert.Throws<ArgumentException>(() => invalid.ToPortableAddress());
    }

    [Fact]
    public void ToPoint_RejectsCanonicalTagTypeMismatch()
    {
        var binding = new S7IsoTagBinding(
            S7IsoTagBinding.CurrentSchemaVersion,
            S7IsoArea.DataBlock,
            0,
            S7IsoValueType.Int32,
            DbNumber: 1);
        var incompatible = TagDefinition.Create(
            "WrongType",
            "PLC.WrongType",
            TagDataType.String,
            source: "siemens.s7.iso");

        Assert.Throws<ArgumentException>(() => binding.ToPoint(incompatible));
    }
}
