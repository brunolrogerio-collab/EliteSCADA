using Scada.Core.Tags;
using Scada.Drivers.Dnp3;

namespace Scada.Drivers.Tests;

public sealed class Dnp3SourceTimeEvidenceTests
{
    [Fact]
    public void MissingSourceTimestampIsUnknown()
    {
        var measurement = new Dnp3Measurement(
            Dnp3PointKind.BinaryInput,
            1,
            true,
            new Dnp3ObjectVariation(2, 2),
            IsEvent: true,
            Dnp3PointFlagSet.Nominal,
            SourceTimestamp: null,
            SourceTimestampSynchronized: false);

        Assert.Equal(Dnp3SourceTimeState.Unknown, Dnp3SourceTimeEvidence.Classify(measurement));
    }

    [Theory]
    [InlineData(true, Dnp3SourceTimeState.Synchronized)]
    [InlineData(false, Dnp3SourceTimeState.Unsynchronized)]
    public void PresentSourceTimestampPreservesSynchronizationEvidence(bool synchronized, Dnp3SourceTimeState expected)
    {
        var sourceTimestamp = new DateTimeOffset(2026, 8, 29, 22, 0, 0, TimeSpan.Zero);
        var measurement = new Dnp3Measurement(
            Dnp3PointKind.BinaryInput,
            1,
            true,
            new Dnp3ObjectVariation(2, 2),
            IsEvent: true,
            Dnp3PointFlagSet.Nominal,
            sourceTimestamp,
            synchronized);

        Assert.Equal(expected, Dnp3SourceTimeEvidence.Classify(measurement));

        var mapped = Dnp3MeasurementMapper.CreateTagValue(
            Guid.NewGuid(),
            true,
            DateTimeOffset.UtcNow,
            Dnp3PointFlagSet.Nominal,
            sourceTimestamp,
            synchronized,
            "dnp3-test");

        Assert.Equal(sourceTimestamp, mapped.SourceTimestamp);
        Assert.Equal(synchronized ? TagQuality.Good : TagQuality.Uncertain, mapped.Quality);
    }
}
