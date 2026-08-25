using Scada.Core.Events;
using Scada.Core.Tags;

namespace Scada.Core.Tests;

public sealed class CurrentTagCacheTests
{
    [Fact]
    public async Task UpdateAsync_StoresCurrentValue_AndPublishesEvent()
    {
        var bus = new InMemoryScadaEventBus();
        var cache = new CurrentTagCache(bus);
        var tag = TagDefinition.Create("Current", "EEE01.P01.Current", TagDataType.Double, "simulation", "A");
        TagValueChanged? observed = null;

        using var subscription = bus.Subscribe<TagValueChanged>(e =>
        {
            observed = e;
            return ValueTask.CompletedTask;
        });

        var value = TagValue.Good(tag.Id, 38.4, "simulation");
        await cache.UpdateAsync(tag, value);

        Assert.True(cache.TryGet(tag.Id, out var cached));
        Assert.Equal(38.4, cached!.Value);
        Assert.NotNull(observed);
        Assert.Equal("EEE01.P01.Current", observed!.Tag.Path);
    }
}
