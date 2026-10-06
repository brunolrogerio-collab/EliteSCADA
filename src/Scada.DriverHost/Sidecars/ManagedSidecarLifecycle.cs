using Scada.Drivers.HostResources;

namespace Scada.DriverHost.Sidecars;

public enum ManagedSidecarLifecycleState
{
    Stopped,
    Starting,
    Ready,
    RestartBackoff,
    Stopping,
    Faulted,
    CrashLoop
}

public enum ManagedSidecarControlPlaneExposure
{
    LocalOnly
}

public sealed record ManagedSidecarArtifactIdentity(
    string ArtifactId,
    string ArtifactVersion,
    string RuntimeVersion,
    int SchemaVersion)
{
    public void Validate()
    {
        ValidateText(ArtifactId, nameof(ArtifactId), 128);
        ValidateText(ArtifactVersion, nameof(ArtifactVersion), 64);
        ValidateText(RuntimeVersion, nameof(RuntimeVersion), 64);
        if (SchemaVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(SchemaVersion), "Sidecar schema version must be positive.");
    }

    private static void ValidateText(string value, string parameterName, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !string.Equals(value, value.Trim(), StringComparison.Ordinal) ||
            value.Length > maximumLength ||
            value.Any(char.IsControl))
        {
            throw new ArgumentException($"Sidecar {parameterName} is invalid.", parameterName);
        }
    }
}

public sealed record ManagedSidecarCompatibilityMetadata(
    string ArtifactId,
    IReadOnlyCollection<string> AcceptedArtifactVersions,
    string RequiredRuntimeVersion,
    int MinimumSchemaVersion,
    int MaximumSchemaVersion,
    string? RollbackArtifactVersion = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ArtifactId))
            throw new ArgumentException("Sidecar compatibility artifact ID is required.", nameof(ArtifactId));
        if (AcceptedArtifactVersions is null || AcceptedArtifactVersions.Count == 0)
            throw new ArgumentException("At least one accepted sidecar artifact version is required.", nameof(AcceptedArtifactVersions));
        if (AcceptedArtifactVersions.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Accepted sidecar artifact versions must be non-empty.", nameof(AcceptedArtifactVersions));
        if (string.IsNullOrWhiteSpace(RequiredRuntimeVersion))
            throw new ArgumentException("Required sidecar runtime version is required.", nameof(RequiredRuntimeVersion));
        if (MinimumSchemaVersion <= 0 || MaximumSchemaVersion < MinimumSchemaVersion)
            throw new ArgumentOutOfRangeException(nameof(MaximumSchemaVersion), "Sidecar schema compatibility range is invalid.");
        if (RollbackArtifactVersion is not null &&
            !AcceptedArtifactVersions.Contains(RollbackArtifactVersion, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "Rollback artifact version must be one of the accepted artifact versions.",
                nameof(RollbackArtifactVersion));
        }
    }

    public bool IsCompatible(ManagedSidecarArtifactIdentity observed, out string? errorCode)
    {
        ArgumentNullException.ThrowIfNull(observed);
        Validate();
        observed.Validate();

        if (!string.Equals(ArtifactId, observed.ArtifactId, StringComparison.Ordinal))
        {
            errorCode = "SIDECAR_ARTIFACT_ID_MISMATCH";
            return false;
        }

        if (!AcceptedArtifactVersions.Contains(observed.ArtifactVersion, StringComparer.Ordinal))
        {
            errorCode = "SIDECAR_ARTIFACT_VERSION_MISMATCH";
            return false;
        }

        if (!string.Equals(RequiredRuntimeVersion, observed.RuntimeVersion, StringComparison.Ordinal))
        {
            errorCode = "SIDECAR_RUNTIME_VERSION_MISMATCH";
            return false;
        }

        if (observed.SchemaVersion < MinimumSchemaVersion ||
            observed.SchemaVersion > MaximumSchemaVersion)
        {
            errorCode = "SIDECAR_SCHEMA_VERSION_MISMATCH";
            return false;
        }

        errorCode = null;
        return true;
    }
}

public static class ManagedSidecarPackageTargets
{
    public const string WindowsX64 = "windows-x64";
    public const string LinuxX64 = "linux-x64";
    public const string LinuxArm64 = "linux-arm64";
    public const string ContainerX64 = "container-x64";
    public const string ContainerArm64 = "container-arm64";
}

