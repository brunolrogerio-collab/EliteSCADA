using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Scada.Drivers.Dnp3;

namespace Scada.Drivers.Dnp3.OpenDnp3;

internal sealed class OpenDnp3MasterSession : IDnp3MasterSession
{
    private const string HostEnvironmentVariable = "ELITESCADA_DNP3_HOST_PATH";
    private readonly Dnp3TcpConnectionOptions _connection;
    private readonly object _gate = new();
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly SemaphoreSlim _stdinGate = new(1, 1);
    private readonly ConcurrentDictionary<long, TaskCompletionSource<Dnp3CommandResult>> _pendingCommands = new();
    private Process? _process;
    private CancellationTokenSource? _lifetimeCts;
    private Task? _stdoutPump;
    private Task? _stderrPump;
    private TaskCompletionSource<bool>? _ready;
    private Func<Dnp3Measurement, CancellationToken, ValueTask>? _measurementHandler;
    private Func<Dnp3SessionState, CancellationToken, ValueTask>? _stateHandler;
    private TimeSpan _responseTimeout = TimeSpan.FromSeconds(5);
    private Dnp3SessionState _state = Dnp3SessionState.Stopped;
    private DateTimeOffset _stateChangedAt = DateTimeOffset.UtcNow;
    private DateTimeOffset? _lastSuccessfulCommunicationAt;
    private DateTimeOffset? _lastFailedCommunicationAt;
    private string? _lastError;
    private long _nextRequestId;
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

    public OpenDnp3MasterSession(Dnp3TcpConnectionOptions connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _connection.Validate();
    }

