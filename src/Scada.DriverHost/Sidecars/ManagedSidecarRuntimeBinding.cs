using Scada.DriverHost.Engineering;
using Scada.Drivers.Abstractions;
using Scada.Drivers.HostResources;

namespace Scada.DriverHost.Sidecars;

public sealed class ManagedSidecarRuntimeBinding :
    ICommunicationDiagnosticsSource,
    IAsyncDisposable
{
    public const string DiagnosticsDriverType = "host.sidecar";

    private readonly string _displayName;
    private readonly ManagedSidecarDefinition _definition;
    private readonly CommunicationDriverRuntimeServices _services;
    private readonly HostResourceRegistry _resourceRegistry;
    private readonly HostResourceLeaseCoordinator _resourceLeases;
    private readonly ManagedSidecarSupervisor _supervisor;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);

    private HostResourceLeaseCoordinator.HostResourceLease? _resourceLease;
    private int _started;
    private int _disposed;

    public ManagedSidecarRuntimeBinding(
        string displayName,
        ManagedSidecarDefinition definition,
        CommunicationDriverRuntimeServices services,
        HostResourceRegistry resourceRegistry,
        HostResourceLeaseCoordinator resourceLeases,
        ManagedSidecarSupervisor supervisor)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Managed sidecar display name is required.", nameof(displayName));

        _displayName = displayName.Trim();
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _resourceRegistry = resourceRegistry ?? throw new ArgumentNullException(nameof(resourceRegistry));
        _resourceLeases = resourceLeases ?? throw new ArgumentNullException(nameof(resourceLeases));
        _supervisor = supervisor ?? throw new ArgumentNullException(nameof(supervisor));

        _definition.Validate();
        _services.Validate();
    }

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _started) != 0)
                throw new InvalidOperationException($"Managed sidecar '{_definition.InstanceId}' is already started.");

            HostResourceLeaseCoordinator.HostResourceLease? resourceLease = null;
            try
            {
                var boundResourceId = _definition.PersistentState?.BoundResourceId;
                if (boundResourceId is not null)
                {
                    resourceLease = _services.AcquireHostResource(
                        _resourceLeases,
                        boundResourceId,
                        $"sidecar:{_definition.InstanceId}");
                }
                else if (!_services.CanOwnExternalEffects)
                {
                    throw new InvalidOperationException(
                        $"Managed sidecar '{_definition.InstanceId}' cannot start because this Runtime does not own external effects.");
                }

                await _supervisor.StartAsync(cancellationToken).ConfigureAwait(false);
                _resourceLease = resourceLease;
                Volatile.Write(ref _started, 1);
            }
            catch
            {
                resourceLease?.Dispose();
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
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _started) == 0) return;

            try
            {
                await _supervisor.StopAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _resourceLease?.Dispose();
                _resourceLease = null;
                Volatile.Write(ref _started, 0);
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public CommunicationDriverDiagnosticSnapshot GetCommunicationDiagnostics()
    {
        var lifecycle = _supervisor.Snapshot;
        var now = DateTimeOffset.UtcNow;
        var boundResourceId = _definition.PersistentState?.BoundResourceId;
        HostResourceDescriptor? resource = null;

        if (boundResourceId is not null)
        {
            try
            {
                resource = _resourceRegistry.GetRequired(boundResourceId);
            }
            catch (KeyNotFoundException)
            {
            }
        }

        var starts = lifecycle.LastStartedAt.HasValue
            ? lifecycle.RestartCount + 1L
            : 0L;
        var failures = lifecycle.FailuresInCrashWindow;
        var details = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["sidecar.state"] = lifecycle.State.ToString(),
            ["sidecar.artifactId"] = _definition.ExpectedArtifact.ArtifactId,
            ["sidecar.artifactVersion"] = _definition.ExpectedArtifact.ArtifactVersion,
            ["sidecar.runtimeVersion"] = _definition.ExpectedArtifact.RuntimeVersion,
            ["sidecar.schemaVersion"] = _definition.ExpectedArtifact.SchemaVersion.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            ["sidecar.controlPlane"] = "local-only",
            ["sidecar.restartCount"] = lifecycle.RestartCount.ToString(
                System.Globalization.CultureInfo.InvariantCulture)
        };

        if (resource is not null)
        {
            details["resource.id"] = resource.ResourceId.Value;
            details["resource.kind"] = resource.ResourceKind.Value;
            details["resource.availability"] = resource.Availability.ToString();
            details["resource.locatorKind"] = resource.Locator.Kind.ToString();
            details["resource.physicalIdentity"] = resource.PhysicalIdentity.StableKey;
        }

        return new CommunicationDriverDiagnosticSnapshot(
            DataSourceKey: _definition.InstanceId,
            DataSourceName: _displayName,
            DriverType: DiagnosticsDriverType,
            RuntimeInstanceId: _definition.InstanceId,
            Endpoint: boundResourceId is null ? null : $"resource:{boundResourceId.Value}",
            State: MapState(lifecycle.State),
            StateChangedAt: ResolveStateChangedAt(lifecycle, now),
            CapturedAt: now,
            LastSuccessfulCommunicationAt: lifecycle.LastReadyAt,
            LastFailedCommunicationAt: lifecycle.LastExitAt,
            LastError: lifecycle.LastSanitizedError,
            DataAge: lifecycle.LastReadyAt is { } readyAt ? now - readyAt : null,
            ConfiguredScanInterval: null,
            LastOperationDuration: null,
            AverageOperationDuration: null,
            LastScanDuration: null,
            RecentFailureRate: starts == 0 ? 0d : Math.Min(1d, failures / (double)starts),
            AssociatedTagCount: 0,
            TagQuality: new CommunicationTagQualitySummary(
                Good: 0,
                BadCommunication: 0,
                Uncertain: 0,
                Bad: 0,
                BadConfiguration: 0,
                BadDevice: 0,
                Stale: 0,
                Disabled: 0,
                NoCurrentSample: 0),
            Counters: new CommunicationDriverCounters(
                Cycles: 0,
                Requests: 0,
                SuccessfulOperations: lifecycle.LastReadyAt.HasValue ? starts : 0,
                FailedOperations: failures,
                ConsecutiveFailures: lifecycle.State == ManagedSidecarLifecycleState.Ready ? 0 : failures,
                Timeouts: 0,
                Connections: starts,
                Disconnections: lifecycle.LastExitAt.HasValue ? Math.Max(1, failures) : 0,
                Reconnects: lifecycle.RestartCount,
                ReadOperations: 0,
                WriteOperations: 0,
                UpdatesPublished: 0),
            ProtocolDetails: details);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try
        {
            await StopAsync().ConfigureAwait(false);
        }
        finally
        {
            await _supervisor.DisposeAsync().ConfigureAwait(false);
            _lifecycleGate.Dispose();
        }
    }

    private static CommunicationDriverOperationalState MapState(ManagedSidecarLifecycleState state) =>
        state switch
        {
            ManagedSidecarLifecycleState.Stopped => CommunicationDriverOperationalState.Stopped,
            ManagedSidecarLifecycleState.Starting => CommunicationDriverOperationalState.Starting,
            ManagedSidecarLifecycleState.Ready => CommunicationDriverOperationalState.Healthy,
            ManagedSidecarLifecycleState.RestartBackoff => CommunicationDriverOperationalState.Reconnecting,
            ManagedSidecarLifecycleState.Stopping => CommunicationDriverOperationalState.Stopping,
            ManagedSidecarLifecycleState.Faulted => CommunicationDriverOperationalState.Faulted,
            ManagedSidecarLifecycleState.CrashLoop => CommunicationDriverOperationalState.Faulted,
            _ => CommunicationDriverOperationalState.Faulted
        };

    private static DateTimeOffset ResolveStateChangedAt(
        ManagedSidecarLifecycleSnapshot lifecycle,
        DateTimeOffset fallback) =>
        lifecycle.State switch
        {
            ManagedSidecarLifecycleState.Ready => lifecycle.LastReadyAt ?? fallback,
            ManagedSidecarLifecycleState.RestartBackoff or
            ManagedSidecarLifecycleState.Faulted or
            ManagedSidecarLifecycleState.CrashLoop => lifecycle.LastExitAt ?? lifecycle.LastStartedAt ?? fallback,
            ManagedSidecarLifecycleState.Starting => lifecycle.LastStartedAt ?? fallback,
            _ => lifecycle.LastExitAt ?? lifecycle.LastReadyAt ?? lifecycle.LastStartedAt ?? fallback
        };
}