public sealed record ManagedSidecarPackagingMetadata(
    IReadOnlyCollection<string> SupportedTargets)
{
    public void Validate()
    {
        if (SupportedTargets is null || SupportedTargets.Count == 0)
            throw new ArgumentException("Sidecar packaging must declare at least one supported target.", nameof(SupportedTargets));
        if (SupportedTargets.Any(value =>
                string.IsNullOrWhiteSpace(value) ||
                value.Length > 64 ||
                value.Any(char.IsControl)))
        {
            throw new ArgumentException("Sidecar packaging target is invalid.", nameof(SupportedTargets));
        }
        if (SupportedTargets.Count != SupportedTargets.Distinct(StringComparer.Ordinal).Count())
            throw new ArgumentException("Sidecar packaging targets must be unique.", nameof(SupportedTargets));
    }
}

public sealed record ManagedSidecarProtectedSettingReference(
    string SettingKey,
    string ResourceKind,
    string ResourceId,
    string Purpose,
    string Reference)
{
    public void Validate()
    {
        Validate(SettingKey, nameof(SettingKey), 128);
        Validate(ResourceKind, nameof(ResourceKind), 96);
        Validate(ResourceId, nameof(ResourceId), 256);
        Validate(Purpose, nameof(Purpose), 128);
        Validate(Reference, nameof(Reference), 512);
    }

    private static void Validate(string value, string parameterName, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !string.Equals(value, value.Trim(), StringComparison.Ordinal) ||
            value.Length > maximumLength ||
            value.Any(char.IsControl))
        {
            throw new ArgumentException($"Sidecar protected setting {parameterName} is invalid.", parameterName);
        }
    }
}

public sealed record ManagedSidecarConfiguration(
    IReadOnlyDictionary<string, string>? PublicSettings = null,
    IReadOnlyCollection<ManagedSidecarProtectedSettingReference>? ProtectedSettings = null)
{
    public void Validate()
    {
        if (PublicSettings is { Count: > 128 })
            throw new ArgumentOutOfRangeException(nameof(PublicSettings), "Sidecar public configuration is limited to 128 settings.");
        if (ProtectedSettings is { Count: > 64 })
            throw new ArgumentOutOfRangeException(nameof(ProtectedSettings), "Sidecar protected configuration is limited to 64 references.");

        var publicKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (PublicSettings is not null)
        {
            foreach (var pair in PublicSettings)
            {
                ValidateSetting(pair.Key, nameof(PublicSettings), 128);
                ValidateSetting(pair.Value, nameof(PublicSettings), 2048);
                if (!publicKeys.Add(pair.Key))
                    throw new ArgumentException("Sidecar public configuration contains duplicate keys.", nameof(PublicSettings));
            }
        }

        var protectedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (ProtectedSettings is null) return;
        foreach (var setting in ProtectedSettings)
        {
            ArgumentNullException.ThrowIfNull(setting);
            setting.Validate();
            if (!protectedKeys.Add(setting.SettingKey))
                throw new ArgumentException("Sidecar protected configuration contains duplicate keys.", nameof(ProtectedSettings));
            if (publicKeys.Contains(setting.SettingKey))
                throw new ArgumentException(
                    $"Sidecar setting '{setting.SettingKey}' cannot be both public and protected.",
                    nameof(ProtectedSettings));
        }
    }

    public IReadOnlyCollection<string> ProtectedReferences =>
        ProtectedSettings?.Select(x => x.Reference).ToArray() ?? Array.Empty<string>();

    private static void ValidateSetting(string value, string parameterName, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > maximumLength ||
            value.Any(character => character is '\r' or '\n' or '\0'))
        {
            throw new ArgumentException("Sidecar public setting is invalid.", parameterName);
        }
    }
}

public sealed record ManagedSidecarPersistentStateOwnership(
    string OwnerKey,
    int StateSchemaVersion,
    string BackupKey,
    HostResourceId? BoundResourceId = null,
    bool RequiresBackupBeforeUpgrade = true)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OwnerKey) || OwnerKey.Length > 128 || OwnerKey.Any(char.IsControl))
            throw new ArgumentException("Sidecar persistent-state owner key is invalid.", nameof(OwnerKey));
        if (StateSchemaVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(StateSchemaVersion));
        if (string.IsNullOrWhiteSpace(BackupKey) || BackupKey.Length > 128 || BackupKey.Any(char.IsControl))
            throw new ArgumentException("Sidecar backup key is invalid.", nameof(BackupKey));
    }
}

public sealed record ManagedSidecarBackupResult(
    string BackupReference,
    DateTimeOffset CapturedAt);

