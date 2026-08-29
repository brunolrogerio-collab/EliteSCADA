using System.Diagnostics;

namespace Scada.Drivers.Iec60870;

/// <summary>
/// Long-lived IEC-104 client surface for a Data Source. It owns reconnect attempts and exposes commands
/// only to the currently active session. Commands are never stored for replay across reconnects.
/// </summary>
public sealed class Iec104ManagedClient
{
    private const int MaximumDiagnosticErrorLength = 512;

    private readonly Func<IIec104ClientAdapter> _adapterFactory;
    private readonly string _host;
    private readonly int _port;
    private readonly Iec104SessionOptions _sessionOptions;
    private readonly TimeZoneInfo _stationTimeZone;
    private readonly ushort[] _commonAddresses;
    private readonly byte _originatorAddress;
    private readonly Iec104ReconnectPolicy _reconnectPolicy;
    private readonly Iec104CommandExecutionOptions _commandOptions;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
    private readonly object _gate = new();
    private readonly string _runtimeInstanceId = Guid.NewGuid().ToString("N");

    private Iec104ClientSessionRunner? _activeSession;
    private int _attempt;
    private long _sessionFailures;
    private long _observedPointUpdates;
    private long _commandsRequested;
    private long _commandsAccepted;
    private long _commandsCompleted;
    private long _commandsRejected;
    private long _commandsTimedOut;
    private long _commandsAmbiguous;
    private long _commandsCancelled;
    private DateTimeOffset? _lastSessionAttemptAt;
    private DateTimeOffset? _lastObservedPointAt;
    private DateTimeOffset? _lastFailureAt;
    private string? _lastError;
    private int? _lastFailedAttempt;
    private TimeSpan? _lastReconnectDelay;
    private bool? _lastBackoffWasReset;

