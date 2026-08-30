using System.IO.BACnet;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.Drivers.Bacnet;

namespace Scada.Drivers.Tests;

public sealed class BacnetRuntimeFactoryTests
{
    [Fact]
    public async Task Factory_CreatesDriverFromRegistryReadyPlanAndHostServices()
    {
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();
        var tag = TagDefinition.Create(
            "AI1",
            "Plant.Bacnet.Factory.AI1",
            TagDataType.Double,
            source: "bacnet-factory");
        var point = new BacnetPoint(tag, new BacnetBinding(1201, 0, 37, 85, UseCov: false));
        var options = new BacnetSessionOptions(
            LocalPort: 47809,
            RequestTimeout: TimeSpan.FromSeconds(2),
            TargetAddress: "192.0.2.10:47808");
        var plan = new BacnetIpRuntimePlan(
            "bacnet-factory",
            "BACnet Factory",
            options,
            TimeSpan.FromMilliseconds(750),
            new[] { point });
        var sessions = new TrackingSessionFactory();
        var factory = new BacnetIpRuntimeFactory(sessions);

        await using var driver = factory.Create(plan, cache, registry);

        Assert.Equal(BacnetDriverDescriptor.DriverType, factory.DriverType);
        Assert.Equal(BacnetDriverDescriptor.DriverType, plan.DriverType);
        Assert.Equal(options, sessions.LastOptions);
        Assert.Equal(1, sessions.CreateCalls);
        Assert.Equal(plan.DataSourceKey, driver.DriverId);
        Assert.Equal(plan.Name, driver.Name);
        Assert.Equal(plan.ScanRate, driver.ScanRate);
        Assert.Equal(1201u, driver.DeviceInstance);
        Assert.Equal(new[] { tag.Id }, driver.Tags.Select(x => x.Id));
    }

    [Fact]
    public void Factory_RejectsEmptyPlanBeforeAllocatingSession()
    {
        var sessions = new TrackingSessionFactory();
        var factory = new BacnetIpRuntimeFactory(sessions);
        var plan = new BacnetIpRuntimePlan(
            "bacnet-empty",
            "BACnet Empty",
            new BacnetSessionOptions(),
            TimeSpan.FromSeconds(1),
            Array.Empty<BacnetPoint>());
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();

        Assert.Throws<ArgumentException>(() => factory.Create(plan, cache, registry));
        Assert.Equal(0, sessions.CreateCalls);
    }

    private sealed class TrackingSessionFactory : IBacnetSessionFactory
    {
        public int CreateCalls { get; private set; }
        public BacnetSessionOptions? LastOptions { get; private set; }

        public IBacnetSession Create(BacnetSessionOptions options)
        {
            CreateCalls++;
            LastOptions = options;
            return new StubSession();
        }
    }

    private sealed class StubSession : IBacnetSession
    {
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<BacnetDeviceObservation> ResolveDeviceAsync(
            uint deviceInstance,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public async IAsyncEnumerable<BacnetDeviceObservation> DiscoverAsync(
            int? maximumResults = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<BacnetPropertyReadResult> ReadAsync(
            BacnetBinding binding,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task WriteAsync(
            BacnetBinding binding,
            IReadOnlyCollection<BacnetValue> values,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IDisposable?> TrySubscribeCovAsync(
            BacnetBinding binding,
            Func<BacnetPropertyReadResult, ValueTask> onNotification,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IDisposable?>(null);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
