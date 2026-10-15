using Scada.Core.Tags;
using Scada.Drivers.Dnp3;

namespace Scada.Drivers.Tests;

public sealed class Dnp3SourceTimestampEvidenceTests
{
    [Theory]
    [InlineData(true, TimestampEvidenceState.Synchronized)]
    [InlineData(false, TimestampEvidenceState.Unsynchronized)]
    public void Mapper_RecordsOnlyExplicitPerMeasurementSynchronizationEvidence(
        bool synchronized,
        TimestampEvidenceState expected)
    {
        var sourceTimestamp = DateTimeOffset.Parse("2026-10-15T12:00:00-03:00");
        var observedAt = DateTimeOffset.Parse("2026-10-15T15:00:02Z");

        var value = Dnp3MeasurementMapper.CreateTagValue(
            Guid.NewGuid(),
            123,
            observedAt,
            Dnp3PointFlagSet.Nominal,
            sourceTimestamp,
            synchronized);

        Assert.Equal(observedAt, value.Timestamp);
        Assert.Equal(sourceTimestamp.ToUniversalTime(), value.SourceTimestamp);
        Assert.Equal(TimeSpan.Zero, value.SourceTimestamp!.Value.Offset);
        Assert.Equal(expected, value.SourceTimestampEvidenceState);
    }

    [Fact]
    public void Mapper_RecordsAbsentSourceTimestampAsNotProvided()
    {
        var value = Dnp3MeasurementMapper.CreateTagValue(
            Guid.NewGuid(),
            123,
            DateTimeOffset.UtcNow,
            Dnp3PointFlagSet.Nominal,
            sourceTimestamp: null);

        Assert.Null(value.SourceTimestamp);
        Assert.Equal(TimestampEvidenceState.NotProvided, value.SourceTimestampEvidenceState);
    }

    [Fact]
    public void Mapper_DoesNotInferSynchronizationWhenEvidenceWasNotSupplied()
    {
        var value = Dnp3MeasurementMapper.CreateTagValue(
            Guid.NewGuid(),
            123,
            DateTimeOffset.UtcNow,
            Dnp3PointFlagSet.Nominal,
            DateTimeOffset.UtcNow.AddSeconds(-1));

        Assert.Equal(TimestampEvidenceState.PresentUnverified, value.SourceTimestampEvidenceState);
    }
}
