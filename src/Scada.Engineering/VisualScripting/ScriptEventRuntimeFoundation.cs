namespace Scada.Engineering.VisualScripting;

public sealed class ScriptEventIdentity
{
    public ScriptEventIdentity(
        PythonScriptEventKind eventKind,
        string handlerName,
        string? targetReference = null,
        string? coalescingKey = null)
    {
        if (string.IsNullOrWhiteSpace(handlerName))
            throw new ArgumentException("Script event handler name is required.", nameof(handlerName));

        EventKind = eventKind;
        HandlerName = handlerName;
        TargetReference = string.IsNullOrWhiteSpace(targetReference) ? null : targetReference;
        CoalescingKey = string.IsNullOrWhiteSpace(coalescingKey)
            ? BuildDefaultCoalescingKey(eventKind, handlerName, TargetReference)
            : coalescingKey;
    }

    public PythonScriptEventKind EventKind { get; }

    public string HandlerName { get; }

    public string? TargetReference { get; }

    public string CoalescingKey { get; }

    private static string BuildDefaultCoalescingKey(
        PythonScriptEventKind eventKind,
        string handlerName,
        string? targetReference) =>
        $"{eventKind}:{handlerName}:{targetReference ?? string.Empty}";
}

public sealed record ScriptEventEnvelope(
    Guid ScriptId,
    string RuntimeInstanceId,
    ScriptEventIdentity Identity,
    long Sequence,
    DateTimeOffset EnqueuedAt);

public enum ScriptEventEnqueueStatus
{
    Enqueued,
    Coalesced,
    RejectedQueueFull,
    DroppedOldestAndEnqueued
}

public sealed record ScriptEventEnqueueResult(
    ScriptEventEnqueueStatus Status,
    ScriptEventEnvelope? EnqueuedEvent,
    ScriptEventEnvelope? ReplacedOrDroppedEvent);

/// <summary>
/// Deterministic bounded queue for one script runtime instance. Coalescing replaces an already queued
/// event with the same event key without growing the queue. It deliberately contains no Python engine.
/// </summary>
public sealed class BoundedScriptEventQueue : IDisposable
{
    private readonly object _sync = new();
    private readonly Guid _scriptId;
    private readonly string _runtimeInstanceId;
    private readonly ScriptExecutionPolicy _policy;
    private readonly LinkedList<ScriptEventEnvelope> _queue = new();
    private long _nextSequence;
    private bool _disposed;

    public BoundedScriptEventQueue(
        Guid scriptId,
        string runtimeInstanceId,
        ScriptExecutionPolicy policy)
    {
        if (scriptId == Guid.Empty)
            throw new ArgumentException("Script ID is required.", nameof(scriptId));
        if (string.IsNullOrWhiteSpace(runtimeInstanceId))
            throw new ArgumentException("Script runtime instance ID is required.", nameof(runtimeInstanceId));

        ArgumentNullException.ThrowIfNull(policy);

        _scriptId = scriptId;
        _runtimeInstanceId = runtimeInstanceId;
        _policy = policy;
    }

