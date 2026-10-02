using System.Text.Json;
using Scada.Security.Authorization;

namespace Scada.Api.Runtime;

public sealed record RuntimeHaProtectionOptions(
    bool Enabled,
    bool AutomaticFailoverEnabled,
    string? ReferencePath,
    TimeSpan LeaseDuration,
    TimeSpan PollInterval,
    TimeSpan ReadyWitnessMaximumAge)
{
    public static RuntimeHaProtectionOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection("HighAvailability:Protection");
        var enabled = section.GetValue<bool?>("Enabled") ?? false;
        var automatic = section.GetValue<bool?>("AutomaticFailoverEnabled") ?? enabled;
        var leaseSeconds = Math.Clamp(section.GetValue<int?>("LeaseSeconds") ?? 10, 3, 120);
        var pollMilliseconds = Math.Clamp(section.GetValue<int?>("PollMilliseconds") ?? 500, 100, 10_000);
        var witnessSeconds = Math.Clamp(section.GetValue<int?>("ReadyWitnessMaximumAgeSeconds") ?? 60, leaseSeconds, 600);
        var path = section["ReferencePath"];
        if (enabled && string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException(
                "HighAvailability:Protection:ReferencePath is required when D2 protection is enabled.");
        }

        return new RuntimeHaProtectionOptions(
            enabled,
            automatic,
            string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path.Trim()),
            TimeSpan.FromSeconds(leaseSeconds),
            TimeSpan.FromMilliseconds(pollMilliseconds),
            TimeSpan.FromSeconds(witnessSeconds));
    }
}

public sealed record RuntimeHaReferenceAuthority(
    string Schema,
    int SchemaVersion,
    string ClusterId,
    long TopologyVersion,
    long Epoch,
    string? ActiveNodeId,
    Guid LeaseId,
    DateTimeOffset LeaseUntilUtc,
    DateTimeOffset UpdatedAtUtc,
    bool PreviousAuthorityFenced,
    string ReasonCode)
{
    public const string SchemaName = "elitescada.runtime-ha-reference-authority";
    public const int CurrentSchemaVersion = 1;

    public bool IsLiveAt(DateTimeOffset now) =>
        ActiveNodeId is not null &&
        LeaseId != Guid.Empty &&
        LeaseUntilUtc > now;
}

public sealed record RuntimeHaReferenceMutationResult(
    bool Accepted,
    string ReasonCode,
    RuntimeHaReferenceAuthority? Authority);

public interface IRuntimeHaReferenceAuthorityStore
{
    Task<RuntimeHaReferenceAuthority?> ReadAsync(CancellationToken cancellationToken = default);

    Task<RuntimeHaReferenceMutationResult> InitializeAsync(
        string initialActiveNodeId,
        CancellationToken cancellationToken = default);

    Task<RuntimeHaReferenceMutationResult> RenewAsync(
        string nodeId,
        Guid leaseId,
        long expectedEpoch,
        CancellationToken cancellationToken = default);

    Task<RuntimeHaReferenceMutationResult> ClaimExpiredAsync(
        string nodeId,
        long expectedEpoch,
        CancellationToken cancellationToken = default);

    Task<RuntimeHaReferenceMutationResult> FenceAsync(
        string nodeId,
        Guid leaseId,
        long expectedEpoch,
        string reasonCode,
        CancellationToken cancellationToken = default);

    Task<RuntimeHaReferenceMutationResult> AssignAfterFenceAsync(
        string targetNodeId,
        long expectedBreakEpoch,
        string reasonCode,
        CancellationToken cancellationToken = default);
}

