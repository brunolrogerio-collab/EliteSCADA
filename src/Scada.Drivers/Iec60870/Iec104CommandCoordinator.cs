namespace Scada.Drivers.Iec60870;

public enum Iec104CommandOutcome
{
    Accepted,
    Completed,
    Rejected,
    TimedOut,
    Ambiguous,
    Cancelled
}

public sealed record Iec104CommandResult(
    Iec104CommandOutcome Outcome,
    Iec104CommandState ProtocolState,
    bool ExecuteWasTransmitted,
    bool WasAccepted,
    string? Detail = null);

public sealed record Iec104CommandExecutionOptions
{
    public int MaxConcurrentCommands { get; init; } = 8;
    public TimeSpan ConfirmationTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan CompletionTimeout { get; init; } = TimeSpan.FromSeconds(10);

    public void Validate()
    {
        if (MaxConcurrentCommands is < 1 or > 256)
            throw new ArgumentOutOfRangeException(nameof(MaxConcurrentCommands), MaxConcurrentCommands, "IEC-104 command concurrency must be in the range 1..256.");
        if (ConfirmationTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ConfirmationTimeout), "IEC-104 command confirmation timeout must be greater than zero.");
        if (CompletionTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(CompletionTimeout), "IEC-104 command completion timeout must be greater than zero.");
    }
}

/// <summary>
/// Correlates IEC-104 control responses with in-flight transactions while preserving a single ASDU read loop.
/// One physical point can have only one in-flight command. Data Source-level concurrency is bounded separately.
/// </summary>
public sealed class Iec104CommandCoordinator : IDisposable
{
    private readonly IIec104ClientAdapter _adapter;
    private readonly Iec104CommandExecutionOptions _options;
    private readonly SemaphoreSlim _concurrency;
    private readonly object _gate = new();
    private readonly Dictionary<Iec104CommandPointKey, PendingCommand> _pending = new();
    private bool _disposed;

    public Iec104CommandCoordinator(
        IIec104ClientAdapter adapter,
        Iec104CommandExecutionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        var effectiveOptions = options ?? new Iec104CommandExecutionOptions();
        effectiveOptions.Validate();

        _adapter = adapter;
        _options = effectiveOptions;
        _concurrency = new SemaphoreSlim(effectiveOptions.MaxConcurrentCommands, effectiveOptions.MaxConcurrentCommands);
    }

    public int InFlightCount
    {
        get
        {
            lock (_gate)
                return _pending.Count;
        }
    }

    public async Task<Iec104CommandResult> ExecuteAsync(
        Iec104CommandTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ThrowIfDisposed();

        bool admitted;
        try
        {
            admitted = await _concurrency.WaitAsync(TimeSpan.Zero, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result(transaction, Iec104CommandOutcome.Cancelled, false, false, "Command was cancelled before IEC-104 execution admission.");
        }

        if (!admitted)
        {
            return Result(
                transaction,
                Iec104CommandOutcome.Rejected,
                false,
                false,
                "IEC-104 Data Source command concurrency limit is already reached; command was not queued or sent.");
        }

        var key = new Iec104CommandPointKey(transaction.CommonAddress, transaction.InformationObjectAddress.Value);
        PendingCommand? pending = null;
        try
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                if (_pending.ContainsKey(key))
                {
                    return Result(
                        transaction,
                        Iec104CommandOutcome.Rejected,
                        false,
                        false,
                        "Another IEC-104 command is already in flight for the same Common Address and IOA.");
                }

                pending = new PendingCommand(transaction);
                _pending.Add(key, pending);
            }

            return await ExecuteCoreAsync(pending, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (pending is not null)
            {
                lock (_gate)
                    _pending.Remove(key);
            }

            _concurrency.Release();
        }
    }

    public bool TryObserveResponse(Iec104AsduEnvelope asdu)
    {
        ArgumentNullException.ThrowIfNull(asdu);
        ThrowIfDisposed();

        lock (_gate)
        {
            foreach (var pending in _pending.Values)
            {
                if (!pending.Transaction.ObserveResponse(asdu))
                    continue;

                SignalChanged(pending);
                return true;
            }
        }

        return false;
    }

