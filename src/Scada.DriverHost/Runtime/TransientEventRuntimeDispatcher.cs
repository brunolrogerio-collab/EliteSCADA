using System.Threading.Channels;
using Scada.Core.Abstractions;
using Scada.Core.Events;

namespace Scada.DriverHost.Runtime;

/// <summary>
/// Runtime-only authority for resolving the canonical definition that validates a
/// protocol-neutral Transient Event occurrence. Engineering persistence and schema
/// selection deliberately remain outside this boundary.
/// </summary>
public interface ITransientEventDefinitionResolver
{
    bool TryResolve(Guid definitionId, out TransientEventDefinition? definition);
}

/// <summary>
/// Small immutable resolver useful for bounded Runtime composition and focused
/// tests. Definitions are normalized once when the resolver is created.
/// </summary>
public sealed class InMemoryTransientEventDefinitionResolver : ITransientEventDefinitionResolver
{
    private readonly IReadOnlyDictionary<Guid, TransientEventDefinition> _definitions;

    public InMemoryTransientEventDefinitionResolver(IEnumerable<TransientEventDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var normalized = new Dictionary<Guid, TransientEventDefinition>();
        foreach (var definition in definitions)
        {
            var candidate = TransientEventContract.NormalizeDefinition(definition);
            if (!normalized.TryAdd(candidate.DefinitionId, candidate))
            {
                throw new ArgumentException(
                    $"Transient Event definition '{candidate.DefinitionId}' is duplicated.",
                    nameof(definitions));
            }
        }

        _definitions = normalized;
    }

    public bool TryResolve(Guid definitionId, out TransientEventDefinition? definition) =>
        _definitions.TryGetValue(definitionId, out definition);
}

/// <summary>
/// Host-owned, protocol-neutral ingress for validated Runtime Transient Events.
/// Cancellation controls admission while waiting for bounded queue capacity. Once
/// admitted, an occurrence is completed explicitly as published or failed rather
/// than being silently abandoned by caller cancellation.
/// </summary>
public interface ITransientEventRuntimeIngress
{
    ValueTask DispatchAsync(
        TransientEventOccurrence occurrence,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Canonical Runtime publication envelope. S0 TransientEventOccurrence deliberately
/// remains independent from IScadaEvent; only this validated Runtime adapter enters
/// the existing IScadaEventBus.
/// </summary>
public sealed class TransientEventRuntimePublication : IScadaEvent
{
    internal TransientEventRuntimePublication(TransientEventOccurrence occurrence)
    {
        Occurrence = occurrence ?? throw new ArgumentNullException(nameof(occurrence));
    }

    public TransientEventOccurrence Occurrence { get; }

    public DateTimeOffset OccurredAt =>
        Occurrence.OccurredAt?.Value ?? Occurrence.ObservedAt;
}

/// <summary>
/// Single-reader bounded dispatcher for transient Runtime events. Queue admission
/// uses WAIT backpressure; no event is dropped, coalesced, retried or promoted to a
/// TAG, Alarm, Historian/Audit record or Operational Event by this component.
/// </summary>
public sealed class TransientEventRuntimeDispatcher : ITransientEventRuntimeIngress, IAsyncDisposable
{
    public const int DefaultCapacity = 256;

    private readonly ITransientEventDefinitionResolver _definitions;
    private readonly IScadaEventBus _eventBus;
    private readonly Channel<DispatchWorkItem> _queue;
    private readonly CancellationTokenSource _abort = new();
    private readonly Task _worker;
    private int _state;

    public TransientEventRuntimeDispatcher(
        ITransientEventDefinitionResolver definitions,
        IScadaEventBus eventBus,
        int capacity = DefaultCapacity)
    {
        _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Transient Event queue capacity must be at least one.");

        _queue = Channel.CreateBounded<DispatchWorkItem>(new BoundedChannelOptions(capacity)
        {
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
        _worker = ProcessAsync();
    }

    public async ValueTask DispatchAsync(
        TransientEventOccurrence occurrence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(occurrence);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureRunning();

        if (!_definitions.TryResolve(occurrence.DefinitionId, out var definition) || definition is null)
        {
            throw new KeyNotFoundException(
                $"Transient Event definition '{occurrence.DefinitionId}' is not authoritative or is unknown to this Runtime.");
        }

        var validated = TransientEventContract.ValidateOccurrence(definition, occurrence);
        var item = new DispatchWorkItem(validated);

        if (!_queue.Writer.TryWrite(item))
        {
            EnsureRunning();
            try
            {
                await _queue.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
            }
            catch (ChannelClosedException)
            {
                throw DispatcherStopped();
            }
        }

        // Caller cancellation only controls bounded admission. After admission the
        // dispatcher owns completion so an accepted physical occurrence cannot be
        // silently abandoned because its producer stopped awaiting the call.
        await item.Completion.Task.ConfigureAwait(false);
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        BeginStop();
        try
        {
            await _worker.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cancellation changes a graceful drain into an explicit abort of work
            // not yet published. The currently executing publication is allowed to
            // finish so the EventBus cannot be left half-forwarded by this layer.
            _abort.Cancel();
            await _worker.ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        BeginStop();
        await _worker.ConfigureAwait(false);
        _abort.Dispose();
    }

    private async Task ProcessAsync()
    {
        try
        {
            await foreach (var item in _queue.Reader.ReadAllAsync(_abort.Token))
            {
                try
                {
                    var publication = new TransientEventRuntimePublication(item.Occurrence);
                    await _eventBus.PublishAsync(publication, CancellationToken.None).ConfigureAwait(false);
                    item.Completion.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    // Publication failures are surfaced to the admitted producer.
                    // There is deliberately no automatic retry because a handler
                    // may already have observed the physical event.
                    item.Completion.TrySetException(ex);
                }
            }
        }
        catch (OperationCanceledException) when (_abort.IsCancellationRequested)
        {
            // Explicit stop cancellation: remaining admitted items are failed below.
        }
        finally
        {
            while (_queue.Reader.TryRead(out var pending))
            {
                pending.Completion.TrySetException(new OperationCanceledException(
                    "Transient Event dispatcher stopped before this admitted occurrence could be published."));
            }

            Volatile.Write(ref _state, 2);
        }
    }

    private void BeginStop()
    {
        if (Interlocked.CompareExchange(ref _state, 1, 0) == 0)
            _queue.Writer.TryComplete();
    }

    private void EnsureRunning()
    {
        if (Volatile.Read(ref _state) != 0)
            throw DispatcherStopped();
    }

    private static InvalidOperationException DispatcherStopped() =>
        new("Transient Event dispatcher is stopping or stopped and cannot admit new occurrences.");

    private sealed class DispatchWorkItem
    {
        public DispatchWorkItem(TransientEventOccurrence occurrence)
        {
            Occurrence = occurrence;
            Completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public TransientEventOccurrence Occurrence { get; }
        public TaskCompletionSource<bool> Completion { get; }
    }
}