    public int Count
    {
        get
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                return _queue.Count;
            }
        }
    }

    public int Capacity => _policy.MaxQueuedEvents;

    public ScriptEventEnqueueResult Enqueue(
        ScriptEventIdentity identity,
        DateTimeOffset? enqueuedAt = null)
    {
        ArgumentNullException.ThrowIfNull(identity);

        lock (_sync)
        {
            ThrowIfDisposed();

            var candidate = new ScriptEventEnvelope(
                _scriptId,
                _runtimeInstanceId,
                identity,
                ++_nextSequence,
                enqueuedAt ?? DateTimeOffset.UtcNow);

            if (_policy.QueueOverflowStrategy == ScriptQueueOverflowStrategy.CoalesceByEventKey)
            {
                var existing = FindByCoalescingKey(identity.CoalescingKey);
                if (existing is not null)
                {
                    var replaced = existing.Value;
                    existing.Value = candidate;
                    return new(
                        ScriptEventEnqueueStatus.Coalesced,
                        candidate,
                        replaced);
                }
            }

            if (_queue.Count < _policy.MaxQueuedEvents)
            {
                _queue.AddLast(candidate);
                return new(
                    ScriptEventEnqueueStatus.Enqueued,
                    candidate,
                    null);
            }

            switch (_policy.QueueOverflowStrategy)
            {
                case ScriptQueueOverflowStrategy.DropOldest:
                {
                    var dropped = _queue.First!.Value;
                    _queue.RemoveFirst();
                    _queue.AddLast(candidate);
                    return new(
                        ScriptEventEnqueueStatus.DroppedOldestAndEnqueued,
                        candidate,
                        dropped);
                }

                case ScriptQueueOverflowStrategy.RejectNewest:
                case ScriptQueueOverflowStrategy.CoalesceByEventKey:
                    return new(
                        ScriptEventEnqueueStatus.RejectedQueueFull,
                        null,
                        null);

                default:
                    throw new InvalidOperationException(
                        $"Unsupported queue overflow strategy '{_policy.QueueOverflowStrategy}'.");
            }
        }
    }

    public bool TryDequeue(out ScriptEventEnvelope? scriptEvent)
    {
        lock (_sync)
        {
            ThrowIfDisposed();

            if (_queue.First is null)
            {
                scriptEvent = null;
                return false;
            }

            scriptEvent = _queue.First.Value;
            _queue.RemoveFirst();
            return true;
        }
    }

    public IReadOnlyCollection<ScriptEventEnvelope> Snapshot()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            return Array.AsReadOnly(_queue.ToArray());
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            _queue.Clear();
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;

            _queue.Clear();
            _disposed = true;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(BoundedScriptEventQueue));
    }

    private LinkedListNode<ScriptEventEnvelope>? FindByCoalescingKey(string key)
    {
        var current = _queue.First;
        while (current is not null)
        {
            if (string.Equals(
                    current.Value.Identity.CoalescingKey,
                    key,
                    StringComparison.Ordinal))
            {
                return current;
            }

            current = current.Next;
        }

        return null;
    }
}

public readonly record struct ScriptSubscriptionToken(Guid Value)
{
    public static ScriptSubscriptionToken New() => new(Guid.NewGuid());
}

public sealed record ScriptEventSubscription(
    ScriptSubscriptionToken Token,
    Guid ScriptId,
    string RuntimeInstanceId,
    PythonScriptEventKind EventKind,
    string HandlerName,
    string? TargetReference,
    TimeSpan? TimerInterval,
    DateTimeOffset CreatedAt);

/// <summary>
/// Owns event subscriptions for one visual runtime instance. Disposing the instance registry removes
/// all subscriptions so closed screens/popups/Dynamos cannot leave orphan timers or listeners.
/// </summary>
public sealed class ScriptEventSubscriptionRegistry : IDisposable
{
    private readonly object _sync = new();
    private readonly string _runtimeInstanceId;
    private readonly Dictionary<ScriptSubscriptionToken, ScriptEventSubscription> _subscriptions = [];
    private bool _disposed;

    public ScriptEventSubscriptionRegistry(string runtimeInstanceId)
    {
        if (string.IsNullOrWhiteSpace(runtimeInstanceId))
            throw new ArgumentException("Runtime instance ID is required.", nameof(runtimeInstanceId));

        _runtimeInstanceId = runtimeInstanceId;
    }

    public int Count
    {
        get
        {
            lock (_sync)
                return _subscriptions.Count;
        }
    }

    public ScriptEventSubscription Register(
        PythonScriptDefinition script,
        PythonScriptEntryPoint entryPoint,
        ScriptExecutionPolicy policy,
        TimeSpan? timerInterval = null,
        DateTimeOffset? createdAt = null)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(entryPoint);
        ArgumentNullException.ThrowIfNull(policy);

