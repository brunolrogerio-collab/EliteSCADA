using System.Net;
using System.Net.Sockets;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoRuntimeReadinessTests
{
    [Fact]
    public async Task Readiness_IsNotStartedBeforeLifecycleBegins()
    {
        var tag = S7IsoTransportTests.Tag(TagDataType.Int16);
        var point = new S7IsoPoint(tag, S7IsoArea.Merker, 0, S7IsoValueType.Int16);
        await using var driver = new S7IsoDriver(
            "s7-readiness-not-started",
            "S7 Readiness Not Started",
            Options(GetUnusedPort()),
            new CurrentTagCache(new InMemoryScadaEventBus()),
            new InMemoryTagRegistry(),
            new[] { point },
            TimeSpan.FromMilliseconds(50));

        var source = Assert.IsAssignableFrom<IS7IsoRuntimeReadinessSource>(driver);
        var readiness = source.GetS7IsoRuntimeReadiness();

        Assert.Equal(S7IsoRuntimeReadinessState.NotStarted, readiness.State);
        Assert.Null(readiness.ReadyAt);
        Assert.Null(readiness.NegotiatedPduSizeAtReady);
        Assert.False(readiness.InitialAcquisitionCompleted);
        Assert.Equal(0L, readiness.InitialAcquisitionAttempts);
        Assert.Null(readiness.LastError);
    }

    [Fact]
    public async Task Readiness_BecomesReadyAfterSessionAndInitialAttemptEvenWithBadPoint()
    {
        await using var server = new TestS7IsoServer(240);
        server.SetBytes(S7IsoArea.Merker, 0, 0, new byte[] { 0x12, 0x34 });
        var goodTag = S7IsoTransportTests.Tag(TagDataType.Int16);
        var oversizedTag = S7IsoTransportTests.Tag(TagDataType.String);
        var goodPoint = new S7IsoPoint(goodTag, S7IsoArea.Merker, 0, S7IsoValueType.Int16);
        var oversizedPoint = new S7IsoPoint(
            oversizedTag,
            S7IsoArea.Merker,
            100,
            S7IsoValueType.String,
            StringLength: 254);
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        await using var driver = new S7IsoDriver(
            "s7-readiness-degraded",
            "S7 Readiness Degraded",
            Options(server.Port, reconnectDelay: TimeSpan.FromMilliseconds(300)),
            cache,
            new InMemoryTagRegistry(),
            new[] { goodPoint, oversizedPoint },
            TimeSpan.FromMilliseconds(50));

        await driver.StartAsync();
        await WaitUntilAsync(
            () =>
                driver.GetS7IsoRuntimeReadiness().State == S7IsoRuntimeReadinessState.Ready &&
                cache.TryGet(goodTag.Id, out var good) && good?.Quality == TagQuality.Good &&
                cache.TryGet(oversizedTag.Id, out var bad) && bad?.Quality == TagQuality.BadConfiguration,
            TimeSpan.FromSeconds(2));

        var readiness = driver.GetS7IsoRuntimeReadiness();
        Assert.Equal(S7IsoRuntimeReadinessState.Ready, readiness.State);
        Assert.True(readiness.InitialAcquisitionCompleted);
        Assert.Equal(1L, readiness.InitialAcquisitionAttempts);
        Assert.Equal((ushort)240, readiness.NegotiatedPduSizeAtReady!.Value);
        Assert.NotNull(readiness.ReadyAt);
        Assert.Null(readiness.LastError);
        Assert.Equal(CommunicationDriverOperationalState.Degraded, driver.GetCommunicationDiagnostics().State);

        server.DropActiveConnection();
        await WaitUntilAsync(
            () => driver.GetCommunicationDiagnostics().State == CommunicationDriverOperationalState.Reconnecting,
            TimeSpan.FromSeconds(2));

        var afterDrop = driver.GetS7IsoRuntimeReadiness();
        Assert.Equal(S7IsoRuntimeReadinessState.Ready, afterDrop.State);
        Assert.Equal((ushort)240, afterDrop.NegotiatedPduSizeAtReady!.Value);
        Assert.True(afterDrop.InitialAcquisitionCompleted);
        Assert.Equal(1L, afterDrop.InitialAcquisitionAttempts);
    }

    [Fact]
    public async Task Readiness_RemainsStartingWhenSessionCannotInitialize()
    {
        var tag = S7IsoTransportTests.Tag(TagDataType.Int16);
        var point = new S7IsoPoint(tag, S7IsoArea.Merker, 0, S7IsoValueType.Int16);
        await using var driver = new S7IsoDriver(
            "s7-readiness-unreachable",
            "S7 Readiness Unreachable",
            Options(GetUnusedPort(), connectTimeout: TimeSpan.FromMilliseconds(100)),
            new CurrentTagCache(new InMemoryScadaEventBus()),
            new InMemoryTagRegistry(),
            new[] { point },
            TimeSpan.FromMilliseconds(50));

        await driver.StartAsync();
        await WaitUntilAsync(
            () =>
                driver.GetCommunicationDiagnostics().State == CommunicationDriverOperationalState.Reconnecting &&
                !string.IsNullOrWhiteSpace(driver.GetS7IsoRuntimeReadiness().LastError),
            TimeSpan.FromSeconds(2));

        var readiness = driver.GetS7IsoRuntimeReadiness();
        Assert.Equal(S7IsoRuntimeReadinessState.Starting, readiness.State);
        Assert.Null(readiness.ReadyAt);
        Assert.Null(readiness.NegotiatedPduSizeAtReady);
        Assert.False(readiness.InitialAcquisitionCompleted);
        Assert.Equal(0L, readiness.InitialAcquisitionAttempts);

        await driver.StopAsync();
        Assert.Equal(S7IsoRuntimeReadinessState.Stopped, driver.GetS7IsoRuntimeReadiness().State);
    }

    private static S7IsoConnectionOptions Options(
        int port,
        TimeSpan? connectTimeout = null,
        TimeSpan? reconnectDelay = null) => new(
        "127.0.0.1",
        S7CpuFamily.S71500,
        S7IsoConnectionMode.RackSlot,
        rack: 0,
        slot: 1,
        connectionRole: S7IsoConnectionRole.Basic,
        port: port,
        connectTimeout: connectTimeout ?? TimeSpan.FromMilliseconds(500),
        requestTimeout: TimeSpan.FromMilliseconds(250),
        reconnectDelay: reconnectDelay ?? TimeSpan.FromMilliseconds(50));

    private static int GetUnusedPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
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