    public Iec104ManagedClient(
        Func<IIec104ClientAdapter> adapterFactory,
        string host,
        int port,
        Iec104SessionOptions sessionOptions,
        TimeZoneInfo stationTimeZone,
        IEnumerable<ushort> commonAddresses,
        Iec104ReconnectPolicy? reconnectPolicy = null,
        Iec104CommandExecutionOptions? commandOptions = null,
        byte originatorAddress = 0,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        ArgumentNullException.ThrowIfNull(adapterFactory);
        ArgumentNullException.ThrowIfNull(sessionOptions);
        ArgumentNullException.ThrowIfNull(stationTimeZone);
        ArgumentNullException.ThrowIfNull(commonAddresses);
        if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("IEC-104 host is required.", nameof(host));
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));

        sessionOptions.Validate();
        var addresses = commonAddresses.Distinct().OrderBy(static value => value).ToArray();
        if (addresses.Length == 0)
            throw new ArgumentException("IEC-104 managed client requires at least one Common Address.", nameof(commonAddresses));

        var effectiveReconnectPolicy = reconnectPolicy ?? new Iec104ReconnectPolicy();
        effectiveReconnectPolicy.Validate();
        var effectiveCommandOptions = commandOptions ?? new Iec104CommandExecutionOptions
        {
            ConfirmationTimeout = sessionOptions.T1,
            CompletionTimeout = sessionOptions.T1
        };
        effectiveCommandOptions.Validate();

        _adapterFactory = adapterFactory;
        _host = host.Trim();
        _port = port;
        _sessionOptions = sessionOptions;
        _stationTimeZone = stationTimeZone;
        _commonAddresses = addresses;
        _reconnectPolicy = effectiveReconnectPolicy;
        _commandOptions = effectiveCommandOptions;
        _originatorAddress = originatorAddress;
        _delayAsync = delayAsync ?? static (delay, cancellationToken) => Task.Delay(delay, cancellationToken);
    }

    public int ReconnectAttempt => Volatile.Read(ref _attempt);

    public Iec104SessionState SessionState
    {
        get
        {
            lock (_gate)
                return _activeSession?.State ?? Iec104SessionState.Stopped;
        }
    }

    public int InFlightCommandCount
    {
        get
        {
            lock (_gate)
                return _activeSession?.InFlightCommandCount ?? 0;
        }
    }

    public Iec104ManagedDiagnosticSnapshot GetDiagnostics()
    {
        lock (_gate)
        {
            return new Iec104ManagedDiagnosticSnapshot(
                _runtimeInstanceId,
                _host,
                _port,
                _activeSession?.State ?? Iec104SessionState.Stopped,
                Volatile.Read(ref _attempt),
                _activeSession?.InFlightCommandCount ?? 0,
                _commonAddresses.ToArray(),
                _sessionOptions.T0,
                _sessionOptions.T1,
                _sessionOptions.T2,
                _sessionOptions.T3,
                _sessionOptions.K,
                _sessionOptions.W,
                Interlocked.Read(ref _sessionFailures),
                Interlocked.Read(ref _observedPointUpdates),
                DateTimeOffset.UtcNow,
                _lastSessionAttemptAt,
                _lastObservedPointAt,
                _lastFailureAt,
                _lastError,
                _lastFailedAttempt,
                _lastReconnectDelay,
                _lastBackoffWasReset,
                new Iec104CommandDiagnosticCounters(
                    Interlocked.Read(ref _commandsRequested),
                    Interlocked.Read(ref _commandsAccepted),
                    Interlocked.Read(ref _commandsCompleted),
                    Interlocked.Read(ref _commandsRejected),
                    Interlocked.Read(ref _commandsTimedOut),
                    Interlocked.Read(ref _commandsAmbiguous),
                    Interlocked.Read(ref _commandsCancelled)),
                _activeSession?.GetTransportDiagnostics());
        }
    }

    public async Task<Iec104CommandResult> ExecuteCommandAsync(
        Iec104CommandTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        Interlocked.Increment(ref _commandsRequested);

        Iec104ClientSessionRunner? session;
        lock (_gate)
            session = _activeSession;

        Iec104CommandResult result;
        if (session is null)
        {
            result = new Iec104CommandResult(
                Iec104CommandOutcome.Rejected,
                transaction.State,
                ExecuteWasTransmitted: false,
                WasAccepted: false,
                "IEC-104 Data Source has no active session; command was not queued for replay.");
        }
        else
        {
            result = await session.ExecuteCommandAsync(transaction, cancellationToken).ConfigureAwait(false);
        }

        RecordCommandOutcome(result.Outcome);
        return result;
    }

    public async Task RunAsync(
        Func<Iec104DecodedPoint, CancellationToken, ValueTask> onObservedPoint,
        Func<Iec104ReconnectFailure, CancellationToken, ValueTask>? onReconnectFailure = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onObservedPoint);

        var backoff = new Iec104ReconnectBackoff(_reconnectPolicy);
        Volatile.Write(ref _attempt, 0);

        async ValueTask ObservePointAsync(Iec104DecodedPoint point, CancellationToken pointCancellationToken)
        {
            Interlocked.Increment(ref _observedPointUpdates);
            lock (_gate)
                _lastObservedPointAt = DateTimeOffset.UtcNow;
            await onObservedPoint(point, pointCancellationToken).ConfigureAwait(false);
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var attempt = Interlocked.Increment(ref _attempt);
            var startedTimestamp = Stopwatch.GetTimestamp();

            lock (_gate)
                _lastSessionAttemptAt = DateTimeOffset.UtcNow;

            await using var adapter = _adapterFactory()
                ?? throw new InvalidOperationException("IEC-104 adapter factory returned null.");
            var session = new Iec104ClientSessionRunner(
                adapter,
                _host,
                _port,
                _sessionOptions,
                _stationTimeZone,
                _commonAddresses,
                _originatorAddress,
                _commandOptions);

            lock (_gate)
                _activeSession = session;

            try
            {
                await session.RunAsync(ObservePointAsync, cancellationToken).ConfigureAwait(false);
                if (!cancellationToken.IsCancellationRequested)
                    throw new IOException("IEC-104 managed session ended without cancellation; reconnect is required.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception failure)
            {
                var sessionDuration = Stopwatch.GetElapsedTime(startedTimestamp);
                var reset = sessionDuration >= _reconnectPolicy.StableSessionThreshold;
                if (reset)
                    backoff.Reset();

                var delay = backoff.NextDelay();
                var reconnectFailure = new Iec104ReconnectFailure(attempt, failure, sessionDuration, delay, reset);
                Interlocked.Increment(ref _sessionFailures);
                lock (_gate)
                {
                    _lastFailureAt = DateTimeOffset.UtcNow;
                    _lastError = SanitizeError(failure);
                    _lastFailedAttempt = attempt;
                    _lastReconnectDelay = delay;
                    _lastBackoffWasReset = reset;
                }

                if (onReconnectFailure is not null)
                {
                    await onReconnectFailure(
                        reconnectFailure,
                        cancellationToken).ConfigureAwait(false);
                }

                lock (_gate)
                {
                    if (ReferenceEquals(_activeSession, session))
                        _activeSession = null;
                }

                await _delayAsync(delay, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                lock (_gate)
                {
                    if (ReferenceEquals(_activeSession, session))
                        _activeSession = null;
                }
            }
        }
    }

    private void RecordCommandOutcome(Iec104CommandOutcome outcome)
    {
        switch (outcome)
        {
            case Iec104CommandOutcome.Accepted:
                Interlocked.Increment(ref _commandsAccepted);
                break;
            case Iec104CommandOutcome.Completed:
                Interlocked.Increment(ref _commandsCompleted);
                break;
            case Iec104CommandOutcome.Rejected:
                Interlocked.Increment(ref _commandsRejected);
                break;
            case Iec104CommandOutcome.TimedOut:
                Interlocked.Increment(ref _commandsTimedOut);
                break;
            case Iec104CommandOutcome.Ambiguous:
                Interlocked.Increment(ref _commandsAmbiguous);
                break;
            case Iec104CommandOutcome.Cancelled:
                Interlocked.Increment(ref _commandsCancelled);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown IEC-104 command outcome.");
        }
    }

    private static string SanitizeError(Exception exception)
    {
        var message = exception.Message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return message.Length <= MaximumDiagnosticErrorLength
            ? message
            : message[..MaximumDiagnosticErrorLength];
    }
}
