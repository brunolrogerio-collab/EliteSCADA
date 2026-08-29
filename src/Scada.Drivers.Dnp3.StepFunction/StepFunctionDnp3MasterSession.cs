using Step = dnp3;

namespace Scada.Drivers.Dnp3.StepFunction;

public sealed class StepFunctionDnp3MasterSessionFactory : IDnp3MasterSessionFactory
{
    public IDnp3MasterSession Create(Dnp3TcpConnectionOptions connectionOptions)
    {
        ArgumentNullException.ThrowIfNull(connectionOptions);
        connectionOptions.Validate();
        return new StepFunctionDnp3MasterSession(connectionOptions);
    }
}

public sealed class StepFunctionDnp3MasterSession : IDnp3MasterSession
{
    private readonly Dnp3TcpConnectionOptions _connectionOptions;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly object _stateGate = new();

    private Step.Runtime? _runtime;
    private Step.MasterChannel? _channel;
    private Step.AssociationId _association = default!;
    private bool _hasAssociation;
    private bool _everConnected;
    private bool _stopping;
    private Dnp3AssociationOptions? _associationOptions;
    private Func<Dnp3Measurement, CancellationToken, ValueTask>? _measurementHandler;
    private Func<Dnp3SessionState, CancellationToken, ValueTask>? _stateHandler;
    private CancellationTokenSource? _sessionCts;

    private Dnp3SessionState _state = Dnp3SessionState.Stopped;
    private DateTimeOffset _stateChangedAt = DateTimeOffset.UtcNow;
    private DateTimeOffset? _lastSuccessfulCommunicationAt;
    private DateTimeOffset? _lastFailedCommunicationAt;
    private string? _lastError;

    private long _requests;
    private long _successfulOperations;
    private long _failedOperations;
    private long _consecutiveFailures;
    private long _timeouts;
    private long _connections;
    private long _disconnections;
    private long _reconnects;
    private long _readOperations;
    private long _writeOperations;
    private long _startupIntegrityScans;
    private long _class0Scans;
    private long _class1Scans;
    private long _class2Scans;
    private long _class3Scans;
    private long _unsolicitedResponses;
    private long _restartDetections;

    public StepFunctionDnp3MasterSession(Dnp3TcpConnectionOptions connectionOptions)
    {
        ArgumentNullException.ThrowIfNull(connectionOptions);
        connectionOptions.Validate();
        _connectionOptions = connectionOptions;
    }

    public Dnp3SessionState State
    {
        get
        {
            lock (_stateGate) return _state;
        }
    }

