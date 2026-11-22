using Scada.Core.Abstractions;
using Scada.Core.Events;
using Scada.Core.Interactions;
using Scada.DriverHost.Runtime;

namespace Scada.Drivers.Tests;

public sealed class TransientEventRuntimeDispatcherTests
{
    [Fact]
    public async Task ValidOccurrence_PublishesCanonicalEnvelopeAndPreservesNormalizedTimestamps()
    {
        var definition = CreateDefinition();
        var resolver = new InMemoryTransientEventDefinitionResolver(new[] { definition });
        var bus = new InMemoryScadaEventBus();
        await using var dispatcher = new TransientEventRuntimeDispatcher(resolver, bus, capacity: 4);

        TransientEventRuntimePublication? observed = null;
        using var subscription = bus.Subscribe<TransientEventRuntimePublication>(publication =>
        {
            observed = publication;
            return ValueTask.CompletedTask;
        });

        var observedAt = new DateTimeOffset(2026, 10, 6, 21, 10, 0, TimeSpan.FromHours(-3));
        var deviceAt = new DateTimeOffset(2026, 10, 7, 0, 9, 59, TimeSpan.Zero);
        var occurrence = CreateOccurrence(
            definition,
            1,
            observedAt,
            new TransientEventTimestamp(deviceAt, TransientEventTimestampOrigin.DeviceClock));

        await dispatcher.DispatchAsync(occurrence);

        Assert.NotNull(observed);
        Assert.IsAssignableFrom<IScadaEvent>(observed);
        Assert.False(typeof(IScadaEvent).IsAssignableFrom(typeof(TransientEventOccurrence)));
        Assert.Equal(occurrence.EventId, observed!.Occurrence.EventId);
        Assert.Equal(observedAt.ToUniversalTime(), observed.Occurrence.ObservedAt);
        Assert.Equal(deviceAt.ToUniversalTime(), observed.Occurrence.OccurredAt!.Value);
        Assert.Equal(deviceAt.ToUniversalTime(), observed.OccurredAt);
    }

    [Fact]
    public async Task UnknownDefinition_FailsClosedWithoutCanonicalPublication()
    {
        var definition = CreateDefinition();
        var resolver = new InMemoryTransientEventDefinitionResolver(new[] { definition });
        var bus = new RecordingEventBus();
        await using var dispatcher = new TransientEventRuntimeDispatcher(resolver, bus, capacity: 2);
        var unknown = CreateOccurrence(definition, 1) with { DefinitionId = Guid.NewGuid() };

        await Assert.ThrowsAsync<KeyNotFoundException>(() => dispatcher.DispatchAsync(unknown).AsTask());
        Assert.Empty(bus.Published);
    }

    [Fact]
    public async Task InvalidPayload_FailsClosedBeforeCanonicalPublication()
    {
        var definition = CreateDefinition();
        var resolver = new InMemoryTransientEventDefinitionResolver(new[] { definition });
        var bus = new RecordingEventBus();
        await using var dispatcher = new TransientEventRuntimeDispatcher(resolver, bus, capacity: 2);
        var invalid = CreateOccurrence(definition, 1) with
        {
            Payload = new[]
            {
                new TransientEventFieldValue("press_count", InteractionScalarValue.String("one"))
            }
        };

        await Assert.ThrowsAsync<ArgumentException>(() => dispatcher.DispatchAsync(invalid).AsTask());
        Assert.Empty(bus.Published);
    }

    [Fact]
    public async Task Dispatcher_PreservesIngressOrderAndPublishesOnlyTransientEnvelope()
    {
        var definition = CreateDefinition();
        var resolver = new InMemoryTransientEventDefinitionResolver(new[] { definition });
        var bus = new BlockingRecordingEventBus();
        await using var dispatcher = new TransientEventRuntimeDispatcher(resolver, bus, capacity: 4);
        var firstOccurrence = CreateOccurrence(definition, 1);
        var secondOccurrence = CreateOccurrence(definition, 2);
        var thirdOccurrence = CreateOccurrence(definition, 3);

        var first = dispatcher.DispatchAsync(firstOccurrence).AsTask();
        await bus.FirstPublicationStarted.WaitAsync(TimeSpan.FromSeconds(2));
        var second = dispatcher.DispatchAsync(secondOccurrence).AsTask();
        var third = dispatcher.DispatchAsync(thirdOccurrence).AsTask();

        bus.ReleaseFirstPublication();
        await Task.WhenAll(first, second, third);

        var published = bus.Published;
        Assert.Equal(
            new[] { firstOccurrence.EventId, secondOccurrence.EventId, thirdOccurrence.EventId },
            published.Select(item => Assert.IsType<TransientEventRuntimePublication>(item).Occurrence.EventId));
        Assert.All(published, item => Assert.IsType<TransientEventRuntimePublication>(item));
    }

