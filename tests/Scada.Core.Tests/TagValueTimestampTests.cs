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
    }

    [Fact]
    public void GoodFactory_LeavesProtocolTimestampsUnset()
    {
        var value = TagValue.Good(Guid.NewGuid(), true, "modbus.tcp");

        Assert.Null(value.SourceTimestamp);
        Assert.Null(value.ServerTimestamp);
    }
}
