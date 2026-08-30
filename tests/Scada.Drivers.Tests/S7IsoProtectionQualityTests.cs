using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoProtectionQualityTests
{
    [Fact]
    public async Task ProtectionDeniedRead_IsBadConfigurationWithoutReconnectAndRecoversInPlace()
    {
        await using var server = new TestS7IsoServer
        {
            ReadReturnCode = 0x03
        };
        server.SetBytes(S7IsoArea.Merker, 0, 12, new byte[] { 0x12, 0x34 });
        var tag = S7IsoTransportTests.Tag(TagDataType.Int16);
        var point = new S7IsoPoint(tag, S7IsoArea.Merker, 12, S7IsoValueType.Int16);
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();
        await using var driver = new S7IsoDriver(
            "s7-protection",
            "S7 Protection",
            S7IsoTransportTests.Options(server.Port),
            cache,
            registry,
            new[] { point },
            TimeSpan.FromMilliseconds(200));

        await driver.StartAsync();
        await WaitUntilAsync(
            () => cache.TryGet(tag.Id, out var value) && value?.Quality == TagQuality.BadConfiguration,
            TimeSpan.FromSeconds(2));

        var denied = Assert.IsType<TagValue>((await driver.ReadAsync(tag.Id))!);
        Assert.Null(denied.Value);
        Assert.Equal(TagQuality.BadConfiguration, denied.Quality);
        var degraded = driver.GetCommunicationDiagnostics();
        Assert.Equal(CommunicationDriverOperationalState.Degraded, degraded.State);
        Assert.Equal(nameof(S7IsoFailureKind.ProtectionDenied), degraded.ProtocolDetails!["lastFailureKind"]);
        Assert.Equal(1L, degraded.Counters.Connections);
        Assert.Equal(0L, degraded.Counters.Disconnections);
        Assert.Equal(0L, degraded.Counters.Reconnects);

        server.ReadReturnCode = S7IsoProtocol.ReturnCodeSuccess;
        await WaitUntilAsync(
            () => cache.TryGet(tag.Id, out var value) && value?.Quality == TagQuality.Good,
            TimeSpan.FromSeconds(2));

        var recovered = Assert.IsType<TagValue>((await driver.ReadAsync(tag.Id))!);
        Assert.Equal((short)0x1234, Assert.IsType<short>(recovered.Value));
        Assert.Equal(TagQuality.Good, recovered.Quality);
        var healthy = driver.GetCommunicationDiagnostics();
        Assert.Equal(CommunicationDriverOperationalState.Healthy, healthy.State);
        Assert.Equal(1L, healthy.Counters.Connections);
        Assert.Equal(0L, healthy.Counters.Disconnections);
        Assert.Equal(0L, healthy.Counters.Reconnects);
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
