using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoDriverTests
{
    [Fact]
    public async Task PollAndWrite_PublishCanonicalValuesQualityAndDiagnostics()
    {
        await using var server = new TestS7IsoServer();
        server.SetBytes(S7IsoArea.DataBlock, 1, 0, new byte[] { 0x12, 0x34 });
        var tag = S7IsoTransportTests.Tag(TagDataType.Int16);
        var point = new S7IsoPoint(
            tag,
            S7IsoArea.DataBlock,
            0,
            S7IsoValueType.Int16,
            DbNumber: 1,
            Writable: true);
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();
        await using var driver = new S7IsoDriver(
            "s7-test",
            "S7 Test",
            S7IsoTransportTests.Options(server.Port),
            cache,
            registry,
            new[] { point },
            TimeSpan.FromMilliseconds(20));

        await driver.StartAsync();
        await WaitUntilAsync(() => cache.TryGet(tag.Id, out var sample) && sample?.Quality == TagQuality.Good, TimeSpan.FromSeconds(2));
        var initial = Assert.IsType<TagValue>((await driver.ReadAsync(tag.Id))!);
        Assert.Equal((short)0x1234, Assert.IsType<short>(initial.Value));
        Assert.Null(initial.SourceTimestamp);
        Assert.Null(initial.ServerTimestamp);

        await driver.WriteAsync(tag.Id, (short)0x4567);
        Assert.Equal(new byte[] { 0x45, 0x67 }, server.GetBytes(S7IsoArea.DataBlock, 1, 0, 2));
        var written = Assert.IsType<TagValue>((await driver.ReadAsync(tag.Id))!);
        Assert.Equal((short)0x4567, Assert.IsType<short>(written.Value));
        Assert.Equal(TagQuality.Good, written.Quality);

        var diagnostics = driver.GetCommunicationDiagnostics();
        Assert.Equal("siemens.s7.iso", diagnostics.DriverType);
        Assert.Equal(CommunicationDriverOperationalState.Healthy, diagnostics.State);
        Assert.Equal("480", diagnostics.ProtocolDetails!["negotiatedPduSize"]);
        Assert.Equal("true", diagnostics.ProtocolDetails["writeEnabled"]);
        Assert.True(diagnostics.Counters.Requests >= 2);
        Assert.True(diagnostics.Counters.ReadOperations >= 1);
        Assert.Equal(1L, diagnostics.Counters.WriteOperations);

        await driver.StopAsync();
        Assert.Equal(DriverState.Stopped, driver.Status.State);
    }

    [Fact]
    public async Task WritePolicy_DisabledByDefaultRejectsWriteWithoutCorruptingReadQuality()
    {
        await using var server = new TestS7IsoServer();
        server.SetBytes(S7IsoArea.DataBlock, 1, 0, new byte[] { 0x12, 0x34 });
        var tag = S7IsoTransportTests.Tag(TagDataType.Int16);
        var point = new S7IsoPoint(
            tag,
            S7IsoArea.DataBlock,
            0,
            S7IsoValueType.Int16,
            DbNumber: 1,
            Writable: true);
        var options = new S7IsoConnectionOptions(
            "127.0.0.1",
            S7CpuFamily.S71500,
            S7IsoConnectionMode.RackSlot,
            rack: 0,
            slot: 1,
            connectionRole: S7IsoConnectionRole.Basic,
            port: server.Port,
            reconnectDelay: TimeSpan.Zero);
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();
        await using var driver = new S7IsoDriver(
            "s7-readonly-policy",
            "S7 Readonly Policy",
            options,
            cache,
            registry,
            new[] { point },
            TimeSpan.FromMilliseconds(20));

        await driver.StartAsync();
        await WaitUntilAsync(() => cache.TryGet(tag.Id, out var sample) && sample?.Quality == TagQuality.Good, TimeSpan.FromSeconds(2));

        var rejected = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await driver.WriteAsync(tag.Id, (short)0x4567);
        });
        Assert.Contains("writeEnabled", rejected.Message, StringComparison.Ordinal);

        Assert.Equal(new byte[] { 0x12, 0x34 }, server.GetBytes(S7IsoArea.DataBlock, 1, 0, 2));
        var current = Assert.IsType<TagValue>((await driver.ReadAsync(tag.Id))!);
        Assert.Equal((short)0x1234, Assert.IsType<short>(current.Value));
        Assert.Equal(TagQuality.Good, current.Quality);

        var diagnostics = driver.GetCommunicationDiagnostics();
        Assert.Equal("false", diagnostics.ProtocolDetails!["writeEnabled"]);
        Assert.Equal(0L, diagnostics.Counters.WriteOperations);
    }

    [Fact]
    public async Task UnreachableEndpoint_PublishesBadCommunicationInsteadOfFakeZero()
    {
        var tag = S7IsoTransportTests.Tag(TagDataType.Int16);
        var point = new S7IsoPoint(tag, S7IsoArea.Merker, 0, S7IsoValueType.Int16);
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();
        var options = new S7IsoConnectionOptions(
            "127.0.0.1",
            S7CpuFamily.S7300,
            S7IsoConnectionMode.RackSlot,
            rack: 0,
            slot: 2,
            connectionRole: S7IsoConnectionRole.Basic,
            port: 1,
            connectTimeout: TimeSpan.FromMilliseconds(100),
            requestTimeout: TimeSpan.FromMilliseconds(100),
            reconnectDelay: TimeSpan.Zero);
        await using var driver = new S7IsoDriver(
            "s7-down",
            "S7 Down",
            options,
            cache,
            registry,
            new[] { point },
            TimeSpan.FromMilliseconds(20));

        await driver.StartAsync();
        await WaitUntilAsync(() => cache.TryGet(tag.Id, out var sample) && sample?.Quality == TagQuality.BadCommunication, TimeSpan.FromSeconds(2));
        var failed = Assert.IsType<TagValue>((await driver.ReadAsync(tag.Id))!);

        Assert.Null(failed.Value);
        Assert.Equal(TagQuality.BadCommunication, failed.Quality);
        Assert.Equal(CommunicationDriverOperationalState.Reconnecting, driver.GetCommunicationDiagnostics().State);
    }

    [Fact]
    public async Task OversizedBinding_PublishesBadConfigurationWithoutFakeReconnect()
    {
        await using var server = new TestS7IsoServer(240);
        var tag = S7IsoTransportTests.Tag(TagDataType.String);
        var point = new S7IsoPoint(
            tag,
            S7IsoArea.DataBlock,
            0,
            S7IsoValueType.String,
            DbNumber: 1,
            Writable: true,
            StringLength: 254);
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();
        await using var driver = new S7IsoDriver(
            "s7-pdu",
            "S7 PDU",
            S7IsoTransportTests.Options(server.Port),
            cache,
            registry,
            new[] { point },
            TimeSpan.FromMilliseconds(20));

        await driver.StartAsync();
        await WaitUntilAsync(
            () => cache.TryGet(tag.Id, out var sample) && sample?.Quality == TagQuality.BadConfiguration,
            TimeSpan.FromSeconds(2));

        var polled = Assert.IsType<TagValue>((await driver.ReadAsync(tag.Id))!);
        Assert.Null(polled.Value);
        Assert.Equal(TagQuality.BadConfiguration, polled.Quality);
        Assert.Equal(CommunicationDriverOperationalState.Degraded, driver.GetCommunicationDiagnostics().State);

        await Assert.ThrowsAsync<S7IsoConfigurationException>(async () =>
        {
            await driver.WriteAsync(tag.Id, "TEST");
        });

        var written = Assert.IsType<TagValue>((await driver.ReadAsync(tag.Id))!);
        Assert.Equal(TagQuality.BadConfiguration, written.Quality);
        var diagnostics = driver.GetCommunicationDiagnostics();
        Assert.Equal(CommunicationDriverOperationalState.Degraded, diagnostics.State);
        Assert.Equal(0L, diagnostics.Counters.Requests);
        Assert.True(diagnostics.Counters.FailedOperations >= 2);
    }

    [Fact]
    public async Task MixedPduCompatibility_KeepsHealthyTagGoodWhileOversizedTagIsBadConfiguration()
    {
        await using var server = new TestS7IsoServer(240);
        server.SetBytes(S7IsoArea.DataBlock, 1, 300, new byte[] { 0x23, 0x45 });

        var healthyTag = S7IsoTransportTests.Tag(TagDataType.Int16);
        var oversizedTag = S7IsoTransportTests.Tag(TagDataType.String);
        var healthyPoint = new S7IsoPoint(
            healthyTag,
            S7IsoArea.DataBlock,
            300,
            S7IsoValueType.Int16,
            DbNumber: 1);
        var oversizedPoint = new S7IsoPoint(
            oversizedTag,
            S7IsoArea.DataBlock,
            0,
            S7IsoValueType.String,
            DbNumber: 1,
            StringLength: 254);

        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();
        await using var driver = new S7IsoDriver(
            "s7-pdu-mixed",
            "S7 PDU Mixed",
            S7IsoTransportTests.Options(server.Port),
            cache,
            registry,
            new[] { oversizedPoint, healthyPoint },
            TimeSpan.FromMilliseconds(20));

        await driver.StartAsync();
        await WaitUntilAsync(
            () =>
                cache.TryGet(healthyTag.Id, out var healthy) && healthy?.Quality == TagQuality.Good &&
                cache.TryGet(oversizedTag.Id, out var oversized) && oversized?.Quality == TagQuality.BadConfiguration,
            TimeSpan.FromSeconds(2));

        var healthyValue = Assert.IsType<TagValue>((await driver.ReadAsync(healthyTag.Id))!);
        var oversizedValue = Assert.IsType<TagValue>((await driver.ReadAsync(oversizedTag.Id))!);
        Assert.Equal((short)0x2345, Assert.IsType<short>(healthyValue.Value));
        Assert.Equal(TagQuality.Good, healthyValue.Quality);
        Assert.Null(oversizedValue.Value);
        Assert.Equal(TagQuality.BadConfiguration, oversizedValue.Quality);

        var diagnostics = driver.GetCommunicationDiagnostics();
        Assert.Equal(CommunicationDriverOperationalState.Degraded, diagnostics.State);
        Assert.True(diagnostics.Counters.Requests >= 1);
        Assert.True(diagnostics.Counters.SuccessfulOperations >= 1);
        Assert.True(diagnostics.Counters.FailedOperations >= 1);
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
