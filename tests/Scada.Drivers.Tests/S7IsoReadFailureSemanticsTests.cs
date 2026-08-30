using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoReadFailureSemanticsTests
{
    [Fact]
    public async Task AddressInvalidRead_DegradesPointWithoutDroppingSessionAndRecoversInPlace()
    {
        await using var server = new TestS7IsoServer
        {
            ReadReturnCode = 0x05
        };
        server.SetBytes(S7IsoArea.Merker, 0, 4, new byte[] { 0x23, 0x45 });
        var tag = S7IsoTransportTests.Tag(TagDataType.Int16);
        var point = new S7IsoPoint(tag, S7IsoArea.Merker, 4, S7IsoValueType.Int16);
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();
        await using var driver = new S7IsoDriver(
            "s7-address-invalid",
            "S7 Address Invalid",
            S7IsoTransportTests.Options(server.Port),
            cache,
            registry,
            new[] { point },
            TimeSpan.FromMilliseconds(200));

        await driver.StartAsync();
        await WaitUntilAsync(
            () => cache.TryGet(tag.Id, out var value) && value?.Quality == TagQuality.BadConfiguration,
            TimeSpan.FromSeconds(2));

        var failed = Assert.IsType<TagValue>((await driver.ReadAsync(tag.Id))!);
        Assert.Null(failed.Value);
        Assert.Equal(TagQuality.BadConfiguration, failed.Quality);
        var degraded = driver.GetCommunicationDiagnostics();
        Assert.Equal(CommunicationDriverOperationalState.Degraded, degraded.State);
        Assert.Equal(nameof(S7IsoFailureKind.AddressInvalid), degraded.ProtocolDetails!["lastFailureKind"]);
        Assert.Equal(1L, degraded.Counters.Connections);
        Assert.Equal(0L, degraded.Counters.Disconnections);
        Assert.Equal(0L, degraded.Counters.Reconnects);

        server.ReadReturnCode = S7IsoProtocol.ReturnCodeSuccess;
        await WaitUntilAsync(
            () => cache.TryGet(tag.Id, out var value) && value?.Quality == TagQuality.Good,
            TimeSpan.FromSeconds(2));

        var recovered = Assert.IsType<TagValue>((await driver.ReadAsync(tag.Id))!);
        Assert.Equal((short)0x2345, Assert.IsType<short>(recovered.Value));
        Assert.Equal(TagQuality.Good, recovered.Quality);
        var healthy = driver.GetCommunicationDiagnostics();
        Assert.Equal(CommunicationDriverOperationalState.Healthy, healthy.State);
        Assert.Equal(1L, healthy.Counters.Connections);
        Assert.Equal(0L, healthy.Counters.Reconnects);
    }

    [Fact]
    public async Task StringLayoutMismatch_IsBadConfigurationWithoutDroppingSessionAndRecoversInPlace()
    {
        await using var server = new TestS7IsoServer();
        var tag = S7IsoTransportTests.Tag(TagDataType.String);
        var point = new S7IsoPoint(
            tag,
            S7IsoArea.DataBlock,
            20,
            S7IsoValueType.String,
            DbNumber: 1,
            StringLength: 10);
        server.SetBytes(
            S7IsoArea.DataBlock,
            1,
            20,
            new byte[] { 8, 3, (byte)'A', (byte)'B', (byte)'C', 0, 0, 0, 0, 0, 0, 0 });

        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();
        await using var driver = new S7IsoDriver(
            "s7-string-layout",
            "S7 String Layout",
            S7IsoTransportTests.Options(server.Port),
            cache,
            registry,
            new[] { point },
            TimeSpan.FromMilliseconds(200));

        await driver.StartAsync();
        await WaitUntilAsync(
            () => cache.TryGet(tag.Id, out var value) && value?.Quality == TagQuality.BadConfiguration,
            TimeSpan.FromSeconds(2));

        var failed = Assert.IsType<TagValue>((await driver.ReadAsync(tag.Id))!);
        Assert.Null(failed.Value);
        Assert.Equal(TagQuality.BadConfiguration, failed.Quality);
        var degraded = driver.GetCommunicationDiagnostics();
        Assert.Equal(CommunicationDriverOperationalState.Degraded, degraded.State);
        Assert.Equal(1L, degraded.Counters.Connections);
        Assert.Equal(0L, degraded.Counters.Disconnections);
        Assert.Equal(0L, degraded.Counters.Reconnects);

        server.SetBytes(
            S7IsoArea.DataBlock,
            1,
            20,
            new byte[] { 10, 3, (byte)'A', (byte)'B', (byte)'C', 0, 0, 0, 0, 0, 0, 0 });
        await WaitUntilAsync(
            () => cache.TryGet(tag.Id, out var value) && value?.Quality == TagQuality.Good,
            TimeSpan.FromSeconds(2));

        var recovered = Assert.IsType<TagValue>((await driver.ReadAsync(tag.Id))!);
        Assert.Equal("ABC", Assert.IsType<string>(recovered.Value));
        Assert.Equal(TagQuality.Good, recovered.Quality);
        var healthy = driver.GetCommunicationDiagnostics();
        Assert.Equal(CommunicationDriverOperationalState.Healthy, healthy.State);
        Assert.Equal(1L, healthy.Counters.Connections);
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
