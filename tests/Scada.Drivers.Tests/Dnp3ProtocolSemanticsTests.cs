using Scada.Core.Tags;
using Scada.Drivers.Dnp3;

namespace Scada.Drivers.Tests;

public sealed class Dnp3ProtocolSemanticsTests
{
    [Fact]
    public void PointIdentity_DoesNotDependOnStaticOrEventVariation()
    {
        var first = new Dnp3PointBinding(
            Dnp3PointKind.AnalogInput,
            7,
            TagDataType.Double,
            new Dnp3ObjectVariation(30, 6),
            new Dnp3ObjectVariation(32, 8),
            Dnp3EventClass.Class1);

        var second = first with
        {
            StaticVariation = new Dnp3ObjectVariation(30, 0),
            EventVariation = new Dnp3ObjectVariation(32, 0),
            ExpectedEventClass = Dnp3EventClass.Class3
        };

        first.Validate();
        second.Validate();

        Assert.Equal("dnp3:analogInput:7", first.PortableAddress);
        Assert.Equal(first.PortableAddress, second.PortableAddress);
    }

    [Fact]
    public void PointBinding_RejectsVariationFromAnotherPointFamily()
    {
        var binding = new Dnp3PointBinding(
            Dnp3PointKind.AnalogInput,
            12,
            TagDataType.Int32,
            new Dnp3ObjectVariation(30, 1),
            new Dnp3ObjectVariation(2, 2));

        Assert.Throws<ArgumentException>(() => binding.Validate());
    }

    [Fact]
    public void DoubleBitBinary_RequiresCanonicalEnumType()
    {
        var valid = new Dnp3PointBinding(
            Dnp3PointKind.DoubleBitBinaryInput,
            4,
            TagDataType.Enum,
            new Dnp3ObjectVariation(3, 2),
            new Dnp3ObjectVariation(4, 2));

        valid.Validate();

        var invalid = valid with { DataType = TagDataType.Boolean };
        Assert.Throws<ArgumentException>(() => invalid.Validate());
    }

    [Fact]
    public void CounterConversions_PreserveUnsignedWireRange()
    {
        Assert.Equal(ushort.MaxValue, Dnp3ValueConversions.Counter16ToCanonical(ushort.MaxValue));
        Assert.Equal(4294967295L, Dnp3ValueConversions.Counter32ToCanonical(uint.MaxValue));
    }

    [Fact]
    public void QualityMapping_UsesWorstApplicableSemanticEvidence()
    {
        var communicationLost = new Dnp3PointFlagSet(
            HasFlags: true,
            Online: false,
            CommunicationLost: true,
            LocalForced: true);

        var referenceError = new Dnp3PointFlagSet(
            HasFlags: true,
            Online: true,
            ReferenceError: true);

        var forced = new Dnp3PointFlagSet(
            HasFlags: true,
            Online: true,
            RemoteForced: true);

        Assert.Equal(TagQuality.BadCommunication, Dnp3QualityMapper.Map(communicationLost));
        Assert.Equal(TagQuality.BadDevice, Dnp3QualityMapper.Map(referenceError));
        Assert.Equal(TagQuality.Uncertain, Dnp3QualityMapper.Map(forced));
        Assert.Equal(TagQuality.Good, Dnp3QualityMapper.Map(Dnp3PointFlagSet.WithoutFlags));
    }

    [Fact]
    public void OverRangePolicy_RemainsExplicitUntilCommonQualityDecision()
    {
        var flags = new Dnp3PointFlagSet(HasFlags: true, Online: true, OverRange: true);

        Assert.Equal(TagQuality.Uncertain, Dnp3QualityMapper.Map(flags));
        Assert.Equal(TagQuality.BadDevice, Dnp3QualityMapper.Map(flags, Dnp3OverRangePolicy.BadDevice));
    }

    [Fact]
    public void UnsynchronizedSourceTime_DowngradesOtherwiseGoodMeasurement()
    {
        var sourceTimestamp = new DateTimeOffset(2026, 8, 29, 14, 0, 0, TimeSpan.Zero);
        var observedAt = sourceTimestamp.AddSeconds(3);

        var value = Dnp3MeasurementMapper.CreateTagValue(
            Guid.NewGuid(),
            123,
            observedAt,
            Dnp3PointFlagSet.Nominal,
            sourceTimestamp,
            sourceTimestampSynchronized: false,
            source: "dnp3-test");

        Assert.Equal(TagQuality.Uncertain, value.Quality);
        Assert.Equal(observedAt, value.Timestamp);
        Assert.Equal(sourceTimestamp, value.SourceTimestamp);
        Assert.Null(value.ServerTimestamp);
    }

    [Fact]
    public void AssociationDefaults_ExpressIntegrityThenEventClasses()
    {
        var options = new Dnp3AssociationOptions();

        options.Validate();

        Assert.Equal(Dnp3ClassSet.All, options.StartupIntegrityClasses);
        Assert.Equal(Dnp3ClassSet.EventClasses, options.DisableUnsolicitedClassesOnStartup);
        Assert.Equal(Dnp3ClassSet.EventClasses, options.EnableUnsolicitedClassesAfterIntegrity);
        Assert.Equal(16, options.MaxQueuedUserRequests);
    }

    [Fact]
    public void AssociationOptions_RejectClassZeroForUnsolicitedConfiguration()
    {
        var options = new Dnp3AssociationOptions
        {
            EnableUnsolicitedClassesAfterIntegrity = Dnp3ClassSet.Class0 | Dnp3ClassSet.Class1
        };

        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void PulseCrob_RequiresPositiveOnTime()
    {
        var invalid = new Dnp3BinaryCommandProfile
        {
            TrueOperation = Dnp3BinaryOperation.PulseOn,
            OnTime = TimeSpan.Zero
        };

        Assert.Throws<ArgumentException>(() => invalid.Validate());

        var valid = invalid with { OnTime = TimeSpan.FromMilliseconds(250) };
        valid.Validate();
        Assert.Equal(Dnp3BinaryOperation.PulseOn, valid.ResolveOperation(true));
    }

    [Fact]
    public void AnalogCommandVariation_RequiresMatchingCanonicalType()
    {
        var profile = new Dnp3AnalogCommandProfile(
            Dnp3CommandMode.SelectBeforeOperate,
            Dnp3AnalogOutputVariation.Float64);

        profile.Validate(TagDataType.Double);
        Assert.Throws<ArgumentException>(() => profile.Validate(TagDataType.Float));
    }

    [Fact]
    public void CommandMode_DoesNotExposeDirectOperateNoResponseInFirstCut()
    {
        Assert.DoesNotContain("DirectOperateNoResponse", Enum.GetNames<Dnp3CommandMode>());
    }
}
