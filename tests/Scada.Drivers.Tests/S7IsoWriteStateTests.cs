using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoWriteStateTests
{
    [Fact]
    public async Task SuccessfulWrite_DoesNotMaskUnrelatedBadConfigurationQuality()
    {
        await using var server = new TestS7IsoServer(240);
        server.SetBytes(S7IsoArea.Merker, 0, 0, new byte[] { 0x00, 0x2A });

        var writableTag = S7IsoTransportTests.Tag(TagDataType.Int16);
        var oversizedTag = S7IsoTransportTests.Tag(TagDataType.String);
        var writable = new S7IsoPoint(
            writableTag,
            S7IsoArea.Merker,
            0,
            S7IsoValueType.Int16,
            Writable: true);
        var oversized = new S7IsoPoint(
            oversizedTag,
            S7IsoArea.DataBlock,
            0,
            S7IsoValueType.String,
            DbNumber: 1,
            StringLength: 254);

        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();
        await using var driver = new S7IsoDriver(
            "s7-write-state",
            "S7 Write State",
            S7IsoTransportTests.Options(server.Port),
            cache,
            registry,
            new[] { writable, oversized },
            TimeSpan.FromSeconds(2));

        await driver.StartAsync();
        await WaitUntilAsync(
            () =>
            {
                var diagnostics = driver.GetCommunicationDiagnostics();
                return diagnostics.State == CommunicationDriverOperationalState.Degraded &&
                       diagnostics.TagQuality.Good == 1 &&
                       diagnostics.TagQuality.BadConfiguration == 1;
            },
            TimeSpan.FromSeconds(2));

        await driver.WriteAsync(writableTag.Id, (short)1234);

        var afterWrite = driver.GetCommunicationDiagnostics();
        Assert.Equal(CommunicationDriverOperationalState.Degraded, afterWrite.State);
        Assert.Equal(1, afterWrite.TagQuality.Good);
        Assert.Equal(1, afterWrite.TagQuality.BadConfiguration);
        Assert.Equal(new byte[] { 0x04, 0xD2 }, server.GetBytes(S7IsoArea.Merker, 0, 0, 2));
        var sample = Assert.IsType<TagValue>((await driver.ReadAsync(writableTag.Id))!);
        Assert.Equal(TagQuality.Good, sample.Quality);
        Assert.Equal((short)1234, Assert.IsType<short>(sample.Value));
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