    [Fact]
    public async Task BoundedQueue_WaitsForCapacityAndCancelledAdmissionIsNotSilentlyAccepted()
    {
        var definition = CreateDefinition();
        var resolver = new InMemoryTransientEventDefinitionResolver(new[] { definition });
        var bus = new BlockingRecordingEventBus();
        await using var dispatcher = new TransientEventRuntimeDispatcher(resolver, bus, capacity: 1);
        var firstOccurrence = CreateOccurrence(definition, 1);
        var secondOccurrence = CreateOccurrence(definition, 2);
        var cancelledOccurrence = CreateOccurrence(definition, 3);

        var first = dispatcher.DispatchAsync(firstOccurrence).AsTask();
        await bus.FirstPublicationStarted.WaitAsync(TimeSpan.FromSeconds(2));
        var second = dispatcher.DispatchAsync(secondOccurrence).AsTask();

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            dispatcher.DispatchAsync(cancelledOccurrence, cancellation.Token).AsTask());

        bus.ReleaseFirstPublication();
        await Task.WhenAll(first, second);

        Assert.Equal(
            new[] { firstOccurrence.EventId, secondOccurrence.EventId },
            bus.Published
                .Cast<TransientEventRuntimePublication>()
                .Select(publication => publication.Occurrence.EventId));
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public async Task RuntimeEventGate_ExistingAuthoritySeamControlsCanonicalForwarding(
        bool authoritative,
        int expectedExternalPublications)
    {
        var definition = CreateDefinition();
        var resolver = new InMemoryTransientEventDefinitionResolver(new[] { definition });
        var external = new RecordingEventBus();
        var gate = new RuntimeEventGate(
            external,
            forwardingEnabled: true,
            effectAuthority: () => authoritative);
        await using var dispatcher = new TransientEventRuntimeDispatcher(resolver, gate, capacity: 2);

        await dispatcher.DispatchAsync(CreateOccurrence(definition, 1));

        Assert.Equal(expectedExternalPublications, external.Published.Count);
        Assert.All(external.Published, item => Assert.IsType<TransientEventRuntimePublication>(item));
    }

    [Fact]
    public async Task ExactEventIdReplay_IsNotDeduplicatedWithinDispatcher()
    {
        var definition = CreateDefinition();
        var resolver = new InMemoryTransientEventDefinitionResolver(new[] { definition });
        var bus = new RecordingEventBus();
        await using var dispatcher = new TransientEventRuntimeDispatcher(resolver, bus, capacity: 4);
        var eventId = Guid.Parse("55400000-0000-0000-0000-000000000099");
        var replay = CreateOccurrence(definition, 4) with { EventId = eventId };

        await dispatcher.DispatchAsync(replay);
        await dispatcher.DispatchAsync(replay);

        Assert.Equal(
            new[] { eventId, eventId },
            bus.Published
                .Cast<TransientEventRuntimePublication>()
                .Select(publication => publication.Occurrence.EventId));
    }

