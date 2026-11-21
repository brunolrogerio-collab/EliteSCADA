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
    [Fact]
    public async Task Publication_KeepsItsSubscribersWhileSubscriptionsChange()
    {
        var bus = new InMemoryScadaEventBus();
        var cache = new CurrentTagCache(bus);
        var tag = TagDefinition.Create("Current", "Plant.Current", TagDataType.Double);
        var value = TagValue.Good(tag.Id, 1.0, "simulation");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        async ValueTask Observe(TagValueChanged evt)
        {
            Interlocked.Increment(ref calls);
            entered.TrySetResult();
            await resume.Task;
        }

        using var first = bus.Subscribe<TagValueChanged>(Observe);
        using var second = bus.Subscribe<TagValueChanged>(Observe);
        var pending = cache.UpdateAsync(tag, value);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        first.Dispose();
        first.Dispose();
        var laterCalls = 0;
        using var later = bus.Subscribe<TagValueChanged>(_ =>
        {
            laterCalls++;
            return ValueTask.CompletedTask;
        });
        resume.SetResult();
        await pending;
        Assert.Equal(2, calls);
        Assert.Equal(0, laterCalls);
        await cache.UpdateAsync(tag, value);
        Assert.Equal(3, calls);
        Assert.Equal(1, laterCalls);
    }
}