    public void FailAll(Exception failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        lock (_gate)
        {
            foreach (var pending in _pending.Values)
            {
                pending.SessionFailure ??= failure;
                SignalChanged(pending);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            foreach (var pending in _pending.Values)
            {
                pending.SessionFailure ??= new ObjectDisposedException(nameof(Iec104CommandCoordinator));
                SignalChanged(pending);
            }
        }

        _concurrency.Dispose();
    }

    private async Task<Iec104CommandResult> ExecuteCoreAsync(
        PendingCommand pending,
        CancellationToken cancellationToken)
    {
        var transaction = pending.Transaction;
        var executeWasTransmitted = false;
        var wasAccepted = false;

        Iec104AsduEnvelope initialRequest;
        lock (_gate)
            initialRequest = transaction.CreateInitialRequest();

        if (transaction.Mode == Iec104CommandMode.SelectBeforeOperate)
        {
            try
            {
                await _adapter.SendAsync(initialRequest, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Result(transaction, Iec104CommandOutcome.Cancelled, false, false, "Select phase was cancelled before any execute request was sent.");
            }
            catch (Exception ex)
            {
                return Result(transaction, Iec104CommandOutcome.TimedOut, false, false, $"Select phase failed before execute: {ex.Message}");
            }

            try
            {
                var selectionState = await WaitForStateAsync(
                    pending,
                    static state => state is Iec104CommandState.Selected or Iec104CommandState.Rejected,
                    _options.ConfirmationTimeout,
                    cancellationToken).ConfigureAwait(false);

                if (selectionState == Iec104CommandState.Rejected)
                    return Result(transaction, Iec104CommandOutcome.Rejected, false, false, "Remote station rejected the IEC-104 select phase.");
            }
            catch (TimeoutException)
            {
                return Result(transaction, Iec104CommandOutcome.TimedOut, false, false, "IEC-104 select confirmation timed out; execute was not sent.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Result(transaction, Iec104CommandOutcome.Cancelled, false, false, "Command was cancelled after selection and before execute.");
            }
            catch (PendingSessionFailureException ex)
            {
                return Result(transaction, Iec104CommandOutcome.TimedOut, false, false, $"IEC-104 session failed before execute: {ex.InnerException?.Message ?? ex.Message}");
            }

            Iec104AsduEnvelope executeRequest;
            lock (_gate)
                executeRequest = transaction.CreateExecuteAfterSelection();

            var executeSendResult = await TrySendExecuteAsync(transaction, executeRequest, cancellationToken).ConfigureAwait(false);
            if (executeSendResult is not null)
                return executeSendResult;

            executeWasTransmitted = true;
        }
        else
        {
            var executeSendResult = await TrySendExecuteAsync(transaction, initialRequest, cancellationToken).ConfigureAwait(false);
            if (executeSendResult is not null)
                return executeSendResult;

            executeWasTransmitted = true;
        }

        try
        {
            var confirmationState = await WaitForStateAsync(
                pending,
                static state => state is Iec104CommandState.Accepted or Iec104CommandState.Completed or Iec104CommandState.Rejected,
                _options.ConfirmationTimeout,
                cancellationToken).ConfigureAwait(false);

            wasAccepted = transaction.ExecuteWasAccepted;
            if (confirmationState == Iec104CommandState.Rejected)
            {
                return wasAccepted
                    ? Result(transaction, Iec104CommandOutcome.Rejected, executeWasTransmitted, true, "Remote station rejected command termination after positively confirming execute.")
                    : Result(transaction, Iec104CommandOutcome.Rejected, executeWasTransmitted, false, "Remote station rejected the IEC-104 execute request.");
            }
        }
        catch (TimeoutException)
        {
            return Result(transaction, Iec104CommandOutcome.Ambiguous, executeWasTransmitted, false, "Execute request was transmitted but no definitive activation confirmation arrived before timeout.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result(transaction, Iec104CommandOutcome.Ambiguous, executeWasTransmitted, false, "Command wait was cancelled after execute transmission; physical outcome is unknown.");
        }
        catch (PendingSessionFailureException ex)
        {
            return Result(transaction, Iec104CommandOutcome.Ambiguous, executeWasTransmitted, transaction.ExecuteWasAccepted, $"IEC-104 session failed after execute transmission and before confirmation became observable: {ex.InnerException?.Message ?? ex.Message}");
        }

        try
        {
            var completionState = await WaitForStateAsync(
                pending,
                static state => state is Iec104CommandState.Completed or Iec104CommandState.Rejected,
                _options.CompletionTimeout,
                cancellationToken).ConfigureAwait(false);

            return completionState == Iec104CommandState.Completed
                ? Result(transaction, Iec104CommandOutcome.Completed, executeWasTransmitted, true, "Remote station confirmed IEC-104 activation termination.")
                : Result(transaction, Iec104CommandOutcome.Rejected, executeWasTransmitted, transaction.ExecuteWasAccepted, "Remote station rejected command completion after previously accepting execute.");
        }
        catch (TimeoutException)
        {
            return Result(transaction, Iec104CommandOutcome.Accepted, executeWasTransmitted, wasAccepted, "Execute was positively confirmed, but no activation termination arrived before the completion observation timeout.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result(transaction, Iec104CommandOutcome.Ambiguous, executeWasTransmitted, transaction.ExecuteWasAccepted, "Command was accepted, then observation was cancelled before completion became known.");
        }
        catch (PendingSessionFailureException ex)
        {
            return Result(transaction, Iec104CommandOutcome.Ambiguous, executeWasTransmitted, transaction.ExecuteWasAccepted, $"Command was accepted, then the IEC-104 session failed before completion became known: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    private async Task<Iec104CommandResult?> TrySendExecuteAsync(
        Iec104CommandTransaction transaction,
        Iec104AsduEnvelope request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _adapter.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (Iec104AmbiguousTransmissionException ex)
        {
            return Result(transaction, Iec104CommandOutcome.Ambiguous, true, false, ex.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return _adapter.IsConnected
                ? Result(transaction, Iec104CommandOutcome.Cancelled, false, false, "Execute send was cancelled before the transport reported a session failure.")
                : Result(transaction, Iec104CommandOutcome.Ambiguous, true, false, "Execute send was cancelled while the transport also failed; transmission may have occurred.");
        }
        catch (Exception ex)
        {
            return _adapter.IsConnected
                ? Result(transaction, Iec104CommandOutcome.TimedOut, false, false, $"Execute request failed before the transport reported transmission ambiguity: {ex.Message}")
                : Result(transaction, Iec104CommandOutcome.Ambiguous, true, false, $"IEC-104 transport failed while sending execute; transmission may have occurred: {ex.Message}");
        }
    }

    private async Task<Iec104CommandState> WaitForStateAsync(
        PendingCommand pending,
        Func<Iec104CommandState, bool> terminalState,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        while (true)
        {
            Task changed;
            Iec104CommandState state;
            Exception? sessionFailure;

            lock (_gate)
            {
                state = pending.Transaction.State;
                sessionFailure = pending.SessionFailure;
                changed = pending.Changed.Task;
            }

            if (terminalState(state))
                return state;
            if (sessionFailure is not null)
                throw new PendingSessionFailureException(sessionFailure);

            try
            {
                await changed.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
            {
                throw new TimeoutException("IEC-104 command response phase timed out.");
            }
        }
    }

    private static Iec104CommandResult Result(
        Iec104CommandTransaction transaction,
        Iec104CommandOutcome outcome,
        bool executeWasTransmitted,
        bool wasAccepted,
        string? detail) =>
        new(outcome, transaction.State, executeWasTransmitted, wasAccepted, detail);

    private static void SignalChanged(PendingCommand pending)
    {
        var previous = pending.Changed;
        pending.Changed = NewSignal();
        previous.TrySetResult(true);
    }

    private static TaskCompletionSource<bool> NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Iec104CommandCoordinator));
    }

    private readonly record struct Iec104CommandPointKey(ushort CommonAddress, int InformationObjectAddress);

    private sealed class PendingCommand
    {
        public PendingCommand(Iec104CommandTransaction transaction)
        {
            Transaction = transaction;
        }

        public Iec104CommandTransaction Transaction { get; }
        public Exception? SessionFailure { get; set; }
        public TaskCompletionSource<bool> Changed { get; set; } = NewSignal();
    }

    private sealed class PendingSessionFailureException : Exception
    {
        public PendingSessionFailureException(Exception innerException)
            : base("IEC-104 session failed while a command was awaiting a protocol response.", innerException)
        {
        }
    }
}