    [Fact]
    public async Task L1_FakeProducer_RestartPreservesExplicitNoDedupPolicy()
    {
        var definition = CreateDefinition();
        var resolver = new InMemoryTransientEventDefinitionResolver(new[] { definition });
        var canonicalBus = new InMemoryScadaEventBus();
        var canonicalPublications = new List<Guid>();
        using var subscription = canonicalBus.Subscribe<TransientEventRuntimePublication>(publication =>
        {
            canonicalPublications.Add(publication.Occurrence.EventId);
            return ValueTask.CompletedTask;
        });

        var eventId = Guid.Parse("55400000-0000-0000-0000-000000000100");
        var replay = CreateOccurrence(definition, 3) with { EventId = eventId };

        async Task EmitFromFreshRuntimeAsync()
        {
            var gate = new RuntimeEventGate(
                canonicalBus,
                forwardingEnabled: true,
                effectAuthority: () => true);
            await using var dispatcher =
                new TransientEventRuntimeDispatcher(resolver, gate, capacity: 4);
            var source = new FakeTransientEventSource(dispatcher, definition);

            await source.EmitAsync(replay);
        }

        await EmitFromFreshRuntimeAsync();
        await EmitFromFreshRuntimeAsync();

        Assert.Equal(new[] { eventId, eventId }, canonicalPublications);
    }

    [Fact]
    public async Task RuntimeAuthorityFixture_ForwardingLifecycle_MapsCandidateActiveAndPreviousRuntime()
    {
        await using var fixture = new RuntimeAuthorityFixture(
            authoritative: true,
            forwardingEnabled: false);

        Assert.False(fixture.Gate.ForwardingEnabled);

        await fixture.Source.EmitAsync(1);
        Assert.Single(fixture.LocalPublications);
        Assert.Empty(fixture.CanonicalPublications);

        fixture.Gate.EnableForwarding();
        Assert.True(fixture.Gate.ForwardingEnabled);

        await fixture.Source.EmitAsync(2);
        Assert.Equal(2, fixture.LocalPublications.Count);
        Assert.Equal(
            fixture.LocalPublications[1],
            Assert.Single(fixture.CanonicalPublications));

        fixture.Gate.DisableForwarding();
        Assert.False(fixture.Gate.ForwardingEnabled);

        await fixture.Source.EmitAsync(3);
        Assert.Equal(3, fixture.LocalPublications.Count);
        Assert.Single(fixture.CanonicalPublications);
    }

    [Fact]
    public async Task RuntimeAuthorityFixture_StandbyAuthority_BlocksCanonicalAndLocalPublication()
    {
        await using var fixture = new RuntimeAuthorityFixture(
            authoritative: false,
            forwardingEnabled: true);

        Assert.True(fixture.Gate.ForwardingEnabled);

        await fixture.Source.EmitAsync(1);
        Assert.Empty(fixture.LocalPublications);
        Assert.Empty(fixture.CanonicalPublications);

        fixture.SetAuthoritative(true);
        await fixture.Source.EmitAsync(2);

        Assert.Single(fixture.LocalPublications);
        Assert.Equal(
            fixture.LocalPublications[0],
            Assert.Single(fixture.CanonicalPublications));

        fixture.SetAuthoritative(false);
        await fixture.Source.EmitAsync(3);

        Assert.Single(fixture.LocalPublications);
        Assert.Single(fixture.CanonicalPublications);
    }

    [Fact]
    public async Task StopCancellation_ReturnsPromptlyAndCompletesEveryAdmittedItemExplicitly()
    {
        var definition = CreateDefinition();
        var resolver = new InMemoryTransientEventDefinitionResolver(new[] { definition });
        var bus = new BlockingRecordingEventBus();
        var dispatcher = new TransientEventRuntimeDispatcher(resolver, bus, capacity: 1);
        var firstOccurrence = CreateOccurrence(definition, 1);
        var queuedOccurrence = CreateOccurrence(definition, 2);

        var first = dispatcher.DispatchAsync(firstOccurrence).AsTask();
        await bus.FirstPublicationStarted.WaitAsync(TimeSpan.FromSeconds(2));
        var queued = dispatcher.DispatchAsync(queuedOccurrence).AsTask();

        using var stopCancellation = new CancellationTokenSource();
        var stop = dispatcher.StopAsync(stopCancellation.Token).AsTask();
        stopCancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            stop.WaitAsync(TimeSpan.FromSeconds(2)));
        var firstError = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            first.WaitAsync(TimeSpan.FromSeconds(2)));
        var queuedError = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            queued.WaitAsync(TimeSpan.FromSeconds(2)));

        Assert.Contains("delivery outcome is unknown", firstError.Message);
        Assert.Contains("before this admitted occurrence was published", queuedError.Message);
        Assert.Empty(bus.Published);
        Assert.Equal(1, bus.Attempts);

        bus.ReleaseFirstPublication();
        await bus.FirstPublicationFinished.WaitAsync(TimeSpan.FromSeconds(2));
        await dispatcher.DisposeAsync();

        var published = Assert.Single(bus.Published);
        Assert.Equal(
            firstOccurrence.EventId,
            Assert.IsType<TransientEventRuntimePublication>(published).Occurrence.EventId);
        Assert.Equal(1, bus.Attempts);
    }

    [Fact]
    public async Task Stop_RejectsNewIngressWithoutSilentAcceptance()
    {
        var definition = CreateDefinition();
        var resolver = new InMemoryTransientEventDefinitionResolver(new[] { definition });
        var bus = new RecordingEventBus();
        await using var dispatcher = new TransientEventRuntimeDispatcher(resolver, bus, capacity: 2);

        await dispatcher.DispatchAsync(CreateOccurrence(definition, 1));
        await dispatcher.StopAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.DispatchAsync(CreateOccurrence(definition, 2)).AsTask());
        Assert.Single(bus.Published);
    }

    [Fact]
    public async Task DownstreamPublicationFailure_IsSurfacedWithoutAutomaticRetry()
    {
        var definition = CreateDefinition();
        var resolver = new InMemoryTransientEventDefinitionResolver(new[] { definition });
        var bus = new ThrowingEventBus();
        await using var dispatcher = new TransientEventRuntimeDispatcher(resolver, bus, capacity: 2);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.DispatchAsync(CreateOccurrence(definition, 1)).AsTask());

        Assert.Equal("synthetic publication failure", error.Message);
        Assert.Equal(1, bus.Attempts);
    }

    private static TransientEventDefinition CreateDefinition() =>
        new(
            Guid.Parse("55400000-0000-0000-0000-000000000001"),
            "button.double_click",
            new[]
            {
                new TransientEventFieldDefinition(
                    "press_count",
                    new InteractionScalarSchema(InteractionScalarKind.Integer, Minimum: 1, Maximum: 4),
                    Required: true)
            });

    private static TransientEventOccurrence CreateOccurrence(
        TransientEventDefinition definition,
        long sequence,
        DateTimeOffset? observedAt = null,
        TransientEventTimestamp? occurredAt = null) =>
        new(
            Guid.NewGuid(),
            definition.DefinitionId,
            definition.SemanticKey,
            new TransientEventSource(
                Guid.Parse("55400000-0000-0000-0000-000000000002"),
                "device-01"),
            new[]
            {
                new TransientEventFieldValue("press_count", InteractionScalarValue.Integer(sequence))
            },
            observedAt ?? DateTimeOffset.UtcNow,
            occurredAt,
            new TransientEventEvidence(Sequence: sequence));

    private sealed class FakeTransientEventSource
    {
        private readonly ITransientEventRuntimeIngress _ingress;
        private readonly TransientEventDefinition _definition;

        public FakeTransientEventSource(
            ITransientEventRuntimeIngress ingress,
            TransientEventDefinition definition)
        {
            _ingress = ingress;
            _definition = definition;
        }

        public Task EmitAsync(long sequence) =>
            _ingress.DispatchAsync(
                CreateOccurrence(_definition, sequence)).AsTask();

        public Task EmitAsync(TransientEventOccurrence occurrence) =>
            _ingress.DispatchAsync(occurrence).AsTask();
    }

    private sealed class RuntimeAuthorityFixture : IAsyncDisposable
    {
        private readonly InMemoryScadaEventBus _canonicalBus = new();
        private readonly IDisposable _localSubscription;
        private readonly IDisposable _canonicalSubscription;
        private int _authoritative;

        public RuntimeAuthorityFixture(bool authoritative, bool forwardingEnabled)
        {
            _authoritative = authoritative ? 1 : 0;

            var definition = CreateDefinition();
            var resolver = new InMemoryTransientEventDefinitionResolver(new[] { definition });
            Gate = new RuntimeEventGate(
                _canonicalBus,
                forwardingEnabled,
                effectAuthority: () => Volatile.Read(ref _authoritative) == 1);
            Dispatcher = new TransientEventRuntimeDispatcher(resolver, Gate, capacity: 4);
            Source = new FakeTransientEventSource(Dispatcher, definition);

            _localSubscription = Gate.Subscribe<TransientEventRuntimePublication>(publication =>
            {
                LocalPublications.Add(publication.Occurrence.EventId);
                return ValueTask.CompletedTask;
            });
            _canonicalSubscription = _canonicalBus.Subscribe<TransientEventRuntimePublication>(publication =>
            {
                CanonicalPublications.Add(publication.Occurrence.EventId);
                return ValueTask.CompletedTask;
            });
        }

        public RuntimeEventGate Gate { get; }
        public TransientEventRuntimeDispatcher Dispatcher { get; }
        public FakeTransientEventSource Source { get; }
        public List<Guid> LocalPublications { get; } = new();
        public List<Guid> CanonicalPublications { get; } = new();

        public void SetAuthoritative(bool authoritative) =>
            Volatile.Write(ref _authoritative, authoritative ? 1 : 0);

        public async ValueTask DisposeAsync()
        {
            _localSubscription.Dispose();
            _canonicalSubscription.Dispose();
            await Dispatcher.DisposeAsync();
        }
    }

    private sealed class RecordingEventBus : IScadaEventBus
    {
        private readonly object _gate = new();
        private readonly List<IScadaEvent> _published = new();

        public IReadOnlyList<IScadaEvent> Published
        {
            get
            {
                lock (_gate)
                    return _published.ToArray();
            }
        }

        public IDisposable Subscribe<TEvent>(Func<TEvent, ValueTask> handler)
            where TEvent : IScadaEvent => NoopSubscription.Instance;

        public ValueTask PublishAsync<TEvent>(
            TEvent scadaEvent,
            CancellationToken cancellationToken = default)
            where TEvent : IScadaEvent
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
                _published.Add(scadaEvent);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class BlockingRecordingEventBus : IScadaEventBus
    {
        private readonly object _gate = new();
        private readonly List<IScadaEvent> _published = new();
        private readonly TaskCompletionSource<bool> _firstPublicationStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _releaseFirstPublication =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _firstPublicationFinished =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _attempts;

        public Task FirstPublicationStarted => _firstPublicationStarted.Task;
        public Task FirstPublicationFinished => _firstPublicationFinished.Task;
        public int Attempts => Volatile.Read(ref _attempts);

        public IReadOnlyList<IScadaEvent> Published
        {
            get
            {
                lock (_gate)
                    return _published.ToArray();
            }
        }

        public void ReleaseFirstPublication() => _releaseFirstPublication.TrySetResult(true);

        public IDisposable Subscribe<TEvent>(Func<TEvent, ValueTask> handler)
            where TEvent : IScadaEvent => NoopSubscription.Instance;

        public async ValueTask PublishAsync<TEvent>(
            TEvent scadaEvent,
            CancellationToken cancellationToken = default)
            where TEvent : IScadaEvent
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Interlocked.Increment(ref _attempts) == 1)
            {
                _firstPublicationStarted.TrySetResult(true);
                await _releaseFirstPublication.Task.ConfigureAwait(false);
            }

            lock (_gate)
                _published.Add(scadaEvent);

            _firstPublicationFinished.TrySetResult(true);
        }
    }

    private sealed class ThrowingEventBus : IScadaEventBus
    {
        public int Attempts { get; private set; }

        public IDisposable Subscribe<TEvent>(Func<TEvent, ValueTask> handler)
            where TEvent : IScadaEvent => NoopSubscription.Instance;

        public ValueTask PublishAsync<TEvent>(
            TEvent scadaEvent,
            CancellationToken cancellationToken = default)
            where TEvent : IScadaEvent
        {
            Attempts++;
            throw new InvalidOperationException("synthetic publication failure");
        }
    }

    private sealed class NoopSubscription : IDisposable
    {
        public static readonly NoopSubscription Instance = new();
        public void Dispose() { }
    }
}
