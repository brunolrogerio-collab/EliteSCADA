using System.IO.BACnet;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Bacnet;

namespace Scada.Drivers.Tests;

public sealed class BacnetCovDiagnosticsBreakdownTests
{
    private static readonly DateTimeOffset RenewalRequestAt = new(2026, 8, 30, 1, 45, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RenewalFailureAt = RenewalRequestAt.AddSeconds(1);
    private static readonly DateTimeOffset NextRenewalAt = RenewalRequestAt.AddSeconds(30);

    [Fact]
    public async Task Diagnostics_SeparateInitialRecreationAndRenewalTraffic()
    {
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();
        var tag = TagDefinition.Create("AI1", "Plant.Bacnet.CovDiagnostics.AI1", TagDataType.Double, source: "bacnet-cov-diagnostics");
        var binding = new BacnetBinding(80, 0, 1, 85, UseCov: true);
        var point = new BacnetPoint(tag, binding);
        await using var session = new DiagnosticCovSession(
            binding,
            subscribeOutcomes: new[] { true, true },
            firstReadError: new TimeoutException("device reboot"),
            backgroundRenewalRequests: 3,
            backgroundRenewalFailures: 1);
        await using var driver = new BacnetIpDriver(
            "bacnet-cov-diagnostics",
            "BACnet COV Diagnostics",
            cache,
            registry,
            new[] { point },
            session,
            scanRate: TimeSpan.FromMilliseconds(5),
            covFallbackPollInterval: TimeSpan.FromMilliseconds(10),
            covRecreationRetryInterval: TimeSpan.FromMilliseconds(5));

        await driver.StartAsync();
        var diagnostics = await WaitForDiagnosticsAsync(
            driver,
            snapshot => DetailLong(snapshot, "covRecreationSubscribeRequests") >= 1 &&
                        DetailLong(snapshot, "covTagCount") == 1 &&
                        snapshot.Counters.Reconnects >= 1,
            TimeSpan.FromSeconds(2));

        Assert.Equal("1", diagnostics.ProtocolDetails!["covInitialCreateAttempts"]);
        Assert.Equal("0", diagnostics.ProtocolDetails["covInitialCreateFailures"]);
        Assert.Equal("1", diagnostics.ProtocolDetails["covInitialSubscribeRequests"]);
        Assert.Equal("0", diagnostics.ProtocolDetails["covInitialSubscribeFailures"]);
        Assert.Equal("1", diagnostics.ProtocolDetails["covRecreationCreateAttempts"]);
        Assert.Equal("0", diagnostics.ProtocolDetails["covRecreationCreateFailures"]);
        Assert.Equal("1", diagnostics.ProtocolDetails["covRecreationSubscribeRequests"]);
        Assert.Equal("0", diagnostics.ProtocolDetails["covRecreationSubscribeFailures"]);
        Assert.Equal("1", diagnostics.ProtocolDetails["covRecreationAttempts"]);
        Assert.Equal("0", diagnostics.ProtocolDetails["covRecreationFailures"]);
        Assert.Equal("5", diagnostics.ProtocolDetails["covSubscribeRequests"]);
        Assert.Equal("1", diagnostics.ProtocolDetails["covSubscribeFailures"]);
        Assert.Equal("3", diagnostics.ProtocolDetails["covRenewalRequests"]);
        Assert.Equal("1", diagnostics.ProtocolDetails["covRenewalFailures"]);
        Assert.Equal("1", diagnostics.ProtocolDetails["covRenewalTrackedRoutes"]);
        Assert.Equal("300", diagnostics.ProtocolDetails["covSubscriptionLifetimeSeconds"]);
        Assert.Equal("225", diagnostics.ProtocolDetails["covRenewalIntervalSeconds"]);
        Assert.Equal("30", diagnostics.ProtocolDetails["covRenewalRetrySeconds"]);
        Assert.Equal(RenewalRequestAt.ToString("O"), diagnostics.ProtocolDetails["covLastRenewalRequestAtUtc"]);
        Assert.Equal(NextRenewalAt.ToString("O"), diagnostics.ProtocolDetails["covNextRenewalAttemptAtUtc"]);
        Assert.Equal(RenewalFailureAt.ToString("O"), diagnostics.ProtocolDetails["covLastRenewalFailureAtUtc"]);
        Assert.Equal(nameof(TimeoutException), diagnostics.ProtocolDetails["covLastRenewalErrorType"]);

        var sessionDiagnostics = session.GetCovSubscriptionDiagnostics();
        var route = Assert.Single(sessionDiagnostics.Routes!);
        Assert.Equal(binding.PortableAddress, route.PortableAddress);
        Assert.Equal(3, route.RenewalRequests);
        Assert.Equal(1, route.RenewalFailures);
        Assert.Equal(NextRenewalAt, route.NextRenewalAttemptAt);

        await driver.StopAsync();
    }

    [Fact]
    public async Task Diagnostics_KeepInitialRejectionOutOfRenewalAndRecreationCounters()
    {
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();
        var tag = TagDefinition.Create("AI1", "Plant.Bacnet.CovRejected.AI1", TagDataType.Double, source: "bacnet-cov-rejected");
        var binding = new BacnetBinding(81, 0, 1, 85, UseCov: true);
        var point = new BacnetPoint(tag, binding);
        await using var session = new DiagnosticCovSession(
            binding,
            subscribeOutcomes: new[] { false });
        await using var driver = new BacnetIpDriver(
            "bacnet-cov-rejected",
            "BACnet COV Rejected",
            cache,
            registry,
            new[] { point },
            session,
            scanRate: TimeSpan.FromMilliseconds(5),
            covFallbackPollInterval: TimeSpan.FromMilliseconds(10),
            covRecreationRetryInterval: TimeSpan.FromMilliseconds(5));

        await driver.StartAsync();
        var diagnostics = await WaitForDiagnosticsAsync(
            driver,
            snapshot => snapshot.Counters.SuccessfulOperations >= 1,
            TimeSpan.FromSeconds(2));

        Assert.Equal("1", diagnostics.ProtocolDetails!["covInitialCreateAttempts"]);
        Assert.Equal("1", diagnostics.ProtocolDetails["covInitialCreateFailures"]);
        Assert.Equal("1", diagnostics.ProtocolDetails["covInitialSubscribeRequests"]);
        Assert.Equal("1", diagnostics.ProtocolDetails["covInitialSubscribeFailures"]);
        Assert.Equal("0", diagnostics.ProtocolDetails["covRecreationCreateAttempts"]);
        Assert.Equal("0", diagnostics.ProtocolDetails["covRecreationCreateFailures"]);
        Assert.Equal("0", diagnostics.ProtocolDetails["covRecreationSubscribeRequests"]);
        Assert.Equal("0", diagnostics.ProtocolDetails["covRecreationSubscribeFailures"]);
        Assert.Equal("0", diagnostics.ProtocolDetails["covRecreationAttempts"]);
        Assert.Equal("0", diagnostics.ProtocolDetails["covRenewalRequests"]);
        Assert.Equal("0", diagnostics.ProtocolDetails["covRenewalFailures"]);
        Assert.Equal("0", diagnostics.ProtocolDetails["covRenewalTrackedRoutes"]);
        Assert.Equal("1", diagnostics.ProtocolDetails["covSubscribeRequests"]);
        Assert.Equal("1", diagnostics.ProtocolDetails["covSubscribeFailures"]);
        Assert.DoesNotContain("covLastRenewalRequestAtUtc", diagnostics.ProtocolDetails);
        Assert.DoesNotContain("covNextRenewalAttemptAtUtc", diagnostics.ProtocolDetails);
        Assert.DoesNotContain("covLastRenewalFailureAtUtc", diagnostics.ProtocolDetails);
        Assert.DoesNotContain("covLastRenewalErrorType", diagnostics.ProtocolDetails);

        await driver.StopAsync();
    }

    private static long DetailLong(CommunicationDriverDiagnosticSnapshot snapshot, string key)
        => snapshot.ProtocolDetails is not null &&
           snapshot.ProtocolDetails.TryGetValue(key, out var text) &&
           long.TryParse(text, out var value)
            ? value
            : -1;

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
            await Task.Delay(10);
        }