public interface IManagedSidecarBackupHook
{
    ValueTask<ManagedSidecarBackupResult> CreateBackupAsync(
        ManagedSidecarPersistentStateOwnership ownership,
        CancellationToken cancellationToken = default);
}

public sealed record ManagedSidecarDefinition(
    string InstanceId,
    ManagedSidecarArtifactIdentity ExpectedArtifact,
    ManagedSidecarCompatibilityMetadata Compatibility,
    ManagedSidecarPackagingMetadata Packaging,
    ManagedSidecarConfiguration Configuration,
    ManagedSidecarPersistentStateOwnership? PersistentState = null,
    ManagedSidecarControlPlaneExposure ControlPlaneExposure = ManagedSidecarControlPlaneExposure.LocalOnly)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(InstanceId) ||
            !string.Equals(InstanceId, InstanceId.Trim(), StringComparison.Ordinal) ||
            InstanceId.Length > 128 ||
            InstanceId.Any(char.IsControl))
        {
            throw new ArgumentException("Sidecar instance ID is invalid.", nameof(InstanceId));
        }

        ArgumentNullException.ThrowIfNull(ExpectedArtifact);
        ArgumentNullException.ThrowIfNull(Compatibility);
        ArgumentNullException.ThrowIfNull(Packaging);
        ArgumentNullException.ThrowIfNull(Configuration);
        ExpectedArtifact.Validate();
        Compatibility.Validate();
        Packaging.Validate();
        Configuration.Validate();
        PersistentState?.Validate();

        if (ControlPlaneExposure != ManagedSidecarControlPlaneExposure.LocalOnly)
            throw new NotSupportedException("Managed sidecar control plane must remain local-only.");

        if (!Compatibility.IsCompatible(ExpectedArtifact, out var errorCode))
            throw new ArgumentException($"Expected sidecar artifact is incompatible ({errorCode}).", nameof(ExpectedArtifact));
    }
}

public sealed record ManagedSidecarReadyReport(
    ManagedSidecarArtifactIdentity Artifact,
    string? SanitizedMessage = null);

public interface IManagedSidecarProcess : IAsyncDisposable
{
    string ProcessIdentity { get; }

    ValueTask<ManagedSidecarReadyReport> WaitForReadyAsync(
        CancellationToken cancellationToken = default);

    ValueTask<int> WaitForExitAsync(
        CancellationToken cancellationToken = default);

    ValueTask StopAsync(
        CancellationToken cancellationToken = default);
}

public interface IManagedSidecarProcessFactory
{
    ValueTask<IManagedSidecarProcess> StartAsync(
        ManagedSidecarDefinition definition,
        CancellationToken cancellationToken = default);
}

public interface IManagedSidecarDelay
{
    ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default);
}

public sealed class SystemManagedSidecarDelay : IManagedSidecarDelay
{
    private readonly TimeProvider _timeProvider;

    public SystemManagedSidecarDelay(TimeProvider? timeProvider = null) =>
        _timeProvider = timeProvider ?? TimeProvider.System;

    public async ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default) =>
        await Task.Delay(delay, _timeProvider, cancellationToken).ConfigureAwait(false);
}

public sealed record ManagedSidecarRestartPolicy(
    TimeSpan ReadinessTimeout,
    TimeSpan GracefulStopTimeout,
    TimeSpan InitialRestartDelay,
    TimeSpan MaximumRestartDelay,
    TimeSpan CrashLoopWindow,
    int CrashLoopThreshold)
{
    public static ManagedSidecarRestartPolicy Default { get; } = new(
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
        5);

    public void Validate()
    {
        if (ReadinessTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(ReadinessTimeout));
        if (GracefulStopTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(GracefulStopTimeout));
        if (InitialRestartDelay <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(InitialRestartDelay));
        if (MaximumRestartDelay < InitialRestartDelay) throw new ArgumentOutOfRangeException(nameof(MaximumRestartDelay));
        if (CrashLoopWindow <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(CrashLoopWindow));
        if (CrashLoopThreshold is < 2 or > 100) throw new ArgumentOutOfRangeException(nameof(CrashLoopThreshold));
    }
}

public sealed record ManagedSidecarLifecycleSnapshot(
    ManagedSidecarLifecycleState State,
    int RestartCount,
    int FailuresInCrashWindow,
    DateTimeOffset? LastStartedAt,
    DateTimeOffset? LastReadyAt,
    DateTimeOffset? LastExitAt,
    int? LastExitCode,
    TimeSpan? PendingRestartDelay,
    string? LastSanitizedError);