    public async ValueTask StartAsync(
        Dnp3AssociationOptions options,
        Func<Dnp3Measurement, CancellationToken, ValueTask> measurementHandler,
        Func<Dnp3SessionState, CancellationToken, ValueTask> stateHandler,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(measurementHandler);
        ArgumentNullException.ThrowIfNull(stateHandler);
        options.Validate();

        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            if (_channel is not null) return;

            _associationOptions = options;
            _measurementHandler = measurementHandler;
            _stateHandler = stateHandler;
            _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _stopping = false;
            _everConnected = false;
            await SetStateAsync(Dnp3SessionState.Connecting, cancellationToken: cancellationToken);

            Step.Runtime? runtime = null;
            Step.MasterChannel? channel = null;
            try
            {
                runtime = new Step.Runtime(new Step.RuntimeConfig { NumCoreThreads = 1 });

                var connectStrategy = new Step.ConnectStrategy()
                    .WithMinConnectDelay(options.ReconnectMinDelay)
                    .WithMaxConnectDelay(options.ReconnectMaxDelay)
                    .WithReconnectDelay(options.ReconnectMinDelay);

                var connectOptions = new Step.ConnectOptions();
                connectOptions.SetTimeout(_connectionOptions.ConnectTimeout);

                channel = Step.MasterChannel.CreateTcpChannel2(
                    runtime,
                    Step.LinkErrorMode.Close,
                    new Step.MasterChannelConfig(_connectionOptions.MasterAddress),
                    new Step.EndpointList(_connectionOptions.SanitizedEndpoint),
                    connectStrategy,
                    connectOptions,
                    new StepFunctionDnp3ClientStateListener(OnClientStateChanged));

                var association = channel.AddAssociation(
                    _connectionOptions.OutstationAddress,
                    BuildAssociationConfig(options),
                    new StepFunctionDnp3ReadHandler(PublishMeasurementAsync),
                    new StepFunctionDnp3AssociationHandler(),
                    new StepFunctionDnp3AssociationInformation(OnTaskStart, OnTaskSuccess, OnTaskFail, OnUnsolicitedResponse));

                AddConfiguredPolls(channel, association, options);

                _runtime = runtime;
                _channel = channel;
                _association = association;
                _hasAssociation = true;
                channel.Enable();
            }
            catch (Exception ex)
            {
                SafeShutdown(channel, runtime);
                _runtime = null;
                _channel = null;
                _hasAssociation = false;
                _sessionCts?.Dispose();
                _sessionCts = null;
                await SetStateAsync(Dnp3SessionState.Faulted, Sanitize(ex.Message), CancellationToken.None);
                throw;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            var channel = _channel;
            var runtime = _runtime;
            if (channel is null && runtime is null)
            {
                if (State != Dnp3SessionState.Stopped)
                    await SetStateAsync(Dnp3SessionState.Stopped, cancellationToken: cancellationToken);
                return;
            }

            _stopping = true;
            await SetStateAsync(Dnp3SessionState.Stopping, cancellationToken: cancellationToken);
            if (_sessionCts is not null) await _sessionCts.CancelAsync();

            Exception? failure = null;
            try
            {
                channel?.Disable();
            }
            catch (Exception ex)
            {
                failure = ex;
            }

            try
            {
                channel?.Shutdown();
            }
            catch (Exception ex)
            {
                failure ??= ex;
            }

            try
            {
                runtime?.Shutdown();
            }
            catch (Exception ex)
            {
                failure ??= ex;
            }
            finally
            {
                _channel = null;
                _runtime = null;
                _hasAssociation = false;
                _association = default!;
                _sessionCts?.Dispose();
                _sessionCts = null;
                _associationOptions = null;
                _measurementHandler = null;
                _stopping = false;
            }

            if (failure is not null)
            {
                RecordFailure(failure.Message, timeout: false);
                await SetStateAsync(Dnp3SessionState.Faulted, Sanitize(failure.Message), CancellationToken.None);
                throw failure;
            }

            await SetStateAsync(Dnp3SessionState.Stopped, cancellationToken: CancellationToken.None);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask<Dnp3CommandResult> ExecuteBinaryAsync(
        ushort index,
        Dnp3BinaryOperation operation,
        Dnp3BinaryCommandProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();
        var channel = RequireOnlineChannel(out var association);
        Interlocked.Increment(ref _writeOperations);

        try
        {
            var commands = new Step.CommandSet();
            commands.AddG12V1U16(index, StepFunctionDnp3Mapping.BuildCrob(operation, profile));
            await channel.Operate(association, StepFunctionDnp3Mapping.MapCommandMode(profile.Mode), commands)
                .WaitAsync(cancellationToken);
            return Dnp3CommandResult.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Dnp3CommandResult.Failure(MapCommandFailureStatus(ex), Sanitize(ex.Message));
        }
    }

    public async ValueTask<Dnp3CommandResult> ExecuteAnalogAsync(
        ushort index,
        object value,
        Dnp3AnalogCommandProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var channel = RequireOnlineChannel(out var association);
        Interlocked.Increment(ref _writeOperations);

        try
        {
            var commands = new Step.CommandSet();
            switch (profile.Variation)
            {
                case Dnp3AnalogOutputVariation.Int32:
                    commands.AddG41V1U16(index, (int)value);
                    break;
                case Dnp3AnalogOutputVariation.Int16:
                    commands.AddG41V2U16(index, (short)value);
                    break;
                case Dnp3AnalogOutputVariation.Float32:
                    commands.AddG41V3U16(index, (float)value);
                    break;
                case Dnp3AnalogOutputVariation.Float64:
                    commands.AddG41V4U16(index, (double)value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(profile), profile.Variation, null);
            }

            await channel.Operate(association, StepFunctionDnp3Mapping.MapCommandMode(profile.Mode), commands)
                .WaitAsync(cancellationToken);
            return Dnp3CommandResult.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Dnp3CommandResult.Failure(MapCommandFailureStatus(ex), Sanitize(ex.Message));
        }
    }

    public Dnp3SessionDiagnosticSnapshot GetDiagnostics()
    {
        Dnp3SessionState state;
        DateTimeOffset stateChangedAt;
        DateTimeOffset? lastSuccess;
        DateTimeOffset? lastFailure;
        string? lastError;
        lock (_stateGate)
        {
            state = _state;
            stateChangedAt = _stateChangedAt;
            lastSuccess = _lastSuccessfulCommunicationAt;
            lastFailure = _lastFailedCommunicationAt;
            lastError = _lastError;
        }

        var success = Interlocked.Read(ref _successfulOperations);
        var failure = Interlocked.Read(ref _failedOperations);
        var total = success + failure;

        return new Dnp3SessionDiagnosticSnapshot(
            _connectionOptions.SanitizedEndpoint,
            state,
            stateChangedAt,
            lastSuccess,
            lastFailure,
            lastError,
            Interlocked.Read(ref _requests),
            success,
            failure,
            Interlocked.Read(ref _consecutiveFailures),
            Interlocked.Read(ref _timeouts),
            Interlocked.Read(ref _connections),
            Interlocked.Read(ref _disconnections),
            Interlocked.Read(ref _reconnects),
            Interlocked.Read(ref _readOperations),
            Interlocked.Read(ref _writeOperations),
            Interlocked.Read(ref _startupIntegrityScans),
            Interlocked.Read(ref _class0Scans),
            Interlocked.Read(ref _class1Scans),
            Interlocked.Read(ref _class2Scans),
            Interlocked.Read(ref _class3Scans),
            Interlocked.Read(ref _unsolicitedResponses),
            Interlocked.Read(ref _restartDetections),
            0,
            total == 0 ? 0d : (double)failure / total);
    }

    private Step.MasterChannel RequireOnlineChannel(out Step.AssociationId association)
    {
        var channel = _channel;
        if (channel is null || !_hasAssociation || State != Dnp3SessionState.Online)
            throw new InvalidOperationException("Step Function DNP3 association is not online; command was not sent or retained.");
        association = _association;
        return channel;
    }

    private static Step.AssociationConfig BuildAssociationConfig(Dnp3AssociationOptions options) =>
        new Step.AssociationConfig(
                StepFunctionDnp3Mapping.MapEventClasses(options.DisableUnsolicitedClassesOnStartup),
                StepFunctionDnp3Mapping.MapEventClasses(options.EnableUnsolicitedClassesAfterIntegrity),
                StepFunctionDnp3Mapping.MapClasses(options.StartupIntegrityClasses),
                StepFunctionDnp3Mapping.MapEventClasses(options.EventScanOnEventsAvailable))
            .WithResponseTimeout(options.ResponseTimeout)
            .WithAutoTimeSync(StepFunctionDnp3Mapping.MapAutoTimeSync(options.TimeSyncMode))
            .WithKeepAliveTimeout(options.KeepAliveTimeout ?? TimeSpan.Zero)
            .WithAutoIntegrityScanOnBufferOverflow(options.IntegrityOnEventBufferOverflow)
            .WithMaxQueuedUserRequests(checked((ushort)options.MaxQueuedUserRequests));

    private static void AddConfiguredPolls(
        Step.MasterChannel channel,
        Step.AssociationId association,
        Dnp3AssociationOptions options)
    {
        if (options.IntegrityPollInterval is { } integrity)
        {
            channel.AddPoll(
                association,
                Step.Request.ClassRequest(
                    options.StartupIntegrityClasses.HasFlag(Dnp3ClassSet.Class0),
                    options.StartupIntegrityClasses.HasFlag(Dnp3ClassSet.Class1),
                    options.StartupIntegrityClasses.HasFlag(Dnp3ClassSet.Class2),
                    options.StartupIntegrityClasses.HasFlag(Dnp3ClassSet.Class3)),
                integrity);
        }

        AddClassPoll(channel, association, Dnp3ClassSet.Class1, options.Class1PollInterval);
        AddClassPoll(channel, association, Dnp3ClassSet.Class2, options.Class2PollInterval);
        AddClassPoll(channel, association, Dnp3ClassSet.Class3, options.Class3PollInterval);
    }

    private static void AddClassPoll(
        Step.MasterChannel channel,
        Step.AssociationId association,
        Dnp3ClassSet pointClass,
        TimeSpan? interval)
    {
        if (interval is null) return;
        channel.AddPoll(
            association,
            Step.Request.ClassRequest(
                false,
                pointClass == Dnp3ClassSet.Class1,
                pointClass == Dnp3ClassSet.Class2,
                pointClass == Dnp3ClassSet.Class3),
            interval.Value);
    }

    private ValueTask PublishMeasurementAsync(Dnp3Measurement measurement)
    {
        var handler = _measurementHandler;
        if (handler is null) return ValueTask.CompletedTask;
        var token = _sessionCts?.Token ?? CancellationToken.None;
        return handler(measurement, token);
    }

    private void OnClientStateChanged(Step.ClientState state)
    {
        if (_stopping) return;

        switch (state)
        {
            case Step.ClientState.Connecting:
                NotifyStateBlocking(_everConnected ? Dnp3SessionState.Reconnecting : Dnp3SessionState.Connecting);
                break;

            case Step.ClientState.Connected:
                Interlocked.Increment(ref _connections);
                _everConnected = true;
                NotifyStateBlocking(Dnp3SessionState.StartupIntegrity);
                break;

            case Step.ClientState.WaitAfterFailedConnect:
                RecordFailure("DNP3 TCP connection attempt failed.", timeout: false);
                NotifyStateBlocking(_everConnected ? Dnp3SessionState.Reconnecting : Dnp3SessionState.Connecting);
                break;

            case Step.ClientState.WaitAfterDisconnect:
                Interlocked.Increment(ref _disconnections);
                Interlocked.Increment(ref _reconnects);
                RecordFailure("DNP3 TCP connection was lost.", timeout: false);
                NotifyStateBlocking(Dnp3SessionState.Reconnecting, "DNP3 TCP connection was lost.");
                break;

            case Step.ClientState.Shutdown:
                NotifyStateBlocking(Dnp3SessionState.Faulted, "Step Function DNP3 channel shut down unexpectedly.");
                break;
        }
    }

    private void OnTaskStart(Step.TaskType taskType, Step.FunctionCode functionCode, byte seq)
    {
        Interlocked.Increment(ref _requests);
        if (taskType == Step.TaskType.StartupIntegrity)
            NotifyStateBlocking(Dnp3SessionState.StartupIntegrity);
        if (taskType == Step.TaskType.ClearRestartBit)
            Interlocked.Increment(ref _restartDetections);
    }

    private void OnTaskSuccess(Step.TaskType taskType, Step.FunctionCode functionCode, byte seq)
    {
        Interlocked.Increment(ref _successfulOperations);
        Interlocked.Exchange(ref _consecutiveFailures, 0);
        lock (_stateGate)
        {
            _lastSuccessfulCommunicationAt = DateTimeOffset.UtcNow;
            _lastError = null;
        }

        if (taskType is Step.TaskType.UserRead or Step.TaskType.PeriodicPoll or Step.TaskType.StartupIntegrity or Step.TaskType.AutoEventScan)
            Interlocked.Increment(ref _readOperations);

        if (taskType == Step.TaskType.StartupIntegrity)
        {
            Interlocked.Increment(ref _startupIntegrityScans);
            var classes = _associationOptions?.StartupIntegrityClasses ?? Dnp3ClassSet.None;
            if (classes.HasFlag(Dnp3ClassSet.Class0)) Interlocked.Increment(ref _class0Scans);
            if (classes.HasFlag(Dnp3ClassSet.Class1)) Interlocked.Increment(ref _class1Scans);
            if (classes.HasFlag(Dnp3ClassSet.Class2)) Interlocked.Increment(ref _class2Scans);
            if (classes.HasFlag(Dnp3ClassSet.Class3)) Interlocked.Increment(ref _class3Scans);
            NotifyStateBlocking(Dnp3SessionState.Online);
        }
    }

    private void OnTaskFail(Step.TaskType taskType, Step.TaskError error)
    {
        var timeout = error == Step.TaskError.ResponseTimeout;
        RecordFailure($"DNP3 task {taskType} failed: {error}.", timeout);
        if (State is Dnp3SessionState.Online or Dnp3SessionState.StartupIntegrity)
            NotifyStateBlocking(Dnp3SessionState.Degraded, $"DNP3 task {taskType} failed: {error}.");
    }

    private void OnUnsolicitedResponse(bool isDuplicate, byte seq)
    {
        Interlocked.Increment(ref _unsolicitedResponses);
        lock (_stateGate) _lastSuccessfulCommunicationAt = DateTimeOffset.UtcNow;
    }

    private void RecordFailure(string? message, bool timeout)
    {
        Interlocked.Increment(ref _failedOperations);
        Interlocked.Increment(ref _consecutiveFailures);
        if (timeout) Interlocked.Increment(ref _timeouts);
        lock (_stateGate)
        {
            _lastFailedCommunicationAt = DateTimeOffset.UtcNow;
            _lastError = Sanitize(message);
        }
    }

    private async ValueTask SetStateAsync(
        Dnp3SessionState state,
        string? error = null,
        CancellationToken cancellationToken = default)
    {
        Func<Dnp3SessionState, CancellationToken, ValueTask>? handler;
        lock (_stateGate)
        {
            if (_state == state && string.Equals(_lastError, error, StringComparison.Ordinal)) return;
            _state = state;
            _stateChangedAt = DateTimeOffset.UtcNow;
            if (!string.IsNullOrWhiteSpace(error)) _lastError = Sanitize(error);
            handler = _stateHandler;
        }

        if (handler is not null) await handler(state, cancellationToken);
    }

    private void NotifyStateBlocking(Dnp3SessionState state, string? error = null)
    {
        try
        {
            SetStateAsync(state, error, CancellationToken.None).AsTask().GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            // Ignore callback cancellation during shutdown.
        }
        catch (Exception ex)
        {
            lock (_stateGate) _lastError = Sanitize(ex.Message);
        }
    }

    private static string MapCommandFailureStatus(Exception ex)
    {
        var name = ex.GetType().Name;
        return name.EndsWith("Exception", StringComparison.Ordinal)
            ? name[..^"Exception".Length].ToUpperInvariant()
            : name.ToUpperInvariant();
    }

    private static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var sanitized = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return sanitized.Length <= 512 ? sanitized : sanitized[..512];
    }

    private static void SafeShutdown(Step.MasterChannel? channel, Step.Runtime? runtime)
    {
        try { channel?.Shutdown(); } catch { }
        try { runtime?.Shutdown(); } catch { }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await StopAsync();
        }
        finally
        {
            _lifecycleGate.Dispose();
        }
    }
}