        lock (_sync)
        {
            ThrowIfDisposed();

            if (!script.Enabled)
                throw new InvalidOperationException(
                    $"Disabled script '{script.Path}' cannot register runtime subscriptions.");

            if (!script.EntryPoints.Contains(entryPoint))
            {
                throw new InvalidOperationException(
                    $"Entry point '{entryPoint.EventKind}:{entryPoint.HandlerName}:{entryPoint.TargetReference}' is not declared by script '{script.Path}'.");
            }

            if (!ScriptScopeEventRules.IsAllowed(script.Scope, entryPoint.EventKind))
            {
                throw new InvalidOperationException(
                    $"Event '{entryPoint.EventKind}' is not valid for script scope '{script.Scope}'.");
            }

            if (entryPoint.EventKind == PythonScriptEventKind.Timer)
            {
                if (!timerInterval.HasValue)
                    throw new ArgumentException(
                        "Timer subscriptions require an explicit interval.",
                        nameof(timerInterval));

                if (timerInterval.Value < policy.MinimumTimerInterval)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(timerInterval),
                        timerInterval,
                        $"Timer interval cannot be shorter than {policy.MinimumTimerInterval}.");
                }
            }
            else if (timerInterval.HasValue)
            {
                throw new ArgumentException(
                    "Timer interval is only valid for Timer subscriptions.",
                    nameof(timerInterval));
            }

            if (_subscriptions.Values.Any(existing =>
                    existing.ScriptId == script.Id &&
                    existing.EventKind == entryPoint.EventKind &&
                    string.Equals(existing.HandlerName, entryPoint.HandlerName, StringComparison.Ordinal) &&
                    string.Equals(existing.TargetReference, entryPoint.TargetReference, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Subscription '{script.Id}:{entryPoint.EventKind}:{entryPoint.HandlerName}:{entryPoint.TargetReference}' is already registered.");
            }

            var subscription = new ScriptEventSubscription(
                ScriptSubscriptionToken.New(),
                script.Id,
                _runtimeInstanceId,
                entryPoint.EventKind,
                entryPoint.HandlerName,
                entryPoint.TargetReference,
                timerInterval,
                createdAt ?? DateTimeOffset.UtcNow);

            _subscriptions.Add(subscription.Token, subscription);
            return subscription;
        }
    }

    public bool Remove(ScriptSubscriptionToken token)
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            return _subscriptions.Remove(token);
        }
    }

    public IReadOnlyCollection<ScriptEventSubscription> Snapshot()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            return Array.AsReadOnly(
                _subscriptions.Values
                    .OrderBy(subscription => subscription.CreatedAt)
                    .ThenBy(subscription => subscription.Token.Value)
                    .ToArray());
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;

            _subscriptions.Clear();
            _disposed = true;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ScriptEventSubscriptionRegistry));
    }
}

public static class ScriptScopeEventRules
{
    public static bool IsAllowed(
        PythonScriptScope scope,
        PythonScriptEventKind eventKind) =>
        scope switch
        {
            PythonScriptScope.ClientVisual =>
                eventKind != PythonScriptEventKind.ServerRuntimeEvent,

            PythonScriptScope.Server =>
                eventKind is
                    PythonScriptEventKind.Initialize or
                    PythonScriptEventKind.Dispose or
                    PythonScriptEventKind.TagChanged or
                    PythonScriptEventKind.Timer or
                    PythonScriptEventKind.ServerRuntimeEvent,

            _ => false
        };
}

public sealed class ScriptFailureThrottle
{
    private readonly object _sync = new();
    private readonly ScriptExecutionPolicy _policy;
    private int _consecutiveFailures;
    private bool _isThrottled;

    public ScriptFailureThrottle(ScriptExecutionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        _policy = policy;
    }

    public int ConsecutiveFailures
    {
        get
        {
            lock (_sync)
                return _consecutiveFailures;
        }
    }

    public bool IsThrottled
    {
        get
        {
            lock (_sync)
                return _isThrottled;
        }
    }

    public void Record(ScriptExecutionStatus status)
    {
        lock (_sync)
        {
            if (status == ScriptExecutionStatus.Completed)
            {
                _consecutiveFailures = 0;
                return;
            }

            if (status is not (ScriptExecutionStatus.Faulted or ScriptExecutionStatus.TimedOut))
                return;

            _consecutiveFailures++;
            if (_consecutiveFailures >= _policy.MaxConsecutiveFailuresBeforeThrottle)
                _isThrottled = true;
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            _consecutiveFailures = 0;
            _isThrottled = false;
        }
    }
}

public sealed record ScriptRuntimeDiagnosticsSnapshot(
    Guid ScriptId,
    string RuntimeInstanceId,
    long ExecutionCount,
    long CompletedCount,
    long FaultedCount,
    long TimeoutCount,
    long CancelledCount,
    long QueueRejectedCount,
    long QueueCoalescedCount,
    long QueueDroppedOldestCount,
    ScriptExecutionStatus? LastStatus,
    TimeSpan? LastDuration,
    DateTimeOffset? LastCompletedAt,
    string? LastSanitizedError,
    int ConsecutiveFailures,
    bool IsThrottled,
    int ActiveSubscriptions,
    int QueuedEvents);

