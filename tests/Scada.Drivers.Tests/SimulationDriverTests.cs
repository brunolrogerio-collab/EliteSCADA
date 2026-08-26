using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.Simulation;

namespace Scada.Drivers.Tests;

public sealed class SimulationDriverTests
{
    [Fact]
    public async Task Driver_CanStopAndRestartWithoutDuplicateTagRegistration()
    {
        var eventBus = new InMemoryScadaEventBus();
        var cache = new CurrentTagCache(eventBus);
        var registry = new InMemoryTagRegistry();
        var tag = TagDefinition.Create(
            "Fallback value",
            "Demo.Fallback.Value",
            TagDataType.Double,
            "builtin.simulation");

        await using var driver = new SimulationDriver(
            cache,
            registry,
            new[] { new SimulationPoint(tag, SimulationSignalType.Constant, ConstantValue: 42) },
            TimeSpan.FromMilliseconds(10));

        await driver.StartAsync();
        await WaitForAsync(() => cache.TryGet(tag.Id, out _), TimeSpan.FromSeconds(1));
        await driver.StopAsync();

        await driver.StartAsync();
        await WaitForAsync(
            () => driver.Status.State == Scada.Drivers.Abstractions.DriverState.Running && cache.TryGet(tag.Id, out _),
            TimeSpan.FromSeconds(1));

        Assert.Single(registry.Snapshot());
        Assert.Equal("Demo.Fallback.Value", registry.Snapshot().Single().Path);
    }

    private static async Task WaitForAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (predicate()) return;
            await Task.Delay(10);
        }

        Assert.True(predicate(), $"Condition was not met within {timeout}.");
    }
}