        latest ??= driver.GetCommunicationDiagnostics();
        Assert.True(predicate(latest), $"BACnet COV diagnostic condition was not reached within {timeout.TotalMilliseconds:0} ms.");
        return latest;
    }

    private sealed class DiagnosticCovSession : IBacnetSession, IBacnetCovSubscriptionDiagnostics
    {
        private readonly BacnetBinding _binding;
        private readonly bool[] _subscribeOutcomes;
        private readonly Exception? _firstReadError;
        private readonly int _backgroundRenewalRequests;
        private readonly int _backgroundRenewalFailures;
        private int _readCalls;
        private int _subscribeIndex = -1;
        private int _subscribeCalls;
        private int _subscribeFailures;
        private int _activeSubscriptions;
        private int _cancelRequests;

        public DiagnosticCovSession(
            BacnetBinding binding,
            bool[] subscribeOutcomes,
            Exception? firstReadError = null,
            int backgroundRenewalRequests = 0,
            int backgroundRenewalFailures = 0)
        {
            _binding = binding;
            _subscribeOutcomes = subscribeOutcomes;
            _firstReadError = firstReadError;
            _backgroundRenewalRequests = backgroundRenewalRequests;
            _backgroundRenewalFailures = backgroundRenewalFailures;
        }

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

        public Task<BacnetPropertyReadResult> ReadAsync(BacnetBinding binding, CancellationToken cancellationToken = default)
        {
            var call = Interlocked.Increment(ref _readCalls);
            if (call == 1 && _firstReadError is not null)
                return Task.FromException<BacnetPropertyReadResult>(_firstReadError);

            return Task.FromResult(new BacnetPropertyReadResult(
                _binding,
                new[] { new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, 82.5f) },
                DateTimeOffset.UtcNow,
                new BacnetObjectState(Reliability: 0),
                UsedReadPropertyMultiple: true));
        }

        public Task WriteAsync(
            BacnetBinding binding,
            IReadOnlyCollection<BacnetValue> values,
            CancellationToken cancellationToken = default)
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
        {
            var active = Volatile.Read(ref _activeSubscriptions);
            var hasRenewal = _backgroundRenewalRequests > 0;
            var hasFailure = _backgroundRenewalFailures > 0;
            var routes = active > 0
                ? new[]
                {
                    new BacnetCovRouteRenewalSnapshot(
                        SubscriptionId: 42,
                        PortableAddress: _binding.PortableAddress,
                        LastRenewalRequestAt: hasRenewal ? RenewalRequestAt : null,
                        NextRenewalAttemptAt: hasRenewal ? NextRenewalAt : null,
                        RenewalRequests: _backgroundRenewalRequests,
                        RenewalFailures: _backgroundRenewalFailures,
                        LastRenewalFailureAt: hasFailure ? RenewalFailureAt : null,
                        LastRenewalErrorType: hasFailure ? nameof(TimeoutException) : null)
                }
                : Array.Empty<BacnetCovRouteRenewalSnapshot>();

            return new BacnetCovSubscriptionSnapshot(
                ActiveSubscriptions: active,
                SubscribeRequests: Volatile.Read(ref _subscribeCalls) + _backgroundRenewalRequests,
                SubscribeFailures: Volatile.Read(ref _subscribeFailures) + _backgroundRenewalFailures,
                CancelRequests: Volatile.Read(ref _cancelRequests),
                CancelFailures: 0,
                LastErrorType: hasFailure ? nameof(TimeoutException) : null,
                SubscriptionLifetime: TimeSpan.FromSeconds(300),
                RenewalInterval: TimeSpan.FromSeconds(225),
                RetryInterval: TimeSpan.FromSeconds(30),
                RenewalRequests: _backgroundRenewalRequests,
                RenewalFailures: _backgroundRenewalFailures,
                LastRenewalRequestAt: hasRenewal ? RenewalRequestAt : null,
                NextRenewalAttemptAt: hasRenewal ? NextRenewalAt : null,
                LastRenewalFailureAt: hasFailure ? RenewalFailureAt : null,
                LastRenewalErrorType: hasFailure ? nameof(TimeoutException) : null,
                Routes: routes);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private sealed class TrackingSubscription(DiagnosticCovSession owner) : IBacnetCovSubscription
        {
            private int _disposed;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
                Interlocked.Increment(ref owner._cancelRequests);
                Interlocked.Decrement(ref owner._activeSubscriptions);
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}