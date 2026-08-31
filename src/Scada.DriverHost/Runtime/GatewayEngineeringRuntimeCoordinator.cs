using Scada.Core.Abstractions;
using Scada.Core.Alarms;
using Scada.Core.Commands;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.Engineering.Contracts;

namespace Scada.DriverHost.Runtime;

public interface IGatewayRuntimeDiagnosticsProvider
{
    IReadOnlyCollection<GatewayRouteRuntimeDiagnostic> GatewayDiagnostics();
}

/// <summary>
/// Decorates the proven Engineering runtime coordinator with protocol-independent
/// TAG Gateway execution. Gateway routes are validated before staging, the old
/// Gateway is stopped inside the activation commit boundary, and the candidate
/// Gateway starts only after the underlying Active Revision swap succeeds.
/// </summary>
public sealed class GatewayEngineeringRuntimeCoordinator : IEngineeringRuntimeCoordinator, IGatewayRuntimeDiagnosticsProvider
{
    private readonly EngineeringRuntimeCoordinator _inner;
    private readonly IScadaEventBus _eventBus;
    private readonly SemaphoreSlim _activationGate = new(1, 1);
    private GatewayRuntimeEngine? _gateway;
    private bool _disposed;

    public GatewayEngineeringRuntimeCoordinator(
        EngineeringRuntimeCoordinator inner,
        IScadaEventBus eventBus)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    public RuntimeDescriptor Describe() => _inner.Describe();
    public IReadOnlyCollection<TagDefinition> Tags() => _inner.Tags();
    public IReadOnlyCollection<TagValue> CurrentValues() => _inner.CurrentValues();
    public IReadOnlyCollection<AlarmDefinition> AlarmDefinitions() => _inner.AlarmDefinitions();
    public IReadOnlyCollection<AlarmInstance> Alarms(bool activeOnly = false) => _inner.Alarms(activeOnly);
    public IReadOnlyCollection<CommandDefinition> Commands() => _inner.Commands();
    public IReadOnlyCollection<ClientMemoryRuntimeSource> ClientMemorySources() => _inner.ClientMemorySources();
    public bool TryGetTag(Guid tagId, out TagDefinition? tag) => _inner.TryGetTag(tagId, out tag);
    public bool TryGetTagByPath(string path, out TagDefinition? tag) => _inner.TryGetTagByPath(path, out tag);
    public bool TryGetCurrent(Guid tagId, out TagValue? value) => _inner.TryGetCurrent(tagId, out value);
    public bool TryGetCommand(Guid commandId, out CommandDefinition? command) => _inner.TryGetCommand(commandId, out command);
    public bool IsServerMemoryTag(Guid tagId) => _inner.IsServerMemoryTag(tagId);

    public IReadOnlyCollection<GatewayRouteRuntimeDiagnostic> GatewayDiagnostics() =>
        Volatile.Read(ref _gateway)?.Diagnostics() ?? Array.Empty<GatewayRouteRuntimeDiagnostic>();

    public ValueTask<bool> AcknowledgeAlarmAsync(Guid alarmId, string user, CancellationToken cancellationToken = default) =>
        _inner.AcknowledgeAlarmAsync(alarmId, user, cancellationToken);

    public ValueTask<bool> ShelveAlarmAsync(Guid alarmId, string user, CancellationToken cancellationToken = default) =>
        _inner.ShelveAlarmAsync(alarmId, user, cancellationToken);

    public ValueTask<bool> UnshelveAlarmAsync(Guid alarmId, string user, CancellationToken cancellationToken = default) =>
        _inner.UnshelveAlarmAsync(alarmId, user, cancellationToken);

    public ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default) =>
        _inner.WriteAsync(tagId, value, cancellationToken);

    public ValueTask ResetServerMemoryRetainedValueAsync(Guid tagId, CancellationToken cancellationToken = default) =>
        _inner.ResetServerMemoryRetainedValueAsync(tagId, cancellationToken);

    public ValueTask ExecuteCommandAsync(Guid commandId, CancellationToken cancellationToken = default) =>
        _inner.ExecuteCommandAsync(commandId, cancellationToken);

    public Task<RuntimeActivationResult> ActivateAsync(
        string projectKey,
        long revision,
        EngineeringPackage package,
        CancellationToken cancellationToken = default) =>
        ActivateCoreAsync(projectKey, revision, package, null, cancellationToken);

    public Task<RuntimeActivationResult> ActivateAsync(
        string projectKey,
        long revision,
        EngineeringPackage package,
        Func<RuntimeActivationCommitContext, CancellationToken, Task> commitAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(commitAsync);
        return ActivateCoreAsync(projectKey, revision, package, commitAsync, cancellationToken);
    }

    private async Task<RuntimeActivationResult> ActivateCoreAsync(
        string projectKey,
        long revision,
        EngineeringPackage package,
        Func<RuntimeActivationCommitContext, CancellationToken, Task>? commitAsync,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(projectKey))
            throw new ArgumentException("Project key is required.", nameof(projectKey));
        if (revision < 1)
            throw new ArgumentOutOfRangeException(nameof(revision));
        ArgumentNullException.ThrowIfNull(package);

        await _activationGate.WaitAsync(cancellationToken);
        try
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GatewayEngineeringRuntimeCoordinator));

            var gatewayIssues = new List<RuntimeActivationIssue>();
            var candidateGateway = BuildCandidateGateway(package, gatewayIssues);
            if (gatewayIssues.Any(issue => issue.IsError))
            {
                await candidateGateway.DisposeAsync();
                return new RuntimeActivationResult(
                    projectKey.Trim(),
                    revision,
                    false,
                    Array.Empty<EngineeringDriverIssue>(),
                    gatewayIssues);
            }

            var previousGateway = Volatile.Read(ref _gateway);
            var previousGatewayStopped = false;
            try
            {
                var result = await _inner.ActivateAsync(
                    projectKey,
                    revision,
                    package,
                    async (context, ct) =>
                    {
                        // Stop automated writes before the external commit callback can
                        // persist/promote the candidate Active Revision. If that callback
                        // later fails, the wrapper restarts the previous Gateway while the
                        // proven inner coordinator keeps the previous runtime active.
                        if (previousGateway is not null)
                        {
                            await previousGateway.StopAsync();
                            previousGatewayStopped = true;
                        }

                        if (commitAsync is not null)
                            await commitAsync(context, ct);
                    },
                    cancellationToken);

                if (!result.Activated)
                {
                    await candidateGateway.DisposeAsync();
                    if (previousGatewayStopped && previousGateway is not null)
                        previousGateway.Start();
                    return result;
                }

                Volatile.Write(ref _gateway, candidateGateway);
                candidateGateway.Start();
                if (previousGateway is not null)
                    await previousGateway.DisposeAsync();
                return result;
            }
            catch
            {
                await candidateGateway.DisposeAsync();
                if (previousGatewayStopped && previousGateway is not null)
                    previousGateway.Start();
                throw;
            }
        }
        finally
        {
            _activationGate.Release();
        }
    }

    private GatewayRuntimeEngine BuildCandidateGateway(
        EngineeringPackage package,
        List<RuntimeActivationIssue> issues)
    {
        var registry = new InMemoryTagRegistry();
        var writableTagIds = new HashSet<Guid>();
        var dataSources = (package.DataSources ?? Array.Empty<DataSourceEngineeringDto>())
            .Where(source => source.Enabled)
            .ToDictionary(source => source.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var dto in package.Tags)
        {
            if (!dto.Id.HasValue || dto.Id.Value == Guid.Empty)
                continue;
            if (string.IsNullOrWhiteSpace(dto.Source) || !dataSources.TryGetValue(dto.Source, out var dataSource))
                continue;

            // Client Memory has no server-authoritative scalar value and the built-in
            // simulation source is not part of the active Engineering runtime. All other
            // enabled server-owned sources remain protocol-neutral here: unsupported
            // communication drivers are rejected by the normal runtime compiler, while
            // future supported drivers become Gateway-eligible without editing this class.
            var sharedRuntimeSource =
                !InternalMemoryRuntimePlanner.IsClientMemoryDriver(dataSource.Driver) &&
                !dataSource.Driver.Equals(EngineeringDriverCompiler.SimulationDriverKey, StringComparison.OrdinalIgnoreCase);
            if (!sharedRuntimeSource)
                continue;

            var metadata = dto.Metadata is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(dto.Metadata, StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(dto.Address)) metadata["address"] = dto.Address;

            var access = dto.AccessPolicy is null
                ? null
                : new TagAccessPolicy(
                    dto.AccessPolicy.ReadRoles?.ToArray(),
                    dto.AccessPolicy.WriteRoles?.ToArray(),
                    dto.AccessPolicy.ConfigureRoles?.ToArray());

            var tag = new TagDefinition(
                dto.Id.Value,
                dto.Name,
                dto.Path,
                dto.DataType,
                dto.Source,
                dto.EngineeringUnit,
                dto.Description,
                dto.ReadOnly,
                metadata,
                access);
            registry.Register(tag);
            if (!tag.ReadOnly)
                writableTagIds.Add(tag.Id);
        }

        foreach (var route in package.Gateways ?? Array.Empty<GatewayRouteEngineeringDto>())
        {
            if (!route.Enabled) continue;
            RequireStableEndpointId(route, package.Tags, source: true, issues);
            RequireStableEndpointId(route, package.Tags, source: false, issues);
        }

        return GatewayRuntimeEngine.Create(
            package.Gateways ?? Array.Empty<GatewayRouteEngineeringDto>(),
            registry,
            new RuntimeCacheView(_inner),
            _eventBus,
            writableTagIds.Contains,
            (tagId, value, ct) => _inner.WriteAsync(tagId, value, ct),
            issues);
    }

    private static void RequireStableEndpointId(
        GatewayRouteEngineeringDto route,
        IReadOnlyCollection<TagEngineeringDto> tags,
        bool source,
        List<RuntimeActivationIssue> issues)
    {
        var id = source ? route.SourceTagId : route.DestinationTagId;
        var path = source ? route.SourceTagPath : route.DestinationTagPath;
        var label = source ? "source" : "destination";
        TagEngineeringDto? tag = null;

        if (id.HasValue)
            tag = tags.FirstOrDefault(candidate => candidate.Id == id.Value);
        if (tag is null && !string.IsNullOrWhiteSpace(path))
            tag = tags.FirstOrDefault(candidate => candidate.Path.Equals(path, StringComparison.OrdinalIgnoreCase));

        if (tag is not null && (!tag.Id.HasValue || tag.Id.Value == Guid.Empty))
        {
            issues.Add(new RuntimeActivationIssue(
                "RUNTIME_GATEWAY_ENDPOINT_STABLE_TAG_ID_REQUIRED",
                $"Gateway route '{route.Key}' {label} TAG '{tag.Path}' requires a stable non-empty TAG ID before runtime activation.",
                route.Key));
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _activationGate.WaitAsync();
        try
        {
            if (_disposed)
                return;
            _disposed = true;

            var gateway = Volatile.Read(ref _gateway);
            if (gateway is not null)
                await gateway.DisposeAsync();
            Volatile.Write(ref _gateway, null);
            await _inner.DisposeAsync();
        }
        finally
        {
            _activationGate.Release();
        }
    }

    private sealed class RuntimeCacheView(IEngineeringRuntimeCoordinator runtime) : ICurrentTagCache
    {
        public bool TryGet(Guid tagId, out TagValue? value) => runtime.TryGetCurrent(tagId, out value);
        public IReadOnlyCollection<TagValue> Snapshot() => runtime.CurrentValues();

        public ValueTask<TagValue?> UpdateAsync(
            TagDefinition tag,
            TagValue value,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Gateway runtime cache view is read-only.");
    }
}