    public Dnp3SessionState State
    {
        get
        {
            lock (_gate) return _state;
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

        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_process is { HasExited: false }) return;

            _measurementHandler = measurementHandler;
            _stateHandler = stateHandler;
            _responseTimeout = options.ResponseTimeout;
            _ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _lifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            await SetStateAsync(Dnp3SessionState.Connecting, cancellationToken).ConfigureAwait(false);

            var startInfo = BuildStartInfo(options);
            var process = new Process { StartInfo = startInfo };
            if (!process.Start())
                throw new InvalidOperationException("OpenDNP3 native host process did not start.");

            process.StandardInput.AutoFlush = true;
            _process = process;
            var lifetimeToken = _lifetimeCts.Token;
            _stdoutPump = PumpStdoutAsync(process, lifetimeToken);
            _stderrPump = PumpStderrAsync(process, lifetimeToken);

            try
            {
                await _ready.Task.WaitAsync(_connection.ConnectTimeout, cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                MarkFailure("OpenDNP3 native host did not become ready before the configured connect timeout.", timedOut: true);
                throw;
            }
        }
        catch
        {
            await CleanupProcessAsync().ConfigureAwait(false);
            await SetStateAsync(Dnp3SessionState.Faulted, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_process is null)
            {
                await SetStateAsync(Dnp3SessionState.Stopped, CancellationToken.None).ConfigureAwait(false);
                return;
            }

            await SetStateAsync(Dnp3SessionState.Stopping, CancellationToken.None).ConfigureAwait(false);
            try
            {
                await SendLineAsync($"{OpenDnp3HostProtocol.VersionToken}\tSTOP", cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                // The helper may already have exited; cleanup below remains authoritative.
            }
            catch (InvalidOperationException)
            {
                // Same as above: never keep the process command for replay.
            }

            await CleanupProcessAsync().ConfigureAwait(false);
            await SetStateAsync(Dnp3SessionState.Stopped, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public ValueTask<Dnp3CommandResult> ExecuteBinaryAsync(
        ushort index,
        Dnp3BinaryOperation operation,
        Dnp3BinaryCommandProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return ExecuteCommandAsync(
            requestId => OpenDnp3HostProtocol.BuildBinaryCommand(requestId, index, operation, profile),
            cancellationToken);
    }

    public ValueTask<Dnp3CommandResult> ExecuteAnalogAsync(
        ushort index,
        object value,
        Dnp3AnalogCommandProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(profile);
        return ExecuteCommandAsync(
            requestId => OpenDnp3HostProtocol.BuildAnalogCommand(requestId, index, value, profile),
            cancellationToken);
    }

    public Dnp3SessionDiagnosticSnapshot GetDiagnostics()
    {
        lock (_gate)
        {
            var completed = _successfulOperations + _failedOperations;
            var failureRate = completed == 0 ? 0d : (double)_failedOperations / completed;
            return new Dnp3SessionDiagnosticSnapshot(
                $"{_connection.Host}:{_connection.Port}",
                _state,
                _stateChangedAt,
                _lastSuccessfulCommunicationAt,
                _lastFailedCommunicationAt,
                _lastError,
                Interlocked.Read(ref _requests),
                Interlocked.Read(ref _successfulOperations),
                Interlocked.Read(ref _failedOperations),
                Interlocked.Read(ref _consecutiveFailures),
                Interlocked.Read(ref _timeouts),
                Interlocked.Read(ref _connections),
                Interlocked.Read(ref _disconnections),
                Interlocked.Read(ref _reconnects),
                Interlocked.Read(ref _readOperations),
                Interlocked.Read(ref _writeOperations),
                Interlocked.Read(ref _startupIntegrityScans),
                RecentFailureRate: failureRate);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        _lifecycleGate.Dispose();
        _stdinGate.Dispose();
    }

    private async ValueTask<Dnp3CommandResult> ExecuteCommandAsync(
        Func<long, string> buildCommand,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (State != Dnp3SessionState.Online || _process is not { HasExited: false })
            return Dnp3CommandResult.Failure("NOT_ONLINE", "DNP3 association is not online; command was not retained for replay.");

        var requestId = Interlocked.Increment(ref _nextRequestId);
        var completion = new TaskCompletionSource<Dnp3CommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pendingCommands.TryAdd(requestId, completion))
            throw new InvalidOperationException("Duplicate OpenDNP3 command request id.");

        Interlocked.Increment(ref _requests);
        Interlocked.Increment(ref _writeOperations);
        try
        {
            await SendLineAsync(buildCommand(requestId), cancellationToken).ConfigureAwait(false);
            try
            {
                var result = await completion.Task.WaitAsync(_responseTimeout, cancellationToken).ConfigureAwait(false);
                if (result.Succeeded) MarkSuccess(); else MarkFailure(result.Message ?? result.Status);
                return result;
            }
            catch (TimeoutException)
            {
                MarkFailure("DNP3 command response timed out.", timedOut: true);
                return Dnp3CommandResult.Failure("TIMEOUT", "DNP3 command response timed out; command was not retained for replay.");
            }
        }
        finally
        {
            _pendingCommands.TryRemove(requestId, out _);
        }
    }

    private ProcessStartInfo BuildStartInfo(Dnp3AssociationOptions options)
    {
        var executable = ResolveHostExecutable();
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        AddOption(startInfo, "--protocol", OpenDnp3HostProtocol.VersionToken);
        AddOption(startInfo, "--host", _connection.Host);
        AddOption(startInfo, "--port", _connection.Port);
        AddOption(startInfo, "--master-address", _connection.MasterAddress);
        AddOption(startInfo, "--outstation-address", _connection.OutstationAddress);
        AddOption(startInfo, "--response-timeout-ms", ToMilliseconds(options.ResponseTimeout));
        AddOption(startInfo, "--reconnect-min-ms", ToMilliseconds(options.ReconnectMinDelay));
        AddOption(startInfo, "--reconnect-max-ms", ToMilliseconds(options.ReconnectMaxDelay));
        AddOption(startInfo, "--keep-alive-ms", ToMilliseconds(options.KeepAliveTimeout));
        AddOption(startInfo, "--startup-classes", (byte)options.StartupIntegrityClasses);
        AddOption(startInfo, "--disable-unsolicited-classes", (byte)options.DisableUnsolicitedClassesOnStartup);
        AddOption(startInfo, "--enable-unsolicited-classes", (byte)options.EnableUnsolicitedClassesAfterIntegrity);
        AddOption(startInfo, "--event-scan-classes", (byte)options.EventScanOnEventsAvailable);
        AddOption(startInfo, "--integrity-poll-ms", ToMilliseconds(options.IntegrityPollInterval));
        AddOption(startInfo, "--class1-poll-ms", ToMilliseconds(options.Class1PollInterval));
        AddOption(startInfo, "--class2-poll-ms", ToMilliseconds(options.Class2PollInterval));
        AddOption(startInfo, "--class3-poll-ms", ToMilliseconds(options.Class3PollInterval));
        AddOption(startInfo, "--integrity-on-overflow", options.IntegrityOnEventBufferOverflow ? 1 : 0);
        AddOption(startInfo, "--time-sync", options.TimeSyncMode.ToString());
        AddOption(startInfo, "--max-queued-user-requests", options.MaxQueuedUserRequests);
        return startInfo;
    }

    private static void AddOption(ProcessStartInfo startInfo, string name, object value)
    {
        startInfo.ArgumentList.Add(name);
        startInfo.ArgumentList.Add(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
    }

    private static long ToMilliseconds(TimeSpan value) => checked((long)value.TotalMilliseconds);
    private static long ToMilliseconds(TimeSpan? value) => value is null ? -1 : ToMilliseconds(value.Value);

    private static string ResolveHostExecutable()
    {
        var configured = Environment.GetEnvironmentVariable(HostEnvironmentVariable);
        var executable = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(AppContext.BaseDirectory, "native", "dnp3", OperatingSystem.IsWindows() ? "EliteScada.Dnp3Host.exe" : "EliteScada.Dnp3Host")
            : Path.GetFullPath(configured.Trim());

        if (!File.Exists(executable))
        {
            throw new FileNotFoundException(
                $"EliteSCADA OpenDNP3 native host was not found at '{executable}'. The runtime never falls back to PATH or a system-installed DNP3 implementation.",
                executable);
        }

        return executable;
    }

    private async Task PumpStdoutAsync(Process process, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line is null) break;
                await HandleHostMessageAsync(OpenDnp3HostProtocol.Parse(line), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            MarkFailure($"OpenDNP3 host protocol failure: {ex.Message}");
        }

        if (!cancellationToken.IsCancellationRequested && State is not Dnp3SessionState.Stopping and not Dnp3SessionState.Stopped)
        {
            FailPendingCommands("HOST_EXITED", "OpenDNP3 native host exited; no command will be replayed.");
            await SetStateAsync(Dnp3SessionState.Faulted, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task PumpStderrAsync(Process process, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await process.StandardError.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line is null) break;
                if (!string.IsNullOrWhiteSpace(line))
                {
                    lock (_gate) _lastError = Sanitize(line);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task HandleHostMessageAsync(OpenDnp3HostMessage message, CancellationToken cancellationToken)
    {
        switch (message)
        {
            case OpenDnp3HostReadyMessage:
                _ready?.TrySetResult(true);
                break;

            case OpenDnp3HostStateMessage state:
                if (state.State == Dnp3SessionState.Online)
                {
                    Interlocked.Increment(ref _connections);
                    MarkSuccess();
                }
                else if (state.State == Dnp3SessionState.Reconnecting)
                {
                    Interlocked.Increment(ref _reconnects);
                    Interlocked.Increment(ref _disconnections);
                    FailPendingCommands("RECONNECTING", "Association reconnect invalidated the in-flight command; it will not be replayed.");
                }
                else if (state.State == Dnp3SessionState.StartupIntegrity)
                {
                    Interlocked.Increment(ref _startupIntegrityScans);
                }
                await SetStateAsync(state.State, cancellationToken).ConfigureAwait(false);
                break;

            case OpenDnp3HostMeasurementMessage measurement:
                Interlocked.Increment(ref _requests);
                Interlocked.Increment(ref _readOperations);
                MarkSuccess();
                if (_measurementHandler is { } handler)
                    await handler(measurement.Measurement, cancellationToken).ConfigureAwait(false);
                break;

            case OpenDnp3HostCommandMessage command:
                if (_pendingCommands.TryRemove(command.RequestId, out var completion))
                    completion.TrySetResult(command.Result);
                break;
        }
    }

    private async ValueTask SendLineAsync(string line, CancellationToken cancellationToken)
    {
        await _stdinGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var process = _process;
            if (process is null || process.HasExited)
                throw new InvalidOperationException("OpenDNP3 native host is not running.");
            await process.StandardInput.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _stdinGate.Release();
        }
    }

    private async ValueTask SetStateAsync(Dnp3SessionState state, CancellationToken cancellationToken)
    {
        Func<Dnp3SessionState, CancellationToken, ValueTask>? handler;
        lock (_gate)
        {
            if (_state == state) return;
            _state = state;
            _stateChangedAt = DateTimeOffset.UtcNow;
            handler = _stateHandler;
        }
        if (handler is not null)
            await handler(state, cancellationToken).ConfigureAwait(false);
    }

    private void MarkSuccess()
    {
        Interlocked.Increment(ref _successfulOperations);
        Interlocked.Exchange(ref _consecutiveFailures, 0);
        lock (_gate)
        {
            _lastSuccessfulCommunicationAt = DateTimeOffset.UtcNow;
            _lastError = null;
        }
    }

    private void MarkFailure(string error, bool timedOut = false)
    {
        Interlocked.Increment(ref _failedOperations);
        Interlocked.Increment(ref _consecutiveFailures);
        if (timedOut) Interlocked.Increment(ref _timeouts);
        lock (_gate)
        {
            _lastFailedCommunicationAt = DateTimeOffset.UtcNow;
            _lastError = Sanitize(error);
        }
    }

    private void FailPendingCommands(string status, string message)
    {
        foreach (var pair in _pendingCommands.ToArray())
        {
            if (_pendingCommands.TryRemove(pair.Key, out var completion))
                completion.TrySetResult(Dnp3CommandResult.Failure(status, message));
        }
    }

    private async Task CleanupProcessAsync()
    {
        var process = _process;
        var lifetime = _lifetimeCts;
        _process = null;
        _lifetimeCts = null;
        _ready?.TrySetCanceled();
        _ready = null;
        FailPendingCommands("STOPPED", "DNP3 session stopped; command was not retained for replay.");

        if (process is not null)
        {
            try
            {
                if (!process.HasExited)
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    try
                    {
                        await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        process.Kill(entireProcessTree: true);
                        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                process.Dispose();
            }
        }

        if (lifetime is not null)
        {
            await lifetime.CancelAsync().ConfigureAwait(false);
            lifetime.Dispose();
        }

        var pumps = new[] { _stdoutPump, _stderrPump }.Where(static task => task is not null).Cast<Task>().ToArray();
        _stdoutPump = null;
        _stderrPump = null;
        if (pumps.Length > 0)
        {
            try
            {
                await Task.WhenAll(pumps).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private static string Sanitize(string value)
    {
        var sanitized = value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Trim();
        return sanitized.Length <= 512 ? sanitized : sanitized[..512];
    }
}
