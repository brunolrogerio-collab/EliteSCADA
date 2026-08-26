using System.Diagnostics;

namespace Scada.Engineering.VisualScripting;

/// <summary>
/// Adapter implemented by the selected sandboxed Python engine. The coordinator supplies a bounded
/// execution lease and never gives the engine direct access to OS, renderer, database or driver internals.
/// Implementations must honor lease cancellation/abort; the coordinator intentionally does not leave an
/// uncooperative handler running in a detached background task after its budget expires.
/// </summary>
public interface IPythonScriptHandlerExecutor
{
    ValueTask ExecuteAsync(
        PythonScriptDefinition script,
        ScriptEventEnvelope scriptEvent,
        ScriptExecutionLease lease);
}

/// <summary>
/// Explicit opt-in exception for an engine adapter that has already sanitized a developer-facing fault.
/// Arbitrary exception messages are not trusted for diagnostics because they may contain sensitive host
/// details or values that must not cross the scripting sandbox boundary.
/// </summary>
public sealed class ScriptExecutionDiagnosticException : Exception
{
    public ScriptExecutionDiagnosticException(string sanitizedMessage)
        : base("Script execution failed with a sanitized diagnostic.")
    {
        if (string.IsNullOrWhiteSpace(sanitizedMessage))
            throw new ArgumentException("Sanitized diagnostic message is required.", nameof(sanitizedMessage));

        SanitizedMessage = sanitizedMessage;
    }

    public string SanitizedMessage { get; }
}

public enum ScriptRuntimeDispatchStatus
{
    NoEvent,
    Executed,
    Throttled
}

public sealed record ScriptRuntimeDispatchResult(
    ScriptRuntimeDispatchStatus Status,
    ScriptEventEnvelope? ScriptEvent = null,
    ScriptExecutionResult? Execution = null);

/// <summary>
/// Host-independent execution coordinator for one script runtime instance. It serializes handlers,
/// owns the bounded event queue, applies timeout/cancellation contracts, contains handler faults and
/// records diagnostics without choosing a concrete Python interpreter.
/// </summary>
public sealed class ScriptRuntimeExecutionCoordinator : IAsyncDisposable
{
    private readonly object _lifecycleSync = new();
    private readonly PythonScriptDefinition _script;
    private readonly string _runtimeInstanceId;
    private readonly ScriptExecutionPolicy _policy;
    private readonly IPythonScriptHandlerExecutor _executor;
    private readonly BoundedScriptEventQueue _queue;
    private readonly ScriptRuntimeDiagnosticsTracker _diagnostics;
    private readonly SemaphoreSlim _processingGate = new(1, 1);
    private readonly CancellationTokenSource _disposeCancellation = new();
    private bool _disposed;

    public ScriptRuntimeExecutionCoordinator(
        PythonScriptDefinition script,
        string runtimeInstanceId,
        ScriptExecutionPolicy policy,
        IPythonScriptHandlerExecutor executor)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(executor);

        if (!script.Enabled)
            throw new InvalidOperationException(
                $"Disabled script '{script.Path}' cannot create a runtime execution coordinator.");
        if (string.IsNullOrWhiteSpace(runtimeInstanceId))
            throw new ArgumentException("Runtime instance ID is required.", nameof(runtimeInstanceId));

