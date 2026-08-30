using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoCommunicationBindingProjectionTests
{
    [Fact]
    public void Projection_KeepsBindingVersionOneAndMovesOrderingToPhysicalTransform()
    {
        var binding = new S7IsoTagBinding(
            S7IsoTagBinding.CurrentSchemaVersion,
            S7IsoArea.DataBlock,
            12,
            S7IsoValueType.Float32,
            DbNumber: 7,
            Writable: true,
            ValueOrder: S7IsoValueOrder.WordSwap);

        var portable = S7IsoCommunicationBindingProjection.ToCanonicalPortableAddress(binding);
        var settings = S7IsoCommunicationBindingProjection.ToCanonicalSettings(binding);
        var transform = S7IsoCommunicationBindingProjection.GetPhysicalValueTransform(binding);

        Assert.Equal(S7IsoTagBinding.SchemaId, S7IsoCommunicationBindingProjection.SchemaId);
        Assert.Equal(S7IsoTagBinding.CurrentSchemaVersion, S7IsoCommunicationBindingProjection.SchemaVersion);
        Assert.Equal(
            "s7iso:v1;area=DataBlock;db=7;byte=12;bit=0;type=Float32;string=0;writable=true",
            portable);
        Assert.DoesNotContain("order", portable, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(settings.Keys, key => string.Equals(key, "valueOrder", StringComparison.OrdinalIgnoreCase));
        Assert.False(transform.ByteSwap);
        Assert.True(transform.WordSwap);
    }

    [Fact]
    public void MaterializeCanonical_RecombinesVersionOneAddressAndSharedTransformForRuntime()
    {
        const string portable =
            "s7iso:v1;area=DataBlock;db=7;byte=12;bit=0;type=Float32;string=0;writable=true";
        var settings = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["area"] = "DataBlock",
            ["dbNumber"] = "7",
            ["byteOffset"] = "12",
            ["bitOffset"] = "0",
            ["valueType"] = "Float32",
            ["stringLength"] = "0",
            ["writable"] = "true"
        };

        Assert.True(
            S7IsoCommunicationBindingProjection.TryMaterializeCanonical(
                portable,
                settings,
                byteSwap: true,
                wordSwap: true,
                out var binding,
                out var error),
            error);

        Assert.NotNull(binding);
        Assert.Equal(S7IsoTagBinding.CurrentSchemaVersion, binding!.SchemaVersion);
        Assert.Equal(S7IsoValueOrder.ByteAndWordSwap, binding.ValueOrder);
        Assert.Equal((ushort)7, binding.DbNumber);
        Assert.Equal(12, binding.ByteOffset);
    }

    [Fact]
    public void MaterializeCanonical_RejectsSecondPersistedOrderingAuthority()
    {
        const string portableWithOrder =
            "s7iso:v1;area=Merker;db=0;byte=4;bit=0;type=Int32;string=0;writable=false;order=WordSwap";

        Assert.False(
            S7IsoCommunicationBindingProjection.TryMaterializeCanonical(
                portableWithOrder,
                settings: null,
                byteSwap: false,
                wordSwap: true,
                out var addressBinding,
                out var addressError));
        Assert.Null(addressBinding);
        Assert.Contains("physical value transform", addressError, StringComparison.OrdinalIgnoreCase);

        const string canonicalPortable =
            "s7iso:v1;area=Merker;db=0;byte=4;bit=0;type=Int32;string=0;writable=false";
        var duplicateSettings = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["area"] = "Merker",
            ["dbNumber"] = "0",
            ["byteOffset"] = "4",
            ["bitOffset"] = "0",
            ["valueType"] = "Int32",
            ["stringLength"] = "0",
            ["writable"] = "false",
            ["valueOrder"] = "WordSwap"
        };

        Assert.False(
            S7IsoCommunicationBindingProjection.TryMaterializeCanonical(
                canonicalPortable,
                duplicateSettings,
                byteSwap: false,
                wordSwap: true,
                out var settingsBinding,
                out var settingsError));
        Assert.Null(settingsBinding);
        Assert.Contains("physical value transform", settingsError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LegacyVersionOneOrderingRemainsReadableForMigration()
    {
        const string legacy =
            "s7iso:v1;area=Merker;db=0;byte=4;bit=0;type=Int32;string=0;writable=false;order=WordSwap";

        Assert.True(S7IsoTagBinding.TryParsePortableAddress(legacy, out var binding, out var error), error);
        Assert.NotNull(binding);
        Assert.Equal(S7IsoTagBinding.CurrentSchemaVersion, binding!.SchemaVersion);
        Assert.Equal(S7IsoValueOrder.WordSwap, binding.ValueOrder);
    }

    [Fact]
    public void MaterializeCanonical_RejectsTransformThatIsInvalidForSiemensValueWidth()
    {
        const string portable =
            "s7iso:v1;area=Merker;db=0;byte=4;bit=0;type=Int16;string=0;writable=false";

        Assert.False(
            S7IsoCommunicationBindingProjection.TryMaterializeCanonical(
                portable,
                settings: null,
                byteSwap: false,
                wordSwap: true,
                out var binding,
                out var error));

        Assert.Null(binding);
        Assert.Contains("word swap", error, StringComparison.OrdinalIgnoreCase);
    }
}