public static class ManagedSidecarLogSanitizer
{
    public static string? Sanitize(
        string? text,
        IEnumerable<string>? sensitiveValues = null,
        int maximumLength = 512)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var sanitized = text.Replace('\r', ' ').Replace('\n', ' ').Trim();

        if (sensitiveValues is not null)
        {
            foreach (var value in sensitiveValues.Where(value => !string.IsNullOrEmpty(value)))
                sanitized = sanitized.Replace(value, "***", StringComparison.Ordinal);
        }

        return sanitized.Length <= maximumLength
            ? sanitized
            : sanitized[..maximumLength];
    }
}

public sealed class ManagedSidecarSupervisor : IAsyncDisposable
{
    private readonly ManagedSidecarDefinition _definition;
    private readonly IManagedSidecarProcessFactory _factory;
    private readonly ManagedSidecarRestartPolicy _policy;
    private readonly IManagedSidecarDelay _delay;
    private readonly TimeProvider _timeProvider;
    private readonly object _sync = new();
    private readonly Queue<DateTimeOffset> _failures = new();

    private CancellationTokenSource? _lifetime;
    private IManagedSidecarProcess? _process;
    private Task? _monitorTask;
    private ManagedSidecarLifecycleState _state = ManagedSidecarLifecycleState.Stopped;
    private int _restartCount;
    private DateTimeOffset? _lastStartedAt;
    private DateTimeOffset? _lastReadyAt;
    private DateTimeOffset? _lastExitAt;
    private int? _lastExitCode;
    private TimeSpan? _pendingRestartDelay;
    private string? _lastSanitizedError;