public sealed class FileRuntimeHaReferenceAuthorityStore : IRuntimeHaReferenceAuthorityStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly RuntimeHaProtectionOptions _options;
    private readonly RuntimeHighAvailabilityService _highAvailability;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public FileRuntimeHaReferenceAuthorityStore(
        RuntimeHaProtectionOptions options,
        RuntimeHighAvailabilityService highAvailability)
        : this(options, highAvailability, () => DateTimeOffset.UtcNow)
    {
    }

    internal FileRuntimeHaReferenceAuthorityStore(
        RuntimeHaProtectionOptions options,
        RuntimeHighAvailabilityService highAvailability,
        Func<DateTimeOffset> utcNow)
    {
        _options = options;
        _highAvailability = highAvailability;
        _utcNow = utcNow;
    }

    public async Task<RuntimeHaReferenceAuthority?> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ReferencePath))
            return null;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await ReadCurrentAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<RuntimeHaReferenceMutationResult> InitializeAsync(
        string initialActiveNodeId,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            current =>
            {
                if (current is not null)
                    return new(false, "reference-already-initialized", current);

                var now = _utcNow();
                var next = Create(
                    epoch: 1,
                    activeNodeId: initialActiveNodeId,
                    leaseUntilUtc: now + _options.LeaseDuration,
                    previousAuthorityFenced: true,
                    reasonCode: "reference-initialized");
                return new(true, "reference-initialized", next);
            },
            cancellationToken);

    public Task<RuntimeHaReferenceMutationResult> RenewAsync(
        string nodeId,
        Guid leaseId,
        long expectedEpoch,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            current =>
            {
                if (current is null)
                    return new(false, "reference-uninitialized", null);
                if (current.Epoch != expectedEpoch)
                    return new(false, "reference-epoch-stale", current);
                if (!string.Equals(current.ActiveNodeId, nodeId, StringComparison.OrdinalIgnoreCase) ||
                    current.LeaseId != leaseId)
                {
                    return new(false, "reference-lease-mismatch", current);
                }

                var now = _utcNow();
                if (current.LeaseUntilUtc <= now)
                    return new(false, "reference-lease-expired", current);

                var next = current with
                {
                    LeaseUntilUtc = now + _options.LeaseDuration,
                    UpdatedAtUtc = now,
                    ReasonCode = "reference-renewed"
                };
                return new(true, "reference-renewed", next);
            },
            cancellationToken);

    public Task<RuntimeHaReferenceMutationResult> ClaimExpiredAsync(
        string nodeId,
        long expectedEpoch,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            current =>
            {
                if (current is null)
                    return new(false, "reference-uninitialized", null);
                if (current.Epoch != expectedEpoch)
                    return new(false, "reference-epoch-stale", current);

                var now = _utcNow();
                if (current.IsLiveAt(now))
                    return new(false, "reference-lease-still-live", current);
                if (current.Epoch == long.MaxValue)
                    return new(false, "reference-epoch-exhausted", current);

                var next = Create(
                    checked(current.Epoch + 1),
                    nodeId,
                    now + _options.LeaseDuration,
                    previousAuthorityFenced: true,
                    reasonCode: "automatic-failover-claimed");
                return new(true, "automatic-failover-claimed", next);
            },
            cancellationToken);

    public Task<RuntimeHaReferenceMutationResult> FenceAsync(
        string nodeId,
        Guid leaseId,
        long expectedEpoch,
        string reasonCode,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            current =>
            {
                if (current is null)
                    return new(false, "reference-uninitialized", null);
                if (current.Epoch != expectedEpoch)
                    return new(false, "reference-epoch-stale", current);
                if (!string.Equals(current.ActiveNodeId, nodeId, StringComparison.OrdinalIgnoreCase) ||
                    current.LeaseId != leaseId)
                {
                    return new(false, "reference-lease-mismatch", current);
                }
                if (current.Epoch == long.MaxValue)
                    return new(false, "reference-epoch-exhausted", current);

                var now = _utcNow();
                var next = Create(
                    checked(current.Epoch + 1),
                    activeNodeId: null,
                    leaseUntilUtc: now,
                    previousAuthorityFenced: true,
                    reasonCode: string.IsNullOrWhiteSpace(reasonCode)
                        ? "controlled-break"
                        : reasonCode.Trim());
                return new(true, "reference-fenced", next);
            },
            cancellationToken);

    public Task<RuntimeHaReferenceMutationResult> AssignAfterFenceAsync(
        string targetNodeId,
        long expectedBreakEpoch,
        string reasonCode,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            current =>
            {
                if (current is null)
                    return new(false, "reference-uninitialized", null);
                if (current.Epoch != expectedBreakEpoch)
                    return new(false, "reference-epoch-stale", current);
                if (current.ActiveNodeId is not null || !current.PreviousAuthorityFenced)
                    return new(false, "reference-break-required", current);

                var now = _utcNow();
                var next = Create(
                    current.Epoch,
                    targetNodeId,
                    now + _options.LeaseDuration,
                    previousAuthorityFenced: true,
                    reasonCode: string.IsNullOrWhiteSpace(reasonCode)
                        ? "controlled-grant"
                        : reasonCode.Trim());
                return new(true, "reference-assigned", next);
            },
            cancellationToken);

    private async Task<RuntimeHaReferenceMutationResult> MutateAsync(
        Func<RuntimeHaReferenceAuthority?, RuntimeHaReferenceMutationResult> mutation,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ReferencePath))
            return new(false, "reference-disabled", null);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await using var lockHandle = await AcquireCrossProcessLockAsync(cancellationToken);
            var current = await ReadCurrentAsync(cancellationToken);
            var result = mutation(current);
            if (!result.Accepted || result.Authority is null)
                return result;

            await PersistAsync(result.Authority, cancellationToken);
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<RuntimeHaReferenceAuthority?> ReadCurrentAsync(
        CancellationToken cancellationToken)
    {
        var path = _options.ReferencePath!;
        if (!File.Exists(path))
            return null;

        try
        {
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            var value = JsonSerializer.Deserialize<RuntimeHaReferenceAuthority>(bytes, Json)
                ?? throw new InvalidDataException("HA reference file is empty.");
            Validate(value);
            return value;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("HA reference file is invalid.", ex);
        }
    }

    private async Task PersistAsync(
        RuntimeHaReferenceAuthority value,
        CancellationToken cancellationToken)
    {
        var path = _options.ReferencePath!;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(
                temporary,
                JsonSerializer.SerializeToUtf8Bytes(value, Json),
                cancellationToken);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private async Task<FileStream> AcquireCrossProcessLockAsync(
        CancellationToken cancellationToken)
    {
        var lockPath = _options.ReferencePath! + ".lock";
        var directory = Path.GetDirectoryName(lockPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var deadline = _utcNow() + TimeSpan.FromSeconds(2);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.DeleteOnClose);
            }
            catch (IOException) when (_utcNow() < deadline)
            {
                await Task.Delay(20, cancellationToken);
            }
        }
    }

    private RuntimeHaReferenceAuthority Create(
        long epoch,
        string? activeNodeId,
        DateTimeOffset leaseUntilUtc,
        bool previousAuthorityFenced,
        string reasonCode)
    {
        var topology = _highAvailability.Authority.Definition;
        return new RuntimeHaReferenceAuthority(
            RuntimeHaReferenceAuthority.SchemaName,
            RuntimeHaReferenceAuthority.CurrentSchemaVersion,
            topology.ClusterId!,
            topology.TopologyVersion,
            epoch,
            activeNodeId,
            activeNodeId is null ? Guid.Empty : Guid.NewGuid(),
            leaseUntilUtc,
            _utcNow(),
            previousAuthorityFenced,
            reasonCode);
    }

    private void Validate(RuntimeHaReferenceAuthority value)
    {
        var topology = _highAvailability.Authority.Definition;
        if (!string.Equals(value.Schema, RuntimeHaReferenceAuthority.SchemaName, StringComparison.Ordinal) ||
            value.SchemaVersion != RuntimeHaReferenceAuthority.CurrentSchemaVersion)
        {
            throw new InvalidDataException("HA reference schema is incompatible.");
        }
        if (!string.Equals(value.ClusterId, topology.ClusterId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("HA reference cluster does not match local HA topology.");
        if (value.TopologyVersion != topology.TopologyVersion)
            throw new InvalidDataException("HA reference topology version does not match local HA topology.");
        if (value.Epoch < 1)
            throw new InvalidDataException("HA reference epoch must be positive.");
        if (value.ActiveNodeId is not null &&
            !topology.Nodes.Any(node => node.NodeId.Equals(value.ActiveNodeId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException("HA reference active node is not present in the configured topology.");
        }
    }
}

public sealed record RuntimeHaProtectionDiagnostics(
    bool Enabled,
    bool AutomaticFailoverEnabled,
    bool IndustrialEffectsPermitted,
    string Status,
    string? ReasonCode,
    RuntimeHaReferenceAuthority? Reference,
    RuntimeHaStandbyPromotionWitness? LastReadyStandbyWitness,
    RuntimeHaTopologySnapshot Topology);

public sealed record RuntimeHaProtectionOperation(
    Guid OperationId,
    string Kind,
    string State,
    string? SourceNodeId,
    string? TargetNodeId,
    long? Epoch,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string ReasonCode);

public sealed class RuntimeHaProtectionCoordinator
{
    private readonly object _gate = new();
    private readonly RuntimeHaProtectionOptions _options;
    private readonly RuntimeHighAvailabilityService _highAvailability;
    private readonly IRuntimeHaReferenceAuthorityStore _reference;
    private readonly RuntimeHaPeerTransportState _peerTransport;
    private readonly RuntimeHaPeerMirrorStore _mirror;
    private readonly HighAvailabilityRuntimeCoordinator _runtime;
    private readonly IRuntimeSessionLeaseStore _sessions;
    private readonly Func<DateTimeOffset> _utcNow;
    private RuntimeHaReferenceAuthority? _lastReference;
    private RuntimeHaStandbyPromotionWitness? _lastReadyWitness;
    private bool _localTakeoverCommitted;
    private string? _reasonCode;
    private readonly Dictionary<Guid, RuntimeHaProtectionOperation> _operations = new();

    public RuntimeHaProtectionCoordinator(
        RuntimeHaProtectionOptions options,
        RuntimeHighAvailabilityService highAvailability,
        IRuntimeHaReferenceAuthorityStore reference,
        RuntimeHaPeerTransportState peerTransport,
        RuntimeHaPeerMirrorStore mirror,
        HighAvailabilityRuntimeCoordinator runtime,
        IRuntimeSessionLeaseStore sessions)
        : this(
            options,
            highAvailability,
            reference,
            peerTransport,
            mirror,
            runtime,
            sessions,
            () => DateTimeOffset.UtcNow)
    {
    }

    internal RuntimeHaProtectionCoordinator(
        RuntimeHaProtectionOptions options,
        RuntimeHighAvailabilityService highAvailability,
        IRuntimeHaReferenceAuthorityStore reference,
        RuntimeHaPeerTransportState peerTransport,
        RuntimeHaPeerMirrorStore mirror,
        HighAvailabilityRuntimeCoordinator runtime,
        IRuntimeSessionLeaseStore sessions,
        Func<DateTimeOffset> utcNow)
    {
        _options = options;
        _highAvailability = highAvailability;
        _reference = reference;
        _peerTransport = peerTransport;
        _mirror = mirror;
        _runtime = runtime;
        _sessions = sessions;
        _utcNow = utcNow;

        _highAvailability.AttachExternalIndustrialFence(CanOwnIndustrialEffects);
    }

    public RuntimeHaProtectionDiagnostics Diagnostics()
    {
        CaptureReadyWitness();
        var topology = _highAvailability.Snapshot();
        RuntimeHaReferenceAuthority? reference;
        RuntimeHaStandbyPromotionWitness? witness;
        bool committed;
        string? reason;
        lock (_gate)
        {
            reference = _lastReference;
            witness = _lastReadyWitness;
            committed = _localTakeoverCommitted;
            reason = _reasonCode;
        }

        var permitted = CanOwnIndustrialEffects(topology);
        var status = !_options.Enabled
            ? "disabled"
            : topology.AmbiguousAuthority
                ? "ambiguous"
                : reference is null
                    ? "blocked"
                    : permitted
                        ? "active"
                        : reference.IsLiveAt(_utcNow())
                            ? "standby"
                            : "degraded";

        return new RuntimeHaProtectionDiagnostics(
            _options.Enabled,
            _options.AutomaticFailoverEnabled,
            permitted,
            status,
            reason ?? (committed ? "local-takeover-committed" : null),
            reference,
            witness,
            topology);
    }

    public IReadOnlyCollection<RuntimeHaProtectionOperation> Operations()
    {
        lock (_gate)
        {
            return _operations.Values
                .OrderByDescending(item => item.StartedAtUtc)
                .Take(32)
                .ToArray();
        }
    }

    public RuntimeHaProtectionOperation? GetOperation(Guid operationId)
    {
        lock (_gate)
            return _operations.TryGetValue(operationId, out var operation) ? operation : null;
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return;

        CaptureReadyWitness();

        RuntimeHaReferenceAuthority? reference;
        try
        {
            reference = await _reference.ReadAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            SetReference(null, false, "reference-read-failed");
            return;
        }

        if (reference is null)
        {
            var topology = _highAvailability.Snapshot();
            if (topology.AuthorityEpoch == 1 &&
                topology.EffectiveActiveNodeId is { } active &&
                active.Equals(_highAvailability.Authority.Definition.InitialActiveNodeId, StringComparison.OrdinalIgnoreCase) &&
                active.Equals(_highAvailability.LocalNodeId, StringComparison.OrdinalIgnoreCase))
            {
                var initialized = await _reference.InitializeAsync(active, cancellationToken);
                if (initialized.Authority is not null)
                    reference = initialized.Authority;
            }
        }

        if (reference is null)
        {
            SetReference(null, false, "reference-uninitialized");
            return;
        }

        var now = _utcNow();
        var localNodeId = _highAvailability.LocalNodeId!;
        var current = _highAvailability.Snapshot();

        if (reference.IsLiveAt(now) &&
            reference.ActiveNodeId!.Equals(localNodeId, StringComparison.OrdinalIgnoreCase))
        {
            RuntimeHaStandbyPromotionWitness? witness = null;
            if (current.EffectiveActiveNodeId is null ||
                !current.EffectiveActiveNodeId.Equals(localNodeId, StringComparison.OrdinalIgnoreCase) ||
                current.AuthorityEpoch != reference.Epoch)
            {
                witness = GetFreshWitness(now);
            }

            var applied = _highAvailability.Authority.ApplyReferencedAuthority(
                localNodeId,
                reference.Epoch,
                reference.PreviousAuthorityFenced,
                witness);
            if (!applied.Accepted &&
                applied.ReasonCode != "reference-promotion-ready-standby-witness-required")
            {
                SetReference(reference, false, applied.ReasonCode);
                return;
            }

            var shouldRenew = reference.LeaseUntilUtc - now <= _options.LeaseDuration / 2;
            if (shouldRenew)
            {
                var renewed = await _reference.RenewAsync(
                    localNodeId,
                    reference.LeaseId,
                    reference.Epoch,
                    cancellationToken);
                if (renewed.Authority is not null)
                    reference = renewed.Authority;
            }

            var descriptor = _runtime.Describe();
            var committed = descriptor.Revision.HasValue &&
                !string.IsNullOrWhiteSpace(descriptor.ProjectKey);
            SetReference(reference, committed, committed ? "reference-local-active" : "local-runtime-unavailable");
            return;
        }

        if (reference.IsLiveAt(now))
        {
            _highAvailability.Authority.ApplyReferencedAuthority(
                reference.ActiveNodeId,
                reference.Epoch,
                reference.PreviousAuthorityFenced);
            await FenceLocalSessionsIfNeededAsync("ha-reference-demotion", cancellationToken);
            SetReference(reference, false, "reference-owned-by-peer");
            return;
        }

        await FenceLocalSessionsIfNeededAsync("ha-reference-expired", cancellationToken);
        SetReference(reference, false, "reference-lease-expired");

        if (!_options.AutomaticFailoverEnabled)
            return;

        var transport = _peerTransport.Snapshot();
        if (transport.ConnectionState is "connected")
            return;

        var witnessForClaim = GetFreshWitness(now);
        if (witnessForClaim is null)
            return;

        await TryAutomaticFailoverAsync(reference, witnessForClaim, cancellationToken);
    }

    public async Task<RuntimeHaProtectionOperation> RequestControlledSwitchAsync(
        string targetNodeId,
        string kind,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetNodeId);
        var now = _utcNow();
        var operation = AddOperation(kind, targetNodeId, now);

        var reference = await _reference.ReadAsync(cancellationToken);
        var localNodeId = _highAvailability.LocalNodeId!;
        if (reference is null ||
            !reference.IsLiveAt(now) ||
            !string.Equals(reference.ActiveNodeId, localNodeId, StringComparison.OrdinalIgnoreCase))
        {
            return Complete(operation, "rejected", "local-node-is-not-reference-active", reference?.Epoch);
        }

        var target = _highAvailability.Snapshot().Nodes.SingleOrDefault(node =>
            node.NodeId.Equals(targetNodeId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (target is null)
            return Complete(operation, "rejected", "target-node-unknown", reference.Epoch);
        if (!target.Ready || target.State != RuntimeHaState.ReadyStandby || !target.Fresh)
            return Complete(operation, "rejected", "target-not-ready-standby", reference.Epoch);

        var fenced = await _reference.FenceAsync(
            localNodeId,
            reference.LeaseId,
            reference.Epoch,
            kind + "-break",
            cancellationToken);
        if (!fenced.Accepted || fenced.Authority is null)
            return Complete(operation, "rejected", fenced.ReasonCode, fenced.Authority?.Epoch);

        _highAvailability.Authority.ApplyReferencedAuthority(
            activeNodeId: null,
            referencedEpoch: fenced.Authority.Epoch,
            previousAuthorityFenced: true);
        await FenceLocalSessionsIfNeededAsync(
            "ha-" + kind + "-break",
            cancellationToken,
            force: true);
        SetReference(fenced.Authority, false, "controlled-break");

        var assigned = await _reference.AssignAfterFenceAsync(
            target.NodeId,
            fenced.Authority.Epoch,
            kind + "-grant",
            cancellationToken);
        if (!assigned.Accepted || assigned.Authority is null)
            return Complete(operation, "failed", assigned.ReasonCode, fenced.Authority.Epoch);

        _highAvailability.Authority.ApplyReferencedAuthority(
            assigned.Authority.ActiveNodeId,
            assigned.Authority.Epoch,
            previousAuthorityFenced: true);
        SetReference(assigned.Authority, false, "controlled-grant");
        return Complete(operation, "completed", assigned.ReasonCode, assigned.Authority.Epoch);
    }

    private async Task TryAutomaticFailoverAsync(
        RuntimeHaReferenceAuthority expiredReference,
        RuntimeHaStandbyPromotionWitness witness,
        CancellationToken cancellationToken)
    {
        var operation = AddOperation("automatic-failover", _highAvailability.LocalNodeId!, _utcNow());
        var claimed = await _reference.ClaimExpiredAsync(
            _highAvailability.LocalNodeId!,
            expiredReference.Epoch,
            cancellationToken);
        if (!claimed.Accepted || claimed.Authority is null)
        {
            Complete(operation, "rejected", claimed.ReasonCode, claimed.Authority?.Epoch);
            return;
        }

        SetReference(claimed.Authority, false, "automatic-failover-claimed");
        var applied = _highAvailability.Authority.ApplyReferencedAuthority(
            _highAvailability.LocalNodeId!,
            claimed.Authority.Epoch,
            previousAuthorityFenced: true,
            witness);
        if (!applied.Accepted)
        {
            Complete(operation, "failed", applied.ReasonCode, claimed.Authority.Epoch);
            return;
        }

        var mirror = _mirror.Snapshot();
        var state = mirror.AuthoritativeState;
        if (!mirror.HasState ||
            state?.Application is null ||
            !state.Runtime.Revision.HasValue ||
            string.IsNullOrWhiteSpace(state.Runtime.ProjectKey))
        {
            await FailClosedAfterClaimAsync(
                claimed.Authority,
                operation,
                "takeover-state-unavailable",
                cancellationToken);
            return;
        }

        var activation = await _runtime.ActivateAsync(
            state.Runtime.ProjectKey,
            state.Runtime.Revision.Value,
            state.Application,
            cancellationToken);
        if (!activation.Activated)
        {
            await FailClosedAfterClaimAsync(
                claimed.Authority,
                operation,
                activation.RuntimeIssues.FirstOrDefault(issue => issue.IsError)?.Code
                    ?? "takeover-runtime-activation-failed",
                cancellationToken);
            return;
        }

        try
        {
            await RebindSessionsForTakeoverAsync(cancellationToken);
        }
        catch
        {
            await FailClosedAfterClaimAsync(
                claimed.Authority,
                operation,
                "takeover-session-rebind-failed",
                CancellationToken.None);
            return;
        }

        SetReference(claimed.Authority, true, "automatic-failover-completed");
        Complete(operation, "completed", "automatic-failover-completed", claimed.Authority.Epoch);
    }

    private async Task FailClosedAfterClaimAsync(
        RuntimeHaReferenceAuthority claimed,
        RuntimeHaProtectionOperation operation,
        string reasonCode,
        CancellationToken cancellationToken)
    {
        var fenced = await _reference.FenceAsync(
            _highAvailability.LocalNodeId!,
            claimed.LeaseId,
            claimed.Epoch,
            reasonCode,
            cancellationToken);
        if (fenced.Authority is not null)
        {
            _highAvailability.Authority.ApplyReferencedAuthority(
                null,
                fenced.Authority.Epoch,
                previousAuthorityFenced: true);
            SetReference(fenced.Authority, false, reasonCode);
        }
        Complete(operation, "failed", reasonCode, fenced.Authority?.Epoch ?? claimed.Epoch);
    }

    private async Task RebindSessionsForTakeoverAsync(CancellationToken cancellationToken)
    {
        var transition = await _sessions.BeginAuthorityTransitionAsync(
            "ha-takeover",
            _utcNow(),
            cancellationToken);
        var authority = await _sessions.CommitAuthorityChangeAsync(
            transition.TransitionId,
            transition.BaseAuthorityRevision,
            _utcNow(),
            demoStartedAtUtc: null,
            cancellationToken);
        await _sessions.RebindActiveClusterLeasesAsync(
            transition.TransitionId,
            authority.AuthorityRevision,
            _highAvailability.ClusterId!,
            _highAvailability.LocalNodeId!,
            cancellationToken);
        await _sessions.FenceLeasesBeforeAuthorityRevisionAsync(
            transition.TransitionId,
            authority.AuthorityRevision,
            cancellationToken);
        await _sessions.CompleteAuthorityTransitionAsync(
            transition.TransitionId,
            authority.AuthorityRevision,
            cancellationToken);
    }

    private async Task FenceLocalSessionsIfNeededAsync(
        string transitionKind,
        CancellationToken cancellationToken,
        bool force = false)
    {
        if (!force)
        {
            lock (_gate)
            {
                if (!_localTakeoverCommitted)
                    return;
            }
        }

        var authority = await _sessions.GetAuthorityStateAsync(cancellationToken);
        if (authority.TransitionPending)
            return;

        var transition = await _sessions.BeginAuthorityTransitionAsync(
            transitionKind,
            _utcNow(),
            cancellationToken);
        var committed = await _sessions.CommitAuthorityChangeAsync(
            transition.TransitionId,
            transition.BaseAuthorityRevision,
            _utcNow(),
            demoStartedAtUtc: null,
            cancellationToken);
        await _sessions.FenceLeasesBeforeAuthorityRevisionAsync(
            transition.TransitionId,
            committed.AuthorityRevision,
            cancellationToken);
        await _sessions.CompleteAuthorityTransitionAsync(
            transition.TransitionId,
            committed.AuthorityRevision,
            cancellationToken);
    }

    private bool CanOwnIndustrialEffects(RuntimeHaTopologySnapshot topology)
    {
        if (!_options.Enabled)
            return true;

        RuntimeHaReferenceAuthority? reference;
        bool committed;
        lock (_gate)
        {
            reference = _lastReference;
            committed = _localTakeoverCommitted;
        }

        var localNodeId = _highAvailability.LocalNodeId;
        return committed &&
            localNodeId is not null &&
            reference is not null &&
            reference.IsLiveAt(_utcNow()) &&
            reference.ActiveNodeId is not null &&
            reference.ActiveNodeId.Equals(localNodeId, StringComparison.OrdinalIgnoreCase) &&
            reference.Epoch == topology.AuthorityEpoch &&
            topology.EffectiveActiveNodeId is not null &&
            topology.EffectiveActiveNodeId.Equals(localNodeId, StringComparison.OrdinalIgnoreCase) &&
            !topology.AmbiguousAuthority &&
            topology.PendingTransfer is null;
    }

    private void CaptureReadyWitness()
    {
        if (!_options.Enabled || _highAvailability.LocalNodeId is null)
            return;

        var witness = _highAvailability.Authority.CaptureStandbyPromotionWitness(
            _highAvailability.LocalNodeId);
        if (witness is null)
            return;

        lock (_gate)
            _lastReadyWitness = witness;
    }

    private RuntimeHaStandbyPromotionWitness? GetFreshWitness(DateTimeOffset now)
    {
        lock (_gate)
        {
            if (_lastReadyWitness is null)
                return null;
            return now - _lastReadyWitness.ReadyAtUtc <= _options.ReadyWitnessMaximumAge
                ? _lastReadyWitness
                : null;
        }
    }

    private void SetReference(
        RuntimeHaReferenceAuthority? reference,
        bool localTakeoverCommitted,
        string reasonCode)
    {
        lock (_gate)
        {
            _lastReference = reference;
            _localTakeoverCommitted = localTakeoverCommitted;
            _reasonCode = reasonCode;
        }
    }

    private RuntimeHaProtectionOperation AddOperation(
        string kind,
        string? targetNodeId,
        DateTimeOffset startedAtUtc)
    {
        var current = _highAvailability.Snapshot();
        var operation = new RuntimeHaProtectionOperation(
            Guid.NewGuid(),
            kind,
            "running",
            current.EffectiveActiveNodeId,
            targetNodeId,
            current.AuthorityEpoch,
            startedAtUtc,
            null,
            "started");
        lock (_gate)
        {
            _operations[operation.OperationId] = operation;
            foreach (var stale in _operations.Values
                         .OrderByDescending(item => item.StartedAtUtc)
                         .Skip(32)
                         .Select(item => item.OperationId)
                         .ToArray())
            {
                _operations.Remove(stale);
            }
        }
        return operation;
    }

    private RuntimeHaProtectionOperation Complete(
        RuntimeHaProtectionOperation operation,
        string state,
        string reasonCode,
        long? epoch)
    {
        var completed = operation with
        {
            State = state,
            Epoch = epoch,
            CompletedAtUtc = _utcNow(),
            ReasonCode = reasonCode
        };
        lock (_gate)
            _operations[completed.OperationId] = completed;
        return completed;
    }
}

public sealed class RuntimeHaProtectionHostedService(
    RuntimeHaProtectionOptions options,
    RuntimeHaProtectionCoordinator coordinator,
    ILogger<RuntimeHaProtectionHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await coordinator.RefreshAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "HA-D2 protection reconciliation failed closed.");
            }

            await Task.Delay(options.PollInterval, stoppingToken);
        }
    }
}

public static class RuntimeHaProtectionComposition
{
    public static IServiceCollection AddRuntimeHighAvailabilityProtection(
        this IServiceCollection services)
    {
        services.AddSingleton(sp =>
            RuntimeHaProtectionOptions.FromConfiguration(
                sp.GetRequiredService<IConfiguration>()));
        services.AddSingleton<IRuntimeHaReferenceAuthorityStore, FileRuntimeHaReferenceAuthorityStore>();
        services.AddSingleton<RuntimeHaProtectionCoordinator>();
        services.AddHostedService<RuntimeHaProtectionHostedService>();
        return services;
    }
}
