using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoOrderingRuntimeTests
{
    [Fact]
    public async Task ByteAndWordSwap_ReadsCanonicalValueAndWritesInversePhysicalOrder()
    {
        await using var server = new TestS7IsoServer();
        var tag = S7IsoTransportTests.Tag(TagDataType.Int32);
        var point = new S7IsoPoint(
            tag,
            S7IsoArea.DataBlock,
            100,
            S7IsoValueType.Int32,
            DbNumber: 3,
            Writable: true,
            ValueOrder: S7IsoValueOrder.ByteAndWordSwap);
        server.SetBytes(S7IsoArea.DataBlock, 3, 100, new byte[] { 0x44, 0x33, 0x22, 0x11 });

        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();
        await using var driver = new S7IsoDriver(
            "s7-order",
            "S7 Ordering",
            S7IsoTransportTests.Options(server.Port),
            cache,
            registry,
            new[] { point },
            TimeSpan.FromMilliseconds(20));

        await driver.StartAsync();
        await WaitUntilAsync(
            () => cache.TryGet(tag.Id, out var sample) && sample?.Quality == TagQuality.Good,
            TimeSpan.FromSeconds(2));

        var read = Assert.IsType<TagValue>((await driver.ReadAsync(tag.Id))!);
        Assert.Equal(0x11223344, Assert.IsType<int>(read.Value));

        await driver.WriteAsync(tag.Id, 0x55667788);

        Assert.Equal(
            new byte[] { 0x88, 0x77, 0x66, 0x55 },
            server.GetBytes(S7IsoArea.DataBlock, 3, 100, 4));
        var written = Assert.IsType<TagValue>((await driver.ReadAsync(tag.Id))!);
        Assert.Equal(0x55667788, Assert.IsType<int>(written.Value));
        Assert.Equal(TagQuality.Good, written.Quality);
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