        _script = script;
        _runtimeInstanceId = runtimeInstanceId;
        _policy = policy;
        _executor = executor;
        _queue = new BoundedScriptEventQueue(script.Id, runtimeInstanceId, policy);
        _diagnostics = new ScriptRuntimeDiagnosticsTracker(script.Id, runtimeInstanceId, policy);
    }

    public Guid ScriptId => _script.Id;

    public string RuntimeInstanceId => _runtimeInstanceId;

    public int QueuedEventCount => _queue.Count;

    public ScriptEventEnqueueResult Enqueue(
        ScriptEventIdentity identity,
        DateTimeOffset? enqueuedAt = null)
    {
        ArgumentNullException.ThrowIfNull(identity);

        lock (_lifecycleSync)
        {
            ThrowIfDisposed();
            ValidateDeclaredEvent(identity);

            var result = _queue.Enqueue(identity, enqueuedAt);
            _diagnostics.RecordQueueResult(result);
            return result;
        }
    }

    public async ValueTask<ScriptRuntimeDispatchResult> ProcessNextAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        using var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _disposeCancellation.Token);

        await _processingGate.WaitAsync(waitCancellation.Token).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();

            var current = _diagnostics.Snapshot(
                activeSubscriptions: 0,
                queuedEvents: _queue.Count);

            if (current.IsThrottled)
                return new(ScriptRuntimeDispatchStatus.Throttled);

            if (!_queue.TryDequeue(out var scriptEvent) || scriptEvent is null)
                return new(ScriptRuntimeDispatchStatus.NoEvent);

            var execution = await ExecuteEventAsync(
                    scriptEvent,
                    cancellationToken)
                .ConfigureAwait(false);

            _diagnostics.RecordExecution(execution);

            return new(
                ScriptRuntimeDispatchStatus.Executed,
                scriptEvent,
                execution);
        }
        finally
        {
            _processingGate.Release();
        }
    }

    public ScriptRuntimeDiagnosticsSnapshot GetDiagnostics(
        int activeSubscriptions = 0) =>
        _diagnostics.Snapshot(
            activeSubscriptions,
            _queue.Count);

    public void ResetThrottle() => _diagnostics.ResetThrottle();

    public async ValueTask DisposeAsync()
    {
        lock (_lifecycleSync)
        {
            if (_disposed)
                return;

            _disposed = true;
            _disposeCancellation.Cancel();
        }

        await _processingGate.WaitAsync().ConfigureAwait(false);
        try
        {
            _queue.Dispose();
        }
        finally
        {
            _processingGate.Release();
            _processingGate.Dispose();
            _disposeCancellation.Dispose();
        }
    }

    private async ValueTask<ScriptExecutionResult> ExecuteEventAsync(
        ScriptEventEnvelope scriptEvent,
        CancellationToken callerCancellation)
    {
        using var timeoutCancellation = new CancellationTokenSource(_policy.HandlerTimeout);
        using var executionCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            callerCancellation,
            _disposeCancellation.Token,
            timeoutCancellation.Token);

        var lease = ScriptExecutionLease.Create(
            _script.Id,
            _runtimeInstanceId,
            scriptEvent.Identity.HandlerName,
            _policy,
            executionCancellation.Token);

        var timestamp = Stopwatch.GetTimestamp();
        ScriptExecutionStatus status;
        string? sanitizedError = null;

        try
        {
            await _executor.ExecuteAsync(
                    _script,
                    scriptEvent,
                    lease)
                .ConfigureAwait(false);

            status = ResolveCompletionStatus(
                callerCancellation,
                timeoutCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            status = ResolveCancellationStatus(
                callerCancellation,
                timeoutCancellation.Token);
        }
        catch (Exception exception)
        {
            status = ScriptExecutionStatus.Faulted;
            sanitizedError = SanitizeException(exception);
        }

        return new(
            _script.Id,
            _runtimeInstanceId,
            scriptEvent.Identity.HandlerName,
            status,
            Stopwatch.GetElapsedTime(timestamp),
            DateTimeOffset.UtcNow,
            sanitizedError);
    }

    private ScriptExecutionStatus ResolveCompletionStatus(
        CancellationToken callerCancellation,
        CancellationToken timeoutCancellation)
    {
        if (callerCancellation.IsCancellationRequested ||
            _disposeCancellation.IsCancellationRequested)
        {
            return ScriptExecutionStatus.Cancelled;
        }

        return timeoutCancellation.IsCancellationRequested
            ? ScriptExecutionStatus.TimedOut
            : ScriptExecutionStatus.Completed;
    }

    private ScriptExecutionStatus ResolveCancellationStatus(
        CancellationToken callerCancellation,
        CancellationToken timeoutCancellation)
    {
        if (callerCancellation.IsCancellationRequested ||
            _disposeCancellation.IsCancellationRequested)
        {
            return ScriptExecutionStatus.Cancelled;
        }

        return timeoutCancellation.IsCancellationRequested
            ? ScriptExecutionStatus.TimedOut
            : ScriptExecutionStatus.Cancelled;
    }

    private void ValidateDeclaredEvent(ScriptEventIdentity identity)
    {
        if (!ScriptScopeEventRules.IsAllowed(_script.Scope, identity.EventKind))
        {
            throw new InvalidOperationException(
                $"Event '{identity.EventKind}' is not valid for script scope '{_script.Scope}'.");
        }

        var declared = _script.EntryPoints.Any(entry =>
            entry.EventKind == identity.EventKind &&
            string.Equals(entry.HandlerName, identity.HandlerName, StringComparison.Ordinal) &&
            string.Equals(entry.TargetReference, identity.TargetReference, StringComparison.Ordinal));

        if (!declared)
        {
            throw new InvalidOperationException(
                $"Event '{identity.EventKind}:{identity.HandlerName}:{identity.TargetReference}' is not declared by script '{_script.Path}'.");
        }
    }

    private void ThrowIfDisposed()
    {
        lock (_lifecycleSync)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ScriptRuntimeExecutionCoordinator));
        }
    }

    private static string SanitizeException(Exception exception)
    {
        var diagnostic = exception is ScriptExecutionDiagnosticException trustedDiagnostic
            ? $"{exception.GetType().Name}: {trustedDiagnostic.SanitizedMessage}"
            : exception.GetType().Name;

        return diagnostic
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
    }
}
