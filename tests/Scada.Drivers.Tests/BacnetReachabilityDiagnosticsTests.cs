using System.IO.BACnet;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Bacnet;

namespace Scada.Drivers.Tests;

public sealed class BacnetReachabilityDiagnosticsTests
{
    [Fact]
    public async Task DriverDiagnostics_CountLogicalDisconnectAndReconnectAfterTimeoutRecovery()
    {
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();
        var tag = TagDefinition.Create("AI1", "Plant.Bacnet.AI1", TagDataType.Double, source: "bacnet-reachability");
        var binding = new BacnetBinding(10, 0, 1, 85, UseCov: false);
        var point = new BacnetPoint(tag, binding);
        var session = new SequencedSession(
            binding,
            ReadStep.Success(11.5f),
            ReadStep.Failure(new TimeoutException("device timeout")),
            ReadStep.Success(12.5f));
        await using var driver = new BacnetIpDriver(
            "bacnet-reachability",
            "BACnet Reachability",
            cache,
            registry,
            new[] { point },
            session,
            scanRate: TimeSpan.FromMilliseconds(15));

        await driver.StartAsync();
        var diagnostics = await WaitForDiagnosticsAsync(
            driver,
            snapshot => snapshot.Counters.Reconnects >= 1 && snapshot.State == CommunicationDriverOperationalState.Healthy,
            TimeSpan.FromSeconds(2));

        Assert.Equal(2, diagnostics.Counters.Connections);
        Assert.Equal(1, diagnostics.Counters.Disconnections);
        Assert.Equal(1, diagnostics.Counters.Reconnects);
        Assert.Equal(1, diagnostics.Counters.Timeouts);
        Assert.Equal(CommunicationDriverOperationalState.Healthy, diagnostics.State);
        Assert.NotNull(diagnostics.LastSuccessfulCommunicationAt);
        Assert.NotNull(diagnostics.LastFailedCommunicationAt);
        Assert.NotNull(diagnostics.ProtocolDetails);
        Assert.Equal("device-reachability", diagnostics.ProtocolDetails!["connectionModel"]);
        Assert.Equal("true", diagnostics.ProtocolDetails["deviceReachable"]);
        Assert.True(diagnostics.ProtocolDetails.ContainsKey("lastReachabilityEstablishedAtUtc"));
        Assert.True(diagnostics.ProtocolDetails.ContainsKey("lastReachabilityLostAtUtc"));
    }

    [Fact]
    public async Task DriverDiagnostics_InitialTimeoutThenFirstSuccessIsConnectionNotReconnect()
    {
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();
        var tag = TagDefinition.Create("AI1", "Plant.Bacnet.Initial.AI1", TagDataType.Double, source: "bacnet-initial");
        var binding = new BacnetBinding(20, 0, 1, 85, UseCov: false);
        var point = new BacnetPoint(tag, binding);
        var session = new SequencedSession(
            binding,
            ReadStep.Failure(new TimeoutException("initial timeout")),
            ReadStep.Success(21.0f));
        await using var driver = new BacnetIpDriver(
            "bacnet-initial",
            "BACnet Initial Reachability",
            cache,
            registry,
            new[] { point },
            session,
            scanRate: TimeSpan.FromMilliseconds(15));

        await driver.StartAsync();
        var diagnostics = await WaitForDiagnosticsAsync(
            driver,
            snapshot => snapshot.Counters.Connections >= 1 && snapshot.Counters.SuccessfulOperations >= 1,
            TimeSpan.FromSeconds(2));

        Assert.Equal(1, diagnostics.Counters.Connections);
        Assert.Equal(0, diagnostics.Counters.Disconnections);
        Assert.Equal(0, diagnostics.Counters.Reconnects);
        Assert.Equal("true", diagnostics.ProtocolDetails!["deviceReachable"]);
    }

    [Fact]
    public async Task DriverDiagnostics_ProtocolFailureDoesNotInventReachabilityLoss()
    {
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();
        var tag = TagDefinition.Create("AI1", "Plant.Bacnet.Protocol.AI1", TagDataType.Double, source: "bacnet-protocol");
        var binding = new BacnetBinding(30, 0, 1, 85, UseCov: false);
        var point = new BacnetPoint(tag, binding);
        var session = new SequencedSession(
            binding,
            ReadStep.Success(31.0f),
            ReadStep.Failure(new InvalidOperationException("BACnet property rejected")),
            ReadStep.Success(32.0f));
        await using var driver = new BacnetIpDriver(
            "bacnet-protocol",
            "BACnet Protocol Failure",
            cache,
            registry,
            new[] { point },
            session,
            scanRate: TimeSpan.FromMilliseconds(15));

        await driver.StartAsync();
        var diagnostics = await WaitForDiagnosticsAsync(
            driver,
            snapshot => snapshot.Counters.FailedOperations >= 1 && snapshot.Counters.SuccessfulOperations >= 2,
            TimeSpan.FromSeconds(2));

        Assert.Equal(1, diagnostics.Counters.Connections);
        Assert.Equal(0, diagnostics.Counters.Disconnections);
        Assert.Equal(0, diagnostics.Counters.Reconnects);
        Assert.Equal("true", diagnostics.ProtocolDetails!["deviceReachable"]);
    }

    private static async Task<CommunicationDriverDiagnosticSnapshot> WaitForDiagnosticsAsync(
        BacnetIpDriver driver,
        Func<CommunicationDriverDiagnosticSnapshot, bool> predicate,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        CommunicationDriverDiagnosticSnapshot? latest = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            latest = driver.GetCommunicationDiagnostics();
            if (predicate(latest)) return latest;
            await Task.Delay(15);
        }

        return latest ?? driver.GetCommunicationDiagnostics();
    }

    private sealed record ReadStep(float? Value, Exception? Error)
    {
        public static ReadStep Success(float value) => new(value, null);
        public static ReadStep Failure(Exception error) => new(null, error);
    }

    private sealed class SequencedSession(BacnetBinding binding, params ReadStep[] steps) : IBacnetSession
    {
        private int _readIndex = -1;

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<BacnetDeviceObservation> ResolveDeviceAsync(uint deviceInstance, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public async IAsyncEnumerable<BacnetDeviceObservation> DiscoverAsync(
            int? maximumResults = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<BacnetPropertyReadResult> ReadAsync(BacnetBinding requestedBinding, CancellationToken cancellationToken = default)
        {
            var index = Interlocked.Increment(ref _readIndex);
            var step = steps[Math.Min(index, steps.Length - 1)];
            if (step.Error is not null) return Task.FromException<BacnetPropertyReadResult>(step.Error);
            return Task.FromResult(new BacnetPropertyReadResult(
                binding,
                new[] { new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, step.Value!.Value) },
                DateTimeOffset.UtcNow,
                new BacnetObjectState(Reliability: 0),
                UsedReadPropertyMultiple: true));
        }

        public Task WriteAsync(BacnetBinding binding, IReadOnlyCollection<BacnetValue> values, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IDisposable?> TrySubscribeCovAsync(
            BacnetBinding binding,
            Func<BacnetPropertyReadResult, ValueTask> onNotification,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IDisposable?>(null);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
