using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoWriteFailureSemanticsTests
{
    [Fact]
    public async Task ProtectionDeniedWrite_DoesNotCorruptLastReadQualityOrDropSession()
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
            "s7-write-denied",
            "S7 Write Denied",
            S7IsoTransportTests.Options(server.Port),
            cache,
            registry,
            new[] { point },
            TimeSpan.FromSeconds(10));

        await driver.StartAsync();
        await WaitUntilAsync(
            () => cache.TryGet(tag.Id, out var value) && value?.Quality == TagQuality.Good,
            TimeSpan.FromSeconds(2));
        server.WriteReturnCode = 0x03;

        var error = await Assert.ThrowsAsync<S7IsoProtocolException>(async () =>
            await driver.WriteAsync(tag.Id, (short)0x4567));

        Assert.Equal((byte)0x03, error.ReturnCode);
        Assert.Equal(new byte[] { 0x12, 0x34 }, server.GetBytes(S7IsoArea.DataBlock, 1, 0, 2));
        var current = Assert.IsType<TagValue>((await driver.ReadAsync(tag.Id))!);
        Assert.Equal((short)0x1234, Assert.IsType<short>(current.Value));
        Assert.Equal(TagQuality.Good, current.Quality);

        var diagnostics = driver.GetCommunicationDiagnostics();
        Assert.Equal(CommunicationDriverOperationalState.Degraded, diagnostics.State);
        Assert.Equal(nameof(S7IsoFailureKind.ProtectionDenied), diagnostics.ProtocolDetails!["lastFailureKind"]);
        Assert.Equal(1L, diagnostics.Counters.Connections);
        Assert.Equal(0L, diagnostics.Counters.Disconnections);
        Assert.True(diagnostics.Counters.FailedOperations >= 1);
        Assert.Equal(1L, diagnostics.Counters.WriteOperations);
        Assert.Equal(1, diagnostics.TagQuality.Good);
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