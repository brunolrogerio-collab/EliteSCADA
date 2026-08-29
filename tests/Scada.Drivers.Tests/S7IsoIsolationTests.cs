using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoIsolationTests
{
    [Fact]
    public async Task TwoDataSources_OneSessionLossDoesNotContaminateTheOther()
    {
        await using var serverA = new TestS7IsoServer();
        await using var serverB = new TestS7IsoServer();
        serverA.SetBytes(S7IsoArea.Merker, 0, 0, new byte[] { 0x11, 0x11 });
        serverB.SetBytes(S7IsoArea.Merker, 0, 0, new byte[] { 0x22, 0x22 });

        var tagA = S7IsoTransportTests.Tag(TagDataType.Int16);
        var tagB = S7IsoTransportTests.Tag(TagDataType.Int16);
        var pointA = new S7IsoPoint(tagA, S7IsoArea.Merker, 0, S7IsoValueType.Int16);
        var pointB = new S7IsoPoint(tagB, S7IsoArea.Merker, 0, S7IsoValueType.Int16);

        var eventBus = new InMemoryScadaEventBus();
        var cache = new CurrentTagCache(eventBus);
        var registry = new InMemoryTagRegistry();
        await using var driverA = new S7IsoDriver(
            "s7-a",
            "S7 A",
            Options(serverA.Port, TimeSpan.FromMilliseconds(300)),
            cache,
            registry,
            new[] { pointA },
            TimeSpan.FromMilliseconds(50));
        await using var driverB = new S7IsoDriver(
            "s7-b",
            "S7 B",
            Options(serverB.Port, TimeSpan.Zero),
            cache,
            registry,
            new[] { pointB },
            TimeSpan.FromMilliseconds(50));

        await driverA.StartAsync();
        await driverB.StartAsync();
        await WaitUntilAsync(
            () =>
                cache.TryGet(tagA.Id, out var a) && a?.Quality == TagQuality.Good &&
                cache.TryGet(tagB.Id, out var b) && b?.Quality == TagQuality.Good,
            TimeSpan.FromSeconds(2));

        serverA.DropActiveConnection();
        await WaitUntilAsync(
            () => cache.TryGet(tagA.Id, out var a) && a?.Quality == TagQuality.BadCommunication,
            TimeSpan.FromSeconds(2));

        var failedA = Assert.IsType<TagValue>((await driverA.ReadAsync(tagA.Id))!);
        var healthyB = Assert.IsType<TagValue>((await driverB.ReadAsync(tagB.Id))!);
        Assert.Equal((short)0x1111, Assert.IsType<short>(failedA.Value));
        Assert.Equal(TagQuality.BadCommunication, failedA.Quality);
        Assert.Equal((short)0x2222, Assert.IsType<short>(healthyB.Value));
        Assert.Equal(TagQuality.Good, healthyB.Quality);
        Assert.Equal(CommunicationDriverOperationalState.Reconnecting, driverA.GetCommunicationDiagnostics().State);
        Assert.Equal(CommunicationDriverOperationalState.Healthy, driverB.GetCommunicationDiagnostics().State);
        Assert.Equal(0L, driverB.GetCommunicationDiagnostics().Counters.Reconnects);

        serverA.SetBytes(S7IsoArea.Merker, 0, 0, new byte[] { 0x33, 0x33 });
        serverB.SetBytes(S7IsoArea.Merker, 0, 0, new byte[] { 0x44, 0x44 });
        await WaitUntilAsync(
            () =>
                cache.TryGet(tagA.Id, out var a) && a?.Quality == TagQuality.Good &&
                Equals(a.Value, (short)0x3333) &&
                cache.TryGet(tagB.Id, out var b) && b?.Quality == TagQuality.Good &&
                Equals(b.Value, (short)0x4444),
            TimeSpan.FromSeconds(3));

        Assert.True(driverA.GetCommunicationDiagnostics().Counters.Reconnects >= 1);
        Assert.Equal(0L, driverB.GetCommunicationDiagnostics().Counters.Reconnects);
    }

    private static S7IsoConnectionOptions Options(int port, TimeSpan reconnectDelay) => new(
        "127.0.0.1",
        S7CpuFamily.S71500,
        S7IsoConnectionMode.RackSlot,
        rack: 0,
        slot: 1,
        connectionRole: S7IsoConnectionRole.Basic,
        port: port,
        reconnectDelay: reconnectDelay);

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
