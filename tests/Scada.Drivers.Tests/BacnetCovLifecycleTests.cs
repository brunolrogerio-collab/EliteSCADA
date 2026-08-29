using System.IO.BACnet;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.Bacnet;

namespace Scada.Drivers.Tests;

public sealed class BacnetCovLifecycleTests
{
    [Fact]
    public async Task ReachabilityRecovery_RecreatesManagedCovSubscriptionAndAsyncCancelsOldHandle()
    {
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();
        var tag = TagDefinition.Create("AI1", "Plant.Bacnet.Cov.AI1", TagDataType.Double, source: "bacnet-cov");
        var binding = new BacnetBinding(50, 0, 1, 85, UseCov: true);
        var point = new BacnetPoint(tag, binding);
        var session = new CovLifecycleSession(
            binding,
            subscribeOutcomes: new[] { true, true },
            ReadStep.Failure(new TimeoutException("device reboot")),
            ReadStep.Success(51.25f));
        await using var driver = new BacnetIpDriver(
            "bacnet-cov",
            "BACnet COV",
            cache,
            registry,
            new[] { point },
            session,
            scanRate: TimeSpan.FromMilliseconds(10),
            covFallbackPollInterval: TimeSpan.FromMilliseconds(25),
            covRecreationRetryInterval: TimeSpan.FromMilliseconds(20));

        await driver.StartAsync();
        var diagnostics = await WaitForDiagnosticsAsync(
            driver,
            snapshot => DetailLong(snapshot, "covRecreationAttempts") >= 1 &&
                        DetailLong(snapshot, "covTagCount") == 1 &&
                        snapshot.Counters.Reconnects >= 1,
            TimeSpan.FromSeconds(2));

        Assert.Equal(2, session.SubscribeCalls);
        Assert.Equal(1, session.AsyncDisposeCalls);
        Assert.Equal(0, session.SyncDisposeCalls);
        Assert.Equal("false", diagnostics.ProtocolDetails!["covRecreationPending"]);
        Assert.Equal("1", diagnostics.ProtocolDetails["covTagCount"]);
        Assert.Equal("1", diagnostics.ProtocolDetails["covManagedTagCount"]);
        Assert.Equal("2", diagnostics.ProtocolDetails["covSubscribeRequests"]);
        Assert.Equal("1", diagnostics.ProtocolDetails["covCancelRequests"]);

        await driver.StopAsync();
        Assert.Equal(2, session.AsyncDisposeCalls);
        Assert.Equal(0, session.SyncDisposeCalls);
    }

    [Fact]
    public async Task FailedCovRecreation_KeepsPollingAndRetriesUntilSubscriptionReturns()
    {
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();
        var tag = TagDefinition.Create("AI1", "Plant.Bacnet.CovRetry.AI1", TagDataType.Double, source: "bacnet-cov-retry");
        var binding = new BacnetBinding(60, 0, 1, 85, UseCov: true);
        var point = new BacnetPoint(tag, binding);
        var session = new CovLifecycleSession(
            binding,
            subscribeOutcomes: new[] { true, false, true },
            ReadStep.Failure(new TimeoutException("device reboot")),
            ReadStep.Success(61.0f));
        await using var driver = new BacnetIpDriver(
            "bacnet-cov-retry",
            "BACnet COV Retry",
            cache,
            registry,
            new[] { point },
            session,
            scanRate: TimeSpan.FromMilliseconds(10),
            covFallbackPollInterval: TimeSpan.FromMilliseconds(25),
            covRecreationRetryInterval: TimeSpan.FromMilliseconds(20));

        await driver.StartAsync();
        var diagnostics = await WaitForDiagnosticsAsync(
            driver,
            snapshot => DetailLong(snapshot, "covRecreationAttempts") >= 2 &&
                        DetailLong(snapshot, "covRecreationFailures") >= 1 &&
                        DetailLong(snapshot, "covTagCount") == 1,
            TimeSpan.FromSeconds(2));

        Assert.True(session.ReadCalls >= 3);
        Assert.Equal(3, session.SubscribeCalls);
        Assert.Equal(1, session.SubscribeFailures);
        Assert.Equal("false", diagnostics.ProtocolDetails!["covRecreationPending"]);
        Assert.Equal("1", diagnostics.ProtocolDetails["covTagCount"]);
        Assert.Equal("1", diagnostics.ProtocolDetails["covManagedTagCount"]);
        Assert.Equal("3", diagnostics.ProtocolDetails["covSubscribeRequests"]);
        Assert.Equal("1", diagnostics.ProtocolDetails["covSubscribeFailures"]);
    }

