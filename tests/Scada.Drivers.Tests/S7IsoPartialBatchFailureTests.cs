using System.Buffers.Binary;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoPartialBatchFailureTests
{
    [Fact]
    public async Task LaterBatchTransportLoss_PreservesEarlierGoodTagsAndRecoversAllInPlace()
    {
        await using var server = new TestS7IsoServer(240)
        {
            DropBeforeDataRequestNumber = 2
        };

        var points = Enumerable.Range(0, 30)
            .Select(index =>
            {
                var bytes = new byte[4];
                BinaryPrimitives.WriteInt32BigEndian(bytes, index + 1);
                server.SetBytes(S7IsoArea.Merker, 0, index * 4, bytes);
                return new S7IsoPoint(
                    S7IsoTransportTests.Tag(TagDataType.Int32),
                    S7IsoArea.Merker,
                    index * 4,
                    S7IsoValueType.Int32);
            })
            .ToArray();

        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();
        await using var driver = new S7IsoDriver(
            "s7-partial-batch",
            "S7 Partial Batch",
            S7IsoTransportTests.Options(server.Port),
            cache,
            registry,
            points,
            TimeSpan.FromSeconds(2));

        await driver.StartAsync();
        await WaitUntilAsync(
            () => driver.GetCommunicationDiagnostics().State == CommunicationDriverOperationalState.Reconnecting,
            TimeSpan.FromSeconds(2));

        for (var index = 0; index < 19; index++)
        {
            var sample = Assert.IsType<TagValue>((await driver.ReadAsync(points[index].Tag.Id))!);
            Assert.Equal(TagQuality.Good, sample.Quality);
            Assert.Equal(index + 1, Assert.IsType<int>(sample.Value));
        }
        for (var index = 19; index < points.Length; index++)
        {
            var sample = Assert.IsType<TagValue>((await driver.ReadAsync(points[index].Tag.Id))!);
            Assert.Equal(TagQuality.BadCommunication, sample.Quality);
            Assert.Null(sample.Value);
        }

        var partial = driver.GetCommunicationDiagnostics();
        Assert.Equal(19, partial.TagQuality.Good);
        Assert.Equal(11, partial.TagQuality.BadCommunication);
        Assert.Equal(nameof(S7IsoFailureKind.TransportUnavailable), partial.ProtocolDetails!["lastFailureKind"]);
        Assert.Equal("2", partial.ProtocolDetails["lastReadBatchCount"]);
        Assert.Equal("30", partial.ProtocolDetails["lastReadPointCount"]);
        Assert.True(partial.Counters.SuccessfulOperations >= 19);
        Assert.True(partial.Counters.FailedOperations >= 11);
        Assert.Equal(1L, partial.Counters.Connections);
        Assert.Equal(1L, partial.Counters.Disconnections);

        server.DropBeforeDataRequestNumber = null;
        await WaitUntilAsync(
            () =>
            {
                var diagnostics = driver.GetCommunicationDiagnostics();
                return diagnostics.State == CommunicationDriverOperationalState.Healthy &&
                       diagnostics.TagQuality.Good == points.Length &&
                       diagnostics.Counters.Reconnects >= 1;
            },
            TimeSpan.FromSeconds(5));

        var recovered = driver.GetCommunicationDiagnostics();
        Assert.Equal(30, recovered.TagQuality.Good);
        Assert.Equal(0, recovered.TagQuality.BadCommunication);
        Assert.Equal("2", recovered.ProtocolDetails!["lastReadBatchCount"]);
        Assert.Equal("30", recovered.ProtocolDetails["lastReadPointCount"]);
        Assert.True(recovered.Counters.Connections >= 2);
        Assert.True(recovered.Counters.Reconnects >= 1);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(10);
        }
        Assert.True(condition(), $"Condition was not met within {timeout}.");
    }
}