    public ManagedSidecarSupervisor(
        ManagedSidecarDefinition definition,
        IManagedSidecarProcessFactory factory,
        ManagedSidecarRestartPolicy? policy = null,
        IManagedSidecarDelay? delay = null,
        TimeProvider? timeProvider = null)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _policy = policy ?? ManagedSidecarRestartPolicy.Default;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _delay = delay ?? new SystemManagedSidecarDelay(_timeProvider);
        _definition.Validate();
        _policy.Validate();
    }

    public ManagedSidecarLifecycleSnapshot Snapshot
    {
        get
        {
            lock (_sync)
            {
                return new ManagedSidecarLifecycleSnapshot(
                    _state,
                    _restartCount,
                    _failures.Count,
                    _lastStartedAt,
                    _lastReadyAt,
                    _lastExitAt,
                    _lastExitCode,
                    _pendingRestartDelay,
                    _lastSanitizedError);
            }
        }
    }

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource lifetime;
        lock (_sync)
        {
            if (_state != ManagedSidecarLifecycleState.Stopped)
                throw new InvalidOperationException($"Managed sidecar '{_definition.InstanceId}' is already {_state}.");
            _failures.Clear();
            _restartCount = 0;
            _lastExitAt = null;
            _lastExitCode = null;
            _pendingRestartDelay = null;
            _lastSanitizedError = null;
            _state = ManagedSidecarLifecycleState.Starting;
            _lifetime = lifetime = new CancellationTokenSource();
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            lifetime.Token);

        IManagedSidecarProcess process;
        try
        {
            process = await StartOneAsync(linked.Token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            lock (_sync)
            {
                _state = ManagedSidecarLifecycleState.Faulted;
                _lastSanitizedError = Sanitize(exception.Message);
            }
            lifetime.Cancel();
            throw;
        }

        lock (_sync)
        {
            _process = process;
            _state = ManagedSidecarLifecycleState.Ready;
            _monitorTask = MonitorAsync(process, lifetime.Token);
        }
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        IManagedSidecarProcess? process;
        Task? monitor;
        CancellationTokenSource? lifetime;

        lock (_sync)
        {
            if (_state == ManagedSidecarLifecycleState.Stopped) return;
            _state = ManagedSidecarLifecycleState.Stopping;
            process = _process;
            monitor = _monitorTask;
            lifetime = _lifetime;
            lifetime?.Cancel();
        }

        Exception? stopError = null;
        if (process is not null)
        {
            using var stopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            stopCts.CancelAfter(_policy.GracefulStopTimeout);
            try
            {
                await process.StopAsync(stopCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stopCts.IsCancellationRequested)
            {
                stopError = new TimeoutException(
                    $"Managed sidecar '{_definition.InstanceId}' did not stop within the graceful timeout.");
            }
            catch (Exception exception)
            {
                stopError = exception;
            }

            await process.DisposeAsync().ConfigureAwait(false);
        }

        if (monitor is not null)
        {
            try
            {
                await monitor.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        lock (_sync)
        {
            _process = null;
            _monitorTask = null;
            _lifetime = null;
            _pendingRestartDelay = null;
            _state = ManagedSidecarLifecycleState.Stopped;
            if (stopError is not null)
                _lastSanitizedError = Sanitize(stopError.Message);
        }

        lifetime?.Dispose();

        if (stopError is not null)
            throw stopError;
    }

    public async ValueTask DisposeAsync()
    {
        if (Snapshot.State != ManagedSidecarLifecycleState.Stopped)
        {
            try
            {
                await StopAsync().ConfigureAwait(false);
            }
            catch
            {
            }
        }
    }

    private async ValueTask<IManagedSidecarProcess> StartOneAsync(CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            _lastStartedAt = _timeProvider.GetUtcNow();
            _state = ManagedSidecarLifecycleState.Starting;
        }

        var process = await _factory.StartAsync(_definition, cancellationToken).ConfigureAwait(false);
        try
        {
            using var readinessCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            readinessCts.CancelAfter(_policy.ReadinessTimeout);
            ManagedSidecarReadyReport ready;
            try
            {
                ready = await process.WaitForReadyAsync(readinessCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (
                readinessCts.IsCancellationRequested &&
                !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"Managed sidecar '{_definition.InstanceId}' readiness timed out.");
            }

            if (!_definition.Compatibility.IsCompatible(ready.Artifact, out var compatibilityError))
            {
                throw new InvalidOperationException(
                    $"Managed sidecar '{_definition.InstanceId}' readiness identity is incompatible ({compatibilityError}).");
            }

            lock (_sync)
            {
                _lastReadyAt = _timeProvider.GetUtcNow();
                _lastSanitizedError = Sanitize(ready.SanitizedMessage);
            }

            return process;
        }
        catch
        {
            try
            {
                using var stopCts = new CancellationTokenSource(_policy.GracefulStopTimeout);
                await process.StopAsync(stopCts.Token).ConfigureAwait(false);
            }
            catch
            {
            }
            await process.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private async Task MonitorAsync(
        IManagedSidecarProcess initialProcess,
        CancellationToken cancellationToken)
    {
        IManagedSidecarProcess? current = initialProcess;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (current is not null)
            {
                int exitCode;
                try
                {
                    exitCode = await current.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                await current.DisposeAsync().ConfigureAwait(false);
                lock (_sync)
                {
                    if (ReferenceEquals(_process, current))
                        _process = null;
                    _lastExitAt = _timeProvider.GetUtcNow();
                    _lastExitCode = exitCode;
                }
                current = null;
                if (RegisterFailure($"Managed sidecar exited with code {exitCode}."))
                    return;
            }

            var delay = ComputeRestartDelay();
            lock (_sync)
            {
                _state = ManagedSidecarLifecycleState.RestartBackoff;
                _pendingRestartDelay = delay;
            }

            try
            {
                await _delay.DelayAsync(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            lock (_sync)
            {
                _pendingRestartDelay = null;
                _restartCount++;
            }

            try
            {
                current = await StartOneAsync(cancellationToken).ConfigureAwait(false);
                lock (_sync)
                {
                    _process = current;
                    _state = ManagedSidecarLifecycleState.Ready;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                if (RegisterFailure(exception.Message))
                    return;
            }
        }
    }

    private bool RegisterFailure(string message)
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            _failures.Enqueue(now);
            while (_failures.Count > 0 && now - _failures.Peek() > _policy.CrashLoopWindow)
                _failures.Dequeue();

            _lastSanitizedError = Sanitize(message);
            if (_failures.Count < _policy.CrashLoopThreshold)
                return false;

            _state = ManagedSidecarLifecycleState.CrashLoop;
            _pendingRestartDelay = null;
            _lastSanitizedError = "SIDECAR_CRASH_LOOP";
            return true;
        }
    }

    private TimeSpan ComputeRestartDelay()
    {
        lock (_sync)
        {
            var exponent = Math.Max(0, _failures.Count - 1);
            var milliseconds = Math.Min(
                _policy.MaximumRestartDelay.TotalMilliseconds,
                _policy.InitialRestartDelay.TotalMilliseconds * Math.Pow(2d, exponent));
            return TimeSpan.FromMilliseconds(milliseconds);
        }
    }

    private string? Sanitize(string? message) =>
        ManagedSidecarLogSanitizer.Sanitize(
            message,
            _definition.Configuration.ProtectedReferences);
}