    [Fact]
    public async Task InitialCovRejection_RemainsPollingOnlyAndDoesNotInventManagedSubscription()
    {
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();
        var tag = TagDefinition.Create("AI1", "Plant.Bacnet.NoCov.AI1", TagDataType.Double, source: "bacnet-no-cov");
        var binding = new BacnetBinding(70, 0, 1, 85, UseCov: true);
        var point = new BacnetPoint(tag, binding);
        var session = new CovLifecycleSession(
            binding,
            subscribeOutcomes: new[] { false },
            ReadStep.Success(71.0f));
        await using var driver = new BacnetIpDriver(
            "bacnet-no-cov",
            "BACnet No COV",
            cache,
            registry,
            new[] { point },
            session,
            scanRate: TimeSpan.FromMilliseconds(10),
            covFallbackPollInterval: TimeSpan.FromMilliseconds(25),
            covRecreationRetryInterval: TimeSpan.FromMilliseconds(20));

        await driver.StartAsync();
        var diagnostics = await WaitForDiagnosticsAsync(
            driver,
            snapshot => snapshot.Counters.SuccessfulOperations >= 2,
            TimeSpan.FromSeconds(2));

        Assert.True(session.ReadCalls >= 2);
        Assert.Equal(1, session.SubscribeCalls);
        Assert.Equal("0", diagnostics.ProtocolDetails!["covTagCount"]);
        Assert.Equal("0", diagnostics.ProtocolDetails["covManagedTagCount"]);
        Assert.Equal("false", diagnostics.ProtocolDetails["covRecreationPending"]);
        Assert.Equal("1", diagnostics.ProtocolDetails["polledTagCount"]);
    }

    private static long DetailLong(Scada.Drivers.Abstractions.CommunicationDriverDiagnosticSnapshot snapshot, string key)
        => snapshot.ProtocolDetails is not null &&
           snapshot.ProtocolDetails.TryGetValue(key, out var text) &&
           long.TryParse(text, out var value)
            ? value
            : -1;

    private static async Task<Scada.Drivers.Abstractions.CommunicationDriverDiagnosticSnapshot> WaitForDiagnosticsAsync(
        BacnetIpDriver driver,
        Func<Scada.Drivers.Abstractions.CommunicationDriverDiagnosticSnapshot, bool> predicate,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        Scada.Drivers.Abstractions.CommunicationDriverDiagnosticSnapshot? latest = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            latest = driver.GetCommunicationDiagnostics();
            if (predicate(latest)) return latest;
            await Task.Delay(10);
        }

        return latest ?? driver.GetCommunicationDiagnostics();
    }

    private sealed record ReadStep(float? Value, Exception? Error)
    {
        public static ReadStep Success(float value) => new(value, null);
        public static ReadStep Failure(Exception error) => new(null, error);
    }

    private sealed class CovLifecycleSession : IBacnetSession, IBacnetCovSubscriptionDiagnostics
    {
        private readonly BacnetBinding _binding;
        private readonly bool[] _subscribeOutcomes;
        private readonly ReadStep[] _readSteps;
        private int _readIndex = -1;
        private int _subscribeIndex = -1;
        private int _activeSubscriptions;
        private int _subscribeCalls;
        private int _subscribeFailures;
        private int _asyncDisposeCalls;
        private int _syncDisposeCalls;

        public CovLifecycleSession(BacnetBinding binding, bool[] subscribeOutcomes, params ReadStep[] readSteps)
        {
            _binding = binding;
            _subscribeOutcomes = subscribeOutcomes;
            _readSteps = readSteps;
        }

        public int ReadCalls => Volatile.Read(ref _readIndex) + 1;
        public int SubscribeCalls => Volatile.Read(ref _subscribeCalls);
        public int SubscribeFailures => Volatile.Read(ref _subscribeFailures);
        public int AsyncDisposeCalls => Volatile.Read(ref _asyncDisposeCalls);
        public int SyncDisposeCalls => Volatile.Read(ref _syncDisposeCalls);

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
            var step = _readSteps[Math.Min(index, _readSteps.Length - 1)];
            if (step.Error is not null) return Task.FromException<BacnetPropertyReadResult>(step.Error);
            return Task.FromResult(new BacnetPropertyReadResult(
                _binding,
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
        {
            Interlocked.Increment(ref _subscribeCalls);
            var index = Interlocked.Increment(ref _subscribeIndex);
            var succeeds = _subscribeOutcomes[Math.Min(index, _subscribeOutcomes.Length - 1)];
            if (!succeeds)
            {
                Interlocked.Increment(ref _subscribeFailures);
                return Task.FromResult<IDisposable?>(null);
            }

            Interlocked.Increment(ref _activeSubscriptions);
            return Task.FromResult<IDisposable?>(new TrackingSubscription(this));
        }

        public BacnetCovSubscriptionSnapshot GetCovSubscriptionDiagnostics()
            => new(
                ActiveSubscriptions: Volatile.Read(ref _activeSubscriptions),
                SubscribeRequests: Volatile.Read(ref _subscribeCalls),
                SubscribeFailures: Volatile.Read(ref _subscribeFailures),
                CancelRequests: Volatile.Read(ref _asyncDisposeCalls),
                CancelFailures: 0,
                LastErrorType: null);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private sealed class TrackingSubscription(CovLifecycleSession owner) : IBacnetCovSubscription
        {
            private int _disposed;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
                Interlocked.Increment(ref owner._syncDisposeCalls);
                Interlocked.Decrement(ref owner._activeSubscriptions);
            }

            public ValueTask DisposeAsync()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0) return ValueTask.CompletedTask;
                Interlocked.Increment(ref owner._asyncDisposeCalls);
                Interlocked.Decrement(ref owner._activeSubscriptions);
                return ValueTask.CompletedTask;
            }
        }
    }
}
