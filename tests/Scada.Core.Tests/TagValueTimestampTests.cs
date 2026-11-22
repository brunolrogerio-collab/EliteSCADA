using Scada.Core.Tags;

namespace Scada.Core.Tests;

public sealed class TagValueTimestampTests
{
    [Fact]
    public void ProtocolTimestamps_AreOptionalAndPreserveLocalTimestamp()
    {
        var observedAt = DateTimeOffset.Parse("2026-08-27T15:00:00-03:00");
        var sourceAt = observedAt.AddMilliseconds(-125);
        var serverAt = observedAt.AddMilliseconds(-40);

        var value = new TagValue(
            Guid.NewGuid(),
            42.5,
            observedAt,
            TagQuality.Good,
            "opc.ua")
        {
            SourceTimestamp = sourceAt,
            ServerTimestamp = serverAt
        };

        Assert.Equal(observedAt, value.Timestamp);
        Assert.Equal(sourceAt, value.SourceTimestamp);
        Assert.Equal(serverAt, value.ServerTimestamp);
        Assert.Equal(TimestampEvidenceState.PresentUnverified, value.SourceTimestampEvidenceState);
        Assert.Equal(TimestampEvidenceState.PresentUnverified, value.ServerTimestampEvidenceState);
        Assert.Equal(TimeSpan.Zero, value.SourceTimestamp!.Value.Offset);
        Assert.Equal(TimeSpan.Zero, value.ServerTimestamp!.Value.Offset);
    }

    [Fact]
    public void GoodFactory_LeavesProtocolTimestampsUnset()
    {
        var value = TagValue.Good(Guid.NewGuid(), true, "modbus.tcp");

        Assert.Null(value.SourceTimestamp);
        Assert.Null(value.ServerTimestamp);
        Assert.Equal(TimestampEvidenceState.NotProvided, value.SourceTimestampEvidenceState);
        Assert.Equal(TimestampEvidenceState.NotProvided, value.ServerTimestampEvidenceState);
    }

    [Fact]
    public void LegacyPayloadWithProtocolTimestamp_IsNeverPromotedToSynchronized()
    {
        var value = new TagValue(
            Guid.NewGuid(),
            1,
            DateTimeOffset.UtcNow,
            TagQuality.Good)
        {
            SourceTimestamp = DateTimeOffset.UtcNow.AddSeconds(-1)
        };

        Assert.Equal(TimestampEvidenceState.PresentUnverified, value.SourceTimestampEvidenceState);
    }

    [Fact]
    public void ServerTimestampCannotClaimSourceClockSynchronization()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TagValue(
            Guid.NewGuid(),
            12,
            DateTimeOffset.UtcNow,
            TagQuality.Good)
        {
            ServerTimestamp = DateTimeOffset.UtcNow,
            ServerTimestampEvidenceState = TimestampEvidenceState.Synchronized
        });
    }
}
