namespace Scada.Drivers.Iec60870;

public sealed class Iec104ClientSessionRunner
{
    private readonly IIec104ClientAdapter _adapter;
    private readonly string _host;
    private readonly int _port;
    private readonly Iec104SessionOptions _options;
    private readonly TimeZoneInfo _stationTimeZone;
    private readonly ushort[] _commonAddresses;
    private readonly byte _originatorAddress;
    private readonly Iec104SessionStateMachine _stateMachine = new();
    private readonly Dictionary<ushort, Iec104GeneralInterrogationTransaction> _generalInterrogations = new();
    private readonly Iec104CommandCoordinator _commandCoordinator;

    public Iec104ClientSessionRunner(
        IIec104ClientAdapter adapter,
        string host,
        int port,
        Iec104SessionOptions options,
        TimeZoneInfo stationTimeZone,
        IEnumerable<ushort> commonAddresses,
        byte originatorAddress = 0,
        Iec104CommandExecutionOptions? commandOptions = null)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(stationTimeZone);
        ArgumentNullException.ThrowIfNull(commonAddresses);
        if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("IEC-104 host is required.", nameof(host));
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));

        options.Validate();
        var addresses = commonAddresses.Distinct().OrderBy(static value => value).ToArray();
        if (addresses.Length == 0)
            throw new ArgumentException("IEC-104 session requires at least one Common Address for the initial General Interrogation profile.", nameof(commonAddresses));

        var effectiveCommandOptions = commandOptions ?? new Iec104CommandExecutionOptions
        {
            ConfirmationTimeout = options.T1,
            CompletionTimeout = options.T1
        };
        effectiveCommandOptions.Validate();

        _adapter = adapter;
        _host = host.Trim();
        _port = port;
        _options = options;
        _stationTimeZone = stationTimeZone;
        _commonAddresses = addresses;
        _originatorAddress = originatorAddress;
        _commandCoordinator = new Iec104CommandCoordinator(adapter, effectiveCommandOptions);
    }

    public Iec104SessionState State => _stateMachine.State;

    public int InFlightCommandCount => _commandCoordinator.InFlightCount;

    public Iec104TcpAdapterDiagnosticSnapshot? GetTransportDiagnostics() =>
        (_adapter as IIec104TransportDiagnosticsSource)?.GetTransportDiagnostics();

    public IReadOnlyDictionary<ushort, Iec104GeneralInterrogationState> GeneralInterrogationStates =>
        _generalInterrogations.ToDictionary(static pair => pair.Key, static pair => pair.Value.State);

    public Task<Iec104CommandResult> ExecuteCommandAsync(
        Iec104CommandTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (State != Iec104SessionState.Running || !_adapter.IsConnected)
        {
            return Task.FromResult(new Iec104CommandResult(
                Iec104CommandOutcome.Rejected,
                transaction.State,
                ExecuteWasTransmitted: false,
                WasAccepted: false,
                "IEC-104 session is not in Running state; command was not sent."));
        }

        return _commandCoordinator.ExecuteAsync(transaction, cancellationToken);
    }

    public async Task RunAsync(
        Func<Iec104DecodedPoint, CancellationToken, ValueTask> onObservedPoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onObservedPoint);
        if (State != Iec104SessionState.Stopped)
            throw new InvalidOperationException($"IEC-104 session runner can only start from Stopped state; current state is {State}.");

        var dataTransferStarted = false;
        Exception? operationFailure = null;

        try
        {
            _stateMachine.TransitionTo(Iec104SessionState.Connecting);
            await _adapter.ConnectAsync(_host, _port, _options, cancellationToken).ConfigureAwait(false);

            _stateMachine.TransitionTo(Iec104SessionState.TcpConnected);
            _stateMachine.TransitionTo(Iec104SessionState.StartingDataTransfer);
            await _adapter.StartDataTransferAsync(cancellationToken).ConfigureAwait(false);
            dataTransferStarted = true;
            _stateMachine.TransitionTo(Iec104SessionState.Running);

            _generalInterrogations.Clear();
            foreach (var commonAddress in _commonAddresses)
            {
                var transaction = new Iec104GeneralInterrogationTransaction(commonAddress, _originatorAddress);
                _generalInterrogations.Add(commonAddress, transaction);
                await _adapter.SendAsync(transaction.CreateActivation(), cancellationToken).ConfigureAwait(false);
            }

            await foreach (var asdu in _adapter.ReadAsync(cancellationToken).WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                if (_commandCoordinator.TryObserveResponse(asdu))
                    continue;
                if (TryObserveGeneralInterrogation(asdu))
                    continue;
                if (!Iec104InformationObjectDecoder.IsSupported(asdu.Header.TypeId))
                    continue;

                var points = Iec104InformationObjectDecoder.Decode(asdu, _stationTimeZone);
                foreach (var point in points)
                    await onObservedPoint(point, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            operationFailure = ex;
            _commandCoordinator.FailAll(ex);
            if (State is not (Iec104SessionState.Stopped or Iec104SessionState.Stopping or Iec104SessionState.Faulted))
                _stateMachine.TransitionTo(Iec104SessionState.Stopping);
            throw;
        }
        catch (Exception ex)
        {
            operationFailure = ex;
            _commandCoordinator.FailAll(ex);
            if (State is not (Iec104SessionState.Faulted or Iec104SessionState.Stopped))
                _stateMachine.TransitionTo(Iec104SessionState.Faulted);
            throw;
        }
        finally
        {
            if (operationFailure is null)
                _commandCoordinator.FailAll(new IOException("IEC-104 session ended before pending command outcomes were resolved."));

            try
            {
                await StopTransportAsync(dataTransferStarted).ConfigureAwait(false);
            }
            catch when (operationFailure is not null)
            {
                // Preserve the primary session failure. Cleanup failure belongs in future diagnostics.
            }
        }
    }

    private bool TryObserveGeneralInterrogation(Iec104AsduEnvelope asdu)
    {
        if (asdu.Header.TypeId != Iec104TypeId.CIcNa1)
            return false;
        if (!_generalInterrogations.TryGetValue(asdu.Header.CommonAddress, out var transaction))
            return false;

        return transaction.ObserveControlResponse(asdu);
    }

    private async Task StopTransportAsync(bool dataTransferStarted)
    {
        var faulted = State == Iec104SessionState.Faulted;
        if (!faulted && State is not Iec104SessionState.Stopped and not Iec104SessionState.Stopping)
            _stateMachine.TransitionTo(Iec104SessionState.Stopping);

        using var cleanupCts = new CancellationTokenSource(_options.T0);
        try
        {
            if (dataTransferStarted && _adapter.IsConnected)
                await _adapter.StopDataTransferAsync(cleanupCts.Token).ConfigureAwait(false);
        }
        finally
        {
            if (_adapter.IsConnected)
                await _adapter.DisconnectAsync(cleanupCts.Token).ConfigureAwait(false);

            if (State == Iec104SessionState.Stopping || State == Iec104SessionState.Faulted)
                _stateMachine.TransitionTo(Iec104SessionState.Stopped);
        }
    }
}