public sealed class ScriptRuntimeDiagnosticsTracker
{
    private const int MaximumStoredErrorLength = 1024;

    private readonly object _sync = new();
    private readonly Guid _scriptId;
    private readonly string _runtimeInstanceId;
    private readonly ScriptFailureThrottle _failureThrottle;

    private long _executionCount;
    private long _completedCount;
    private long _faultedCount;
    private long _timeoutCount;
    private long _cancelledCount;
    private long _queueRejectedCount;
    private long _queueCoalescedCount;
    private long _queueDroppedOldestCount;
    private ScriptExecutionStatus? _lastStatus;
    private TimeSpan? _lastDuration;
    private DateTimeOffset? _lastCompletedAt;
    private string? _lastSanitizedError;

    public ScriptRuntimeDiagnosticsTracker(
        Guid scriptId,
        string runtimeInstanceId,
        ScriptExecutionPolicy policy)
    {
        if (scriptId == Guid.Empty)
            throw new ArgumentException("Script ID is required.", nameof(scriptId));
        if (string.IsNullOrWhiteSpace(runtimeInstanceId))
            throw new ArgumentException("Runtime instance ID is required.", nameof(runtimeInstanceId));

        ArgumentNullException.ThrowIfNull(policy);

        _scriptId = scriptId;
        _runtimeInstanceId = runtimeInstanceId;
        _failureThrottle = new ScriptFailureThrottle(policy);
    }

    public void RecordExecution(ScriptExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.ScriptId != _scriptId ||
            !string.Equals(result.RuntimeInstanceId, _runtimeInstanceId, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Execution result belongs to a different script runtime instance.",
                nameof(result));
        }

        lock (_sync)
        {
            _executionCount++;
            if (result.Duration < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(
                    nameof(result),
                    "Execution duration cannot be negative.");

            _lastStatus = result.Status;
            _lastDuration = result.Duration;
            _lastCompletedAt = result.CompletedAt;

            if (result.Status is ScriptExecutionStatus.Faulted or ScriptExecutionStatus.TimedOut)
                _lastSanitizedError = NormalizeSanitizedError(result.SanitizedError);

            switch (result.Status)
            {
                case ScriptExecutionStatus.Completed:
                    _completedCount++;
                    break;
                case ScriptExecutionStatus.Faulted:
                    _faultedCount++;
                    break;
                case ScriptExecutionStatus.TimedOut:
                    _timeoutCount++;
                    break;
                case ScriptExecutionStatus.Cancelled:
                    _cancelledCount++;
                    break;
            }

            _failureThrottle.Record(result.Status);
        }
    }

    public void RecordQueueResult(ScriptEventEnqueueResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        lock (_sync)
        {
            switch (result.Status)
            {
                case ScriptEventEnqueueStatus.RejectedQueueFull:
                    _queueRejectedCount++;
                    break;
                case ScriptEventEnqueueStatus.Coalesced:
                    _queueCoalescedCount++;
                    break;
                case ScriptEventEnqueueStatus.DroppedOldestAndEnqueued:
                    _queueDroppedOldestCount++;
                    break;
            }
        }
    }

    public ScriptRuntimeDiagnosticsSnapshot Snapshot(
        int activeSubscriptions,
        int queuedEvents)
    {
        if (activeSubscriptions < 0)
            throw new ArgumentOutOfRangeException(nameof(activeSubscriptions));
        if (queuedEvents < 0)
            throw new ArgumentOutOfRangeException(nameof(queuedEvents));

        lock (_sync)
        {
            return new(
                _scriptId,
                _runtimeInstanceId,
                _executionCount,
                _completedCount,
                _faultedCount,
                _timeoutCount,
                _cancelledCount,
                _queueRejectedCount,
                _queueCoalescedCount,
                _queueDroppedOldestCount,
                _lastStatus,
                _lastDuration,
                _lastCompletedAt,
                _lastSanitizedError,
                _failureThrottle.ConsecutiveFailures,
                _failureThrottle.IsThrottled,
                activeSubscriptions,
                queuedEvents);
        }
    }

    public void ResetThrottle() => _failureThrottle.Reset();

    private static string? NormalizeSanitizedError(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var singleLine = value
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();

        return singleLine.Length <= MaximumStoredErrorLength
            ? singleLine
            : singleLine[..MaximumStoredErrorLength];
    }
}
