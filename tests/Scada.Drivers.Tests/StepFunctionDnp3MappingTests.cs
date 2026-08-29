using Scada.Drivers.Dnp3;
using Scada.Drivers.Dnp3.StepFunction;
using Step = dnp3;

namespace Scada.Drivers.Tests;

public sealed class StepFunctionDnp3MappingTests
{
    [Theory]
    [InlineData(Step.Variation.Group1Var2, 1, 2)]
    [InlineData(Step.Variation.Group32Var7, 32, 7)]
    [InlineData(Step.Variation.Group42Var8, 42, 8)]
    public void VariationMapping_PreservesGroupAndVariation(Step.Variation source, byte group, byte variation)
    {
        Assert.True(StepFunctionDnp3Mapping.TryMapVariation(source, out var mapped));
        Assert.Equal(new Dnp3ObjectVariation(group, variation), mapped);
    }

    [Fact]
    public void Flags_WithoutWireFlags_RemainGoodInsteadOfOffline()
    {
        var flags = StepFunctionDnp3Mapping.MapFlags(Dnp3PointKind.BinaryInput, hasFlags: false, raw: 0);

        Assert.False(flags.HasFlags);
        Assert.Equal(Scada.Core.Tags.TagQuality.Good, Dnp3QualityMapper.Map(flags));
    }

    [Fact]
    public void Flags_MapFamilySpecificBits()
    {
        var analog = StepFunctionDnp3Mapping.MapFlags(Dnp3PointKind.AnalogInput, true, 0x61);
        Assert.True(analog.Online);
        Assert.True(analog.OverRange);
        Assert.True(analog.ReferenceError);

        var counter = StepFunctionDnp3Mapping.MapFlags(Dnp3PointKind.Counter, true, 0x61);
        Assert.True(counter.Rollover);
        Assert.True(counter.Discontinuity);
        Assert.False(counter.OverRange);

        var binary = StepFunctionDnp3Mapping.MapFlags(Dnp3PointKind.BinaryInput, true, 0x21);
        Assert.True(binary.ChatterFilter);
    }

    [Fact]
    public void NumericMapping_PreservesVariationWidth()
    {
        Assert.IsType<ushort>(StepFunctionDnp3Mapping.MapCounter(65535, new Dnp3ObjectVariation(20, 2)));
        Assert.IsType<uint>(StepFunctionDnp3Mapping.MapCounter(uint.MaxValue, new Dnp3ObjectVariation(20, 1)));
        Assert.IsType<short>(StepFunctionDnp3Mapping.MapAnalog(-123, new Dnp3ObjectVariation(30, 2)));
        Assert.IsType<int>(StepFunctionDnp3Mapping.MapAnalog(123456, new Dnp3ObjectVariation(30, 1)));
        Assert.IsType<float>(StepFunctionDnp3Mapping.MapAnalog(1.25, new Dnp3ObjectVariation(30, 5)));
        Assert.IsType<double>(StepFunctionDnp3Mapping.MapAnalog(1.25, new Dnp3ObjectVariation(30, 6)));
    }

    [Fact]
    public void TimestampMapping_PreservesSynchronizationQuality()
    {
        var synchronized = StepFunctionDnp3Mapping.MapTimestamp(Step.Timestamp.SynchronizedTimestamp(1_700_000_000_000));
        var unsynchronized = StepFunctionDnp3Mapping.MapTimestamp(Step.Timestamp.UnsynchronizedTimestamp(1_700_000_000_001));
        var invalid = StepFunctionDnp3Mapping.MapTimestamp(Step.Timestamp.InvalidTimestamp());

        Assert.NotNull(synchronized.Timestamp);
        Assert.True(synchronized.Synchronized);
        Assert.NotNull(unsynchronized.Timestamp);
        Assert.False(unsynchronized.Synchronized);
        Assert.Null(invalid.Timestamp);
    }

    [Fact]
    public void CrobMapping_PreservesOperationTripCloseAndTiming()
    {
        var profile = new Dnp3BinaryCommandProfile
        {
            Mode = Dnp3CommandMode.SelectBeforeOperate,
            TrueOperation = Dnp3BinaryOperation.PulseOn,
            FalseOperation = Dnp3BinaryOperation.PulseOff,
            TripCloseCode = Dnp3TripCloseCode.Trip,
            Count = 2,
            OnTime = TimeSpan.FromMilliseconds(125),
            OffTime = TimeSpan.FromMilliseconds(250)
        };

        var crob = StepFunctionDnp3Mapping.BuildCrob(Dnp3BinaryOperation.PulseOn, profile);

        Assert.Equal(Step.OpType.PulseOn, crob.Code.OpType);
        Assert.Equal(Step.TripCloseCode.Trip, crob.Code.Tcc);
        Assert.Equal((byte)2, crob.Count);
        Assert.Equal((uint)125, crob.OnTime);
        Assert.Equal((uint)250, crob.OffTime);
    }

    [Fact]
    public void ClassMapping_PreservesConfiguredSets()
    {
        var classes = StepFunctionDnp3Mapping.MapClasses(Dnp3ClassSet.Class0 | Dnp3ClassSet.Class2);
        var events = StepFunctionDnp3Mapping.MapEventClasses(Dnp3ClassSet.Class1 | Dnp3ClassSet.Class3);

        Assert.True(classes.Class0);
        Assert.False(classes.Class1);
        Assert.True(classes.Class2);
        Assert.False(classes.Class3);
        Assert.True(events.Class1);
        Assert.False(events.Class2);
        Assert.True(events.Class3);
    }
}
