using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoCommunicationBindingSchemaV2Tests
{
    [Fact]
    public void Projection_SeparatesProtocolIdentityFromPhysicalTransform()
    {
        var binding = new S7IsoTagBinding(
            S7IsoTagBinding.CurrentSchemaVersion,
            S7IsoArea.DataBlock,
            12,
            S7IsoValueType.Float32,
            DbNumber: 7,
            Writable: true,
            ValueOrder: S7IsoValueOrder.WordSwap);

        var portable = S7IsoCommunicationBindingSchemaV2.ToPortableAddress(binding);
        var settings = S7IsoCommunicationBindingSchemaV2.ToSettings(binding);
        var transform = S7IsoCommunicationBindingSchemaV2.GetPhysicalTransform(binding);

        Assert.Equal(
            "s7iso:v2;area=DataBlock;db=7;byte=12;bit=0;type=Float32;string=0;writable=true",
            portable);
        Assert.DoesNotContain("order", portable, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("valueOrder", settings.Keys);
        Assert.False(transform.ByteSwap);
        Assert.True(transform.WordSwap);
    }

    [Fact]
    public void Materialize_RecombinesV2ProtocolSettingsAndSharedTransformForRuntime()
    {
        const string portable =
            "s7iso:v2;area=DataBlock;db=7;byte=12;bit=0;type=Float32;string=0;writable=true";
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
            S7IsoCommunicationBindingSchemaV2.TryMaterialize(
                portable,
                settings,
                byteSwap: true,
                wordSwap: true,
                out var binding,
                out var error),
            error);

        Assert.NotNull(binding);
        Assert.Equal(S7IsoValueOrder.ByteAndWordSwap, binding!.ValueOrder);
        Assert.Equal(S7IsoArea.DataBlock, binding.Area);
        Assert.Equal((ushort)7, binding.DbNumber);
        Assert.Equal(12, binding.ByteOffset);
    }

    [Fact]
    public void Materialize_RejectsDuplicateOrderingAuthorityAndSettingsDrift()
    {
        const string portable =
            "s7iso:v2;area=Merker;db=0;byte=4;bit=0;type=Int32;string=0;writable=false";

        var duplicateAuthority = S7IsoCommunicationBindingSchemaV2.ToSettings(new S7IsoTagBinding(
            S7IsoTagBinding.CurrentSchemaVersion,
            S7IsoArea.Merker,
            4,
            S7IsoValueType.Int32)).ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        duplicateAuthority["valueOrder"] = nameof(S7IsoValueOrder.WordSwap);

        Assert.False(
            S7IsoCommunicationBindingSchemaV2.TryMaterialize(
                portable,
                duplicateAuthority,
                false,
                false,
                out var duplicateBinding,
                out var duplicateError));
        Assert.Null(duplicateBinding);
        Assert.Contains("shared physical value transform", duplicateError, StringComparison.OrdinalIgnoreCase);

        var drifted = new Dictionary<string, string>(duplicateAuthority, StringComparer.Ordinal);
        drifted.Remove("valueOrder");
        drifted["byteOffset"] = "6";

        Assert.False(
            S7IsoCommunicationBindingSchemaV2.TryMaterialize(
                portable,
                drifted,
                false,
                false,
                out var driftedBinding,
                out var driftedError));
        Assert.Null(driftedBinding);
        Assert.Contains("does not match PortableAddress", driftedError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Materialize_RejectsWordSwapForSixteenBitValue()
    {
        const string portable =
            "s7iso:v2;area=Merker;db=0;byte=4;bit=0;type=Int16;string=0;writable=false";

        Assert.False(
            S7IsoCommunicationBindingSchemaV2.TryMaterialize(
                portable,
                settings: null,
                byteSwap: false,
                wordSwap: true,
                out var binding,
                out var error));

        Assert.Null(binding);
        Assert.Contains("word swap", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PortableV2_RejectsPhysicalOrderingWhileLegacyV1RemainsReadable()
    {
        const string invalidV2 =
            "s7iso:v2;area=Merker;db=0;byte=4;bit=0;type=Int32;string=0;writable=false;order=WordSwap";
        Assert.False(S7IsoCommunicationBindingSchemaV2.TryParsePortableAddress(
            invalidV2,
            out var invalidBinding,
            out var invalidError));
        Assert.Null(invalidBinding);
        Assert.Contains("cannot contain physical", invalidError, StringComparison.OrdinalIgnoreCase);

        const string legacyV1 =
            "s7iso:v1;area=Merker;db=0;byte=4;bit=0;type=Int32;string=0;writable=false;order=WordSwap";
        Assert.True(S7IsoTagBinding.TryParsePortableAddress(
            legacyV1,
            out var legacyBinding,
            out var legacyError),
            legacyError);
        Assert.Equal(S7IsoValueOrder.WordSwap, legacyBinding!.ValueOrder);
    }
}
