using System.Text.Json;
using Microsoft.Extensions.Logging;
using Scada.Core.Tags;
using Scada.Security.Authorization;

namespace Scada.Api.Runtime;

public sealed record RuntimeHaProtectionOptions(
    bool Enabled,
    bool AutomaticFailoverEnabled,
    string? ReferencePath,
    TimeSpan LeaseDuration,
    TimeSpan PollInterval,
    TimeSpan ReadyWitnessMaximumAge,
    TimeSpan ClockSkewSafetyMargin,
    string? OperationHistoryPath = null)
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
        var maximumSkewSeconds = Math.Max(0, leaseSeconds / 3);
        var skewSeconds = Math.Clamp(
            section.GetValue<int?>("ClockSkewSafetyMarginSeconds") ?? 1,
            0,
            maximumSkewSeconds);
        var path = section["ReferencePath"];
        if (enabled && string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException(
                "HighAvailability:Protection:ReferencePath is required when D2 protection is enabled.");
        }

        var localNodeId = configuration["HighAvailability:NodeId"];
        if (string.IsNullOrWhiteSpace(localNodeId))
            localNodeId = RuntimeHaTopologyDefinition.FromConfiguration(configuration).LocalNodeId;

        return new RuntimeHaProtectionOptions(
            enabled,
            automatic,
            string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path.Trim()),
            TimeSpan.FromSeconds(leaseSeconds),
            TimeSpan.FromMilliseconds(pollMilliseconds),
            TimeSpan.FromSeconds(witnessSeconds),
            TimeSpan.FromSeconds(skewSeconds),
            ResolveOperationHistoryPath(configuration, localNodeId));
    }

    public static string ResolveOperationHistoryPath(IConfiguration configuration, string localNodeId)
    {
        var configured = configuration["HighAvailability:Protection:OperationHistoryPath"];
        if (!string.IsNullOrWhiteSpace(configured))
            return Path.GetFullPath(configured.Trim());

        var nodeHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(localNodeId)))
            .ToLowerInvariant()[..20];
        return Path.Combine(AppContext.BaseDirectory, "data", "ha", $"{nodeHash}.operations.json");
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
                if (current.ActiveNodeId is not null &&
                    current.LeaseUntilUtc + _options.ClockSkewSafetyMargin > now)
                {
                    return new(false, "reference-fence-safety-window-active", current);
                }
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
    private readonly RuntimeHaHostConfigurationAuthority? _hostConfiguration;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly IRuntimeHaProtectionOperationHistoryStore? _operationHistoryStore;
    private readonly ILogger<RuntimeHaProtectionCoordinator>? _logger;
    private RuntimeHaReferenceAuthority? _lastReference;
    private RuntimeHaStandbyPromotionWitness? _lastReadyWitness;
    private bool _localTakeoverCommitted;
    private string? _reasonCode;
    private DateTimeOffset? _lastDatabaseFailoverAttemptUtc;
    private readonly Dictionary<Guid, RuntimeHaProtectionOperation> _operations = new();

    public RuntimeHaProtectionCoordinator(
        RuntimeHaProtectionOptions options,
        RuntimeHighAvailabilityService highAvailability,
        IRuntimeHaReferenceAuthorityStore reference,
        RuntimeHaPeerTransportState peerTransport,
        RuntimeHaPeerMirrorStore mirror,
        HighAvailabilityRuntimeCoordinator runtime,
        IRuntimeSessionLeaseStore sessions,
        RuntimeHaHostConfigurationAuthority? hostConfiguration = null,
        IRuntimeHaProtectionOperationHistoryStore? operationHistoryStore = null,
        ILogger<RuntimeHaProtectionCoordinator>? logger = null)
        : this(
            options,
            highAvailability,
            reference,
            peerTransport,
            mirror,
            runtime,
            sessions,
            () => DateTimeOffset.UtcNow,
            hostConfiguration,
            operationHistoryStore,
            logger)
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
        Func<DateTimeOffset> utcNow,
        RuntimeHaHostConfigurationAuthority? hostConfiguration = null,
        IRuntimeHaProtectionOperationHistoryStore? operationHistoryStore = null,
        ILogger<RuntimeHaProtectionCoordinator>? logger = null)
    {
        _options = options;
        _highAvailability = highAvailability;
        _reference = reference;
        _peerTransport = peerTransport;
        _mirror = mirror;
        _runtime = runtime;
        _sessions = sessions;
        _hostConfiguration = hostConfiguration;
        _utcNow = utcNow;
        _operationHistoryStore = operationHistoryStore;
        _logger = logger;

        if (_operationHistoryStore is not null)
        {
            try
            {
                foreach (var operation in _operationHistoryStore.Load())
                    _operations[operation.OperationId] = operation;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                _logger?.LogWarning(ex, "HA operation history could not be loaded; continuing with an empty local history.");
            }
        }

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

        var configurationPending = _hostConfiguration?.PendingRestart == true;
        var permitted = CanOwnIndustrialEffects(topology);
        var localNodeOwnsLiveReference =
            reference?.ActiveNodeId is { } activeNodeId &&
            activeNodeId.Equals(
                _highAvailability.LocalNodeId,
                StringComparison.OrdinalIgnoreCase) &&
            reference.IsLiveAt(_utcNow());
        var status = configurationPending
            ? "blocked"
            : !_options.Enabled
                ? "disabled"
                : topology.AmbiguousAuthority
                    ? "ambiguous"
                    : reference is null
                        ? "blocked"
                        : permitted
                            ? "active"
                            : reference.IsLiveAt(_utcNow()) && !localNodeOwnsLiveReference
                                ? "standby"
                                : "degraded";

        return new RuntimeHaProtectionDiagnostics(
            _options.Enabled,
            _options.AutomaticFailoverEnabled,
            permitted,
            status,
            configurationPending
                ? "host-configuration-restart-required"
                : reason ?? (committed ? "local-takeover-committed" : null),
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

        if (reference is null &&
            (_hostConfiguration?.AllowsReferenceBootstrap ?? true))
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
                witness,
                restartReferenceEvidence: reference);
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
                if (!renewed.Accepted || renewed.Authority is null)
                {
                    var rejectedAuthority = renewed.Authority ?? reference;
                    _highAvailability.Authority.ApplyReferencedAuthority(
                        activeNodeId: null,
                        referencedEpoch: rejectedAuthority.Epoch,
                        previousAuthorityFenced: true);
                    await FenceLocalSessionsIfNeededAsync(
                        "ha-reference-renewal-failed",
                        cancellationToken,
                        force: true);
                    SetReference(rejectedAuthority, false, renewed.ReasonCode);
                    return;
                }

                reference = renewed.Authority;
            }

            if (reference.IsLiveAt(now) &&
                reference.ActiveNodeId!.Equals(localNodeId, StringComparison.OrdinalIgnoreCase) &&
                await TryTransferForDatabaseOutageAsync(now, cancellationToken))
            {
                return;
            }

            var localDatabase = current.Nodes.SingleOrDefault(node =>
                node.NodeId.Equals(localNodeId, StringComparison.OrdinalIgnoreCase));
            if (!_runtime.ProductRuntimeActive &&
                (localDatabase?.DatabaseAvailable == false ||
                 localDatabase?.DatabaseAvailabilityConsecutiveFailures > 0))
            {
                // The reference authority is still valid and renewed above. A database
                // outage may make a fresh runtime activation impossible, but that must
                // not fence the last live Active node or turn the outage into a cluster
                // stop. Keep the authority and expose the degraded state until recovery.
                SetReference(reference, false, "database-unavailable-runtime-continues-degraded");
                return;
            }
            if (!_runtime.ProductRuntimeActive &&
                localDatabase?.DatabaseAvailable == true &&
                localDatabase.DatabaseAvailabilityConsecutiveSuccesses < 2)
            {
                SetReference(reference, false, "database-recovery-health-confirming");
                return;
            }

            var descriptor = _runtime.Describe();
            if (descriptor.Revision.HasValue &&
                !string.IsNullOrWhiteSpace(descriptor.ProjectKey) &&
                !_runtime.ProductRuntimeActive)
            {
                var mirror = _mirror.Snapshot();
                var state = mirror.AuthoritativeState;
                var operation = AddOperation("authority-runtime-promotion", localNodeId, now);
                try
                {
                    if (!mirror.HasState ||
                        state?.Application is null ||
                        state.Runtime.Revision != descriptor.Revision ||
                        !string.Equals(
                            state.Runtime.ProjectKey,
                            descriptor.ProjectKey,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        await FailClosedAfterClaimAsync(
                            reference,
                            operation,
                            "takeover-state-unavailable",
                            cancellationToken);
                        return;
                    }

                    var takeover = await RunTakeoverWithLeaseRenewalAsync(
                        reference,
                        async takeoverToken =>
                        {
                            var activationFailure = await ActivateAuthoritativeRuntimeAsync(
                                state,
                                takeoverToken);
                            if (activationFailure is not null)
                                return activationFailure;

                            // The target has fenced authority and its active runtime is
                            // ready. Commit the local runtime fence before restoring values.
                            SetReference(reference, true, "authority-runtime-promotion-in-progress");
                            var restoreFailure = await RestoreAuthoritativeValuesAsync(
                                state,
                                takeoverToken);
                            if (restoreFailure is not null)
                                return restoreFailure;

                            try
                            {
                                await RebindSessionsForTakeoverAsync(takeoverToken);
                            }
                            catch (OperationCanceledException) when (takeoverToken.IsCancellationRequested)
                            {
                                throw;
                            }
                            catch
                            {
                                return "takeover-session-rebind-failed";
                            }

                            return null;
                        },
                        cancellationToken);
                    reference = takeover.Authority;
                    if (takeover.FailureReason is not null)
                    {
                        await FailClosedAfterClaimAsync(
                            reference,
                            operation,
                            takeover.FailureReason,
                            CancellationToken.None);
                        return;
                    }

                    SetReference(reference, true, "authority-runtime-promotion-completed");
                    Complete(operation, "completed", "authority-runtime-promotion-completed", reference.Epoch);
                    return;
                }
                catch (OperationCanceledException)
                {
                    try
                    {
                        await FailClosedAfterClaimAsync(
                            reference,
                            operation,
                            "authority-runtime-promotion-cancelled",
                            CancellationToken.None);
                    }
                    catch
                    {
                        Complete(operation, "failed", "authority-runtime-promotion-cancelled", reference.Epoch);
                    }
                    throw;
                }
                catch (Exception)
                {
                    // The hosted reconciler logs the exception. Fence any partially restored
                    // authority and make the operation terminal so the UI cannot show a ghost promotion.
                    try
                    {
                        await FailClosedAfterClaimAsync(
                            reference,
                            operation,
                            "authority-runtime-promotion-reconciliation-failed",
                            CancellationToken.None);
                    }
                    catch
                    {
                        Complete(operation, "failed", "authority-runtime-promotion-reconciliation-failed", reference.Epoch);
                    }
                    throw;
                }
            }

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

        _highAvailability.Authority.ApplyReferencedAuthority(
            activeNodeId: null,
            referencedEpoch: reference.Epoch,
            previousAuthorityFenced: true);
        var expiredReferenceLocalNode = _highAvailability.Snapshot().Nodes.SingleOrDefault(node =>
            node.NodeId.Equals(localNodeId, StringComparison.OrdinalIgnoreCase));
        if (expiredReferenceLocalNode?.DatabaseAvailable != false &&
            expiredReferenceLocalNode?.DatabaseAvailabilityConsecutiveFailures is not > 0)
        {
            await FenceLocalSessionsIfNeededAsync("ha-reference-expired", cancellationToken);
        }
        SetReference(reference, false, "reference-lease-expired");

        if (!_options.AutomaticFailoverEnabled)
            return;

        var localNode = _highAvailability.Snapshot().Nodes.SingleOrDefault(node =>
            node.NodeId.Equals(_highAvailability.LocalNodeId, StringComparison.OrdinalIgnoreCase));
        if (localNode?.DatabaseAvailable != true ||
            localNode.DatabaseAvailabilityConsecutiveSuccesses < 2 ||
            localNode.DatabaseAvailabilityConsecutiveFailures > 0)
        {
            SetReasonCode("database-unavailable-automatic-failover-blocked");
            return;
        }

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
        var local = _highAvailability.Snapshot().Nodes.SingleOrDefault(node =>
            node.NodeId.Equals(localNodeId, StringComparison.OrdinalIgnoreCase));
        var databaseOutageTransfer =
            string.Equals(kind, "database-failover", StringComparison.Ordinal) &&
            local is not null &&
            HasDatabaseHealthProofAfterOutage(
                local.DatabaseAvailable,
                local.DatabaseAvailabilityObservedAtUtc,
                target.DatabaseAvailable,
                target.DatabaseAvailabilityObservedAtUtc,
                target.DatabaseAvailabilityConsecutiveSuccesses,
                target.DatabaseAvailabilityConsecutiveFailures,
                _options.ClockSkewSafetyMargin);
        if (string.Equals(kind, "database-failover", StringComparison.Ordinal) &&
            !databaseOutageTransfer)
            return Complete(
                operation,
                "rejected",
                "target-database-not-confirmed-after-source-outage",
                reference.Epoch);

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
        if (databaseOutageTransfer)
        {
            // The external reference fence is already committed. The failed local
            // database cannot persist session-lease transitions; do not let that
            // secondary store prevent authority from moving to the healthy node.
            // Local industrial effects are fenced by the reference authority above.
            _logger?.LogWarning(
                "Skipping durable local session transition during database failover from {SourceNodeId}; reference fencing remains authoritative.",
                localNodeId);
        }
        else
        {
            await FenceLocalSessionsIfNeededAsync(
                "ha-" + kind + "-break",
                cancellationToken,
                force: true);
        }
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

    private async Task<bool> TryTransferForDatabaseOutageAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var topology = _highAvailability.Snapshot();
        var local = topology.Nodes.SingleOrDefault(node =>
            node.NodeId.Equals(_highAvailability.LocalNodeId, StringComparison.OrdinalIgnoreCase));
        if (local?.DatabaseAvailable != false)
            return false;

        var target = topology.Nodes
            .Where(node => !node.NodeId.Equals(_highAvailability.LocalNodeId, StringComparison.OrdinalIgnoreCase))
            .Where(node => node.Ready && node.State == RuntimeHaState.ReadyStandby && node.Fresh)
            .Where(node => local is not null && HasDatabaseHealthProofAfterOutage(
                local.DatabaseAvailable,
                local.DatabaseAvailabilityObservedAtUtc,
                node.DatabaseAvailable,
                node.DatabaseAvailabilityObservedAtUtc,
                node.DatabaseAvailabilityConsecutiveSuccesses,
                node.DatabaseAvailabilityConsecutiveFailures,
                _options.ClockSkewSafetyMargin))
            .OrderBy(node => node.NodeId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (target is null)
        {
            // Both stores are unavailable (or the peer has not confirmed health). Keep the
            // current Active authority; DB loss alone is not a reason to stop runtime.
            SetReasonCode("database-unavailable-runtime-continues-degraded");
            return false;
        }

        if (!_options.AutomaticFailoverEnabled)
        {
            SetReasonCode("database-unavailable-peer-healthy-manual-switch-available");
            return false;
        }

        lock (_gate)
        {
            if (_lastDatabaseFailoverAttemptUtc is { } lastAttempt &&
                now - lastAttempt < TimeSpan.FromSeconds(15))
                return false;
            _lastDatabaseFailoverAttemptUtc = now;
        }

        var operation = await RequestControlledSwitchAsync(
            target.NodeId,
            "database-failover",
            cancellationToken);
        return !string.Equals(operation.State, "rejected", StringComparison.Ordinal);
    }

    internal static bool HasDatabaseHealthProofAfterOutage(
        bool? sourceDatabaseAvailable,
        DateTimeOffset? sourceDatabaseObservedAtUtc,
        bool? targetDatabaseAvailable,
        DateTimeOffset? targetDatabaseObservedAtUtc,
        int targetDatabaseConsecutiveSuccesses,
        int targetDatabaseConsecutiveFailures,
        TimeSpan clockSkewSafetyMargin)
    {
        if (sourceDatabaseAvailable != false ||
            targetDatabaseAvailable != true ||
            targetDatabaseConsecutiveFailures > 0 ||
            sourceDatabaseObservedAtUtc is not { } outageObservedAtUtc ||
            targetDatabaseObservedAtUtc is not { } targetHealthObservedAtUtc ||
            targetDatabaseConsecutiveSuccesses < 2)
        {
            return false;
        }

        // Do not hand authority to a peer whose last healthy database probe
        // predates this node's outage detection. In a simultaneous outage that
        // peer's old "healthy" heartbeat must not be mistaken for recovery.
        return targetHealthObservedAtUtc > outageObservedAtUtc + clockSkewSafetyMargin;
    }

    private void SetReasonCode(string reasonCode)
    {
        lock (_gate)
            _reasonCode = reasonCode;
    }

    public async Task<RuntimeHaProtectionOperation> RequestRecoveryAsync(
        string? targetNodeId = null,
        CancellationToken cancellationToken = default)
    {
        var localNodeId = _highAvailability.LocalNodeId!;
        var target = string.IsNullOrWhiteSpace(targetNodeId)
            ? localNodeId
            : targetNodeId.Trim();
        var operation = AddOperation("explicit-recovery", target, _utcNow());

        if (!target.Equals(localNodeId, StringComparison.OrdinalIgnoreCase))
            return Complete(operation, "rejected", "recovery-must-be-requested-on-target-node", null);

        var reference = await _reference.ReadAsync(cancellationToken);
        if (reference is null)
            return Complete(operation, "rejected", "reference-uninitialized", null);
        if (reference.ActiveNodeId is not null || !reference.PreviousAuthorityFenced)
            return Complete(operation, "rejected", "reference-break-required", reference.Epoch);

        var witness = GetFreshWitness(_utcNow());
        if (witness is null)
            return Complete(operation, "rejected", "ready-standby-witness-required", reference.Epoch);

        var assigned = await _reference.AssignAfterFenceAsync(
            localNodeId,
            reference.Epoch,
            "explicit-recovery-grant",
            cancellationToken);
        if (!assigned.Accepted || assigned.Authority is null)
            return Complete(operation, "rejected", assigned.ReasonCode, assigned.Authority?.Epoch);

        await CompleteLocalTakeoverAsync(
            assigned.Authority,
            witness,
            operation,
            "explicit-recovery-completed",
            cancellationToken);
        return GetOperation(operation.OperationId)!;
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

        await CompleteLocalTakeoverAsync(
            claimed.Authority,
            witness,
            operation,
            "automatic-failover-completed",
            cancellationToken);
    }

    private async Task CompleteLocalTakeoverAsync(
        RuntimeHaReferenceAuthority authority,
        RuntimeHaStandbyPromotionWitness witness,
        RuntimeHaProtectionOperation operation,
        string successReasonCode,
        CancellationToken cancellationToken)
    {
        SetReference(authority, false, authority.ReasonCode);
        var takeover = await RunTakeoverWithLeaseRenewalAsync(
            authority,
            async takeoverToken =>
            {
                var applied = _highAvailability.Authority.ApplyReferencedAuthority(
                    _highAvailability.LocalNodeId!,
                    authority.Epoch,
                    previousAuthorityFenced: true,
                    witness);
                if (!applied.Accepted)
                    return applied.ReasonCode;

                var mirror = _mirror.Snapshot();
                var state = mirror.AuthoritativeState;
                if (!mirror.HasState ||
                    state?.Application is null ||
                    !state.Runtime.Revision.HasValue ||
                    string.IsNullOrWhiteSpace(state.Runtime.ProjectKey))
                {
                    return "takeover-state-unavailable";
                }

                var activationFailure = await ActivateAuthoritativeRuntimeAsync(
                    state,
                    takeoverToken);
                if (activationFailure is not null)
                    return activationFailure;

                SetReference(authority, true, "takeover-state-restoration-in-progress");
                var restoreFailure = await RestoreAuthoritativeValuesAsync(
                    state,
                    takeoverToken);
                if (restoreFailure is not null)
                    return restoreFailure;

                try
                {
                    await RebindSessionsForTakeoverAsync(takeoverToken);
                }
                catch (OperationCanceledException) when (takeoverToken.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    return "takeover-session-rebind-failed";
                }

                return null;
            },
            cancellationToken);

        authority = takeover.Authority;
        if (takeover.FailureReason is not null)
        {
            await FailClosedAfterClaimAsync(
                authority,
                operation,
                takeover.FailureReason,
                CancellationToken.None);
            return;
        }

        SetReference(authority, true, successReasonCode);
        Complete(operation, "completed", successReasonCode, authority.Epoch);
    }

    private async Task<string?> ActivateAuthoritativeRuntimeAsync(
        RuntimeHaAuthoritativeStateSnapshot state,
        CancellationToken cancellationToken)
    {
        if (state.Application is null ||
            !state.Runtime.Revision.HasValue ||
            string.IsNullOrWhiteSpace(state.Runtime.ProjectKey))
        {
            return "takeover-state-unavailable";
        }

        var activation = await _runtime.ActivateAsync(
            state.Runtime.ProjectKey,
            state.Runtime.Revision.Value,
            state.Application,
            cancellationToken);
        if (!activation.Activated)
        {
            return activation.RuntimeIssues.FirstOrDefault(issue => issue.IsError)?.Code
                ?? "takeover-runtime-activation-failed";
        }

        return null;
    }

    private async Task<(RuntimeHaReferenceAuthority Authority, string? FailureReason)> RunTakeoverWithLeaseRenewalAsync(
        RuntimeHaReferenceAuthority authority,
        Func<CancellationToken, Task<string?>> takeover,
        CancellationToken cancellationToken)
    {
        using var takeoverCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var latestAuthority = authority;
        string? renewalFailure = null;
        var renewalInterval = TimeSpan.FromMilliseconds(Math.Max(
            100,
            Math.Min(_options.PollInterval.TotalMilliseconds, _options.LeaseDuration.TotalMilliseconds / 4)));

        async Task RenewUntilCanceledAsync()
        {
            try
            {
                while (!takeoverCancellation.IsCancellationRequested)
                {
                    await Task.Delay(renewalInterval, takeoverCancellation.Token);
                    var renewed = await _reference.RenewAsync(
                        _highAvailability.LocalNodeId!,
                        latestAuthority.LeaseId,
                        latestAuthority.Epoch,
                        takeoverCancellation.Token);
                    if (!renewed.Accepted || renewed.Authority is null)
                    {
                        renewalFailure = "takeover-lease-renewal-failed";
                        takeoverCancellation.Cancel();
                        return;
                    }

                    latestAuthority = renewed.Authority;
                    lock (_gate)
                    {
                        if (_lastReference?.LeaseId == latestAuthority.LeaseId &&
                            _lastReference.Epoch == latestAuthority.Epoch)
                            _lastReference = latestAuthority;
                    }
                }
            }
            catch (OperationCanceledException) when (takeoverCancellation.IsCancellationRequested)
            {
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                renewalFailure = "takeover-lease-renewal-failed";
                takeoverCancellation.Cancel();
            }
        }

        var renewalTask = RenewUntilCanceledAsync();
        string? takeoverFailure;
        try
        {
            takeoverFailure = await takeover(takeoverCancellation.Token);
        }
        catch (OperationCanceledException) when (renewalFailure is not null)
        {
            takeoverFailure = renewalFailure;
        }
        finally
        {
            takeoverCancellation.Cancel();
            await renewalTask;
        }

        return (latestAuthority, renewalFailure ?? takeoverFailure);
    }

    private async Task<string?> RestoreAuthoritativeValuesAsync(
        RuntimeHaAuthoritativeStateSnapshot state,
        CancellationToken cancellationToken)
    {
        try
        {
            await _runtime.RestoreAuthoritativeValuesAsync(
                state.Tags.Select(value => new TagValue(
                    value.TagId,
                    value.Value,
                    value.Timestamp,
                    value.Quality,
                    value.Source)
                {
                    SourceTimestamp = value.SourceTimestamp,
                    ServerTimestamp = value.ServerTimestamp
                }).ToArray(),
                cancellationToken);
        }
        catch
        {
            return "takeover-tag-state-restore-failed";
        }

        return null;
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
        if (_hostConfiguration?.AllowsIndustrialEffects == false)
            return false;
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
        var now = _utcNow();
        return committed &&
            localNodeId is not null &&
            reference is not null &&
            reference.ActiveNodeId is not null &&
            reference.LeaseUntilUtc - _options.ClockSkewSafetyMargin > now &&
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
        PersistOperationHistory();
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
        PersistOperationHistory();
        return completed;
    }

    private void PersistOperationHistory()
    {
        if (_operationHistoryStore is null) return;
        try
        {
            RuntimeHaProtectionOperation[] snapshot;
            lock (_gate)
                snapshot = _operations.Values
                    .OrderByDescending(item => item.StartedAtUtc)
                    .Take(32)
                    .ToArray();
            _operationHistoryStore.Save(snapshot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // HA fencing and runtime transitions must not depend on diagnostics storage.
            _logger?.LogWarning(ex, "HA operation history could not be persisted.");
        }
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
            sp.GetRequiredService<RuntimeHaHostConfigurationAuthority>()
                .CreateProtectionOptions());
        services.AddSingleton<IRuntimeHaProtectionOperationHistoryStore>(sp =>
            new FileRuntimeHaProtectionOperationHistoryStore(
                sp.GetRequiredService<RuntimeHaProtectionOptions>().OperationHistoryPath!));
        services.AddSingleton<IRuntimeHaReferenceAuthorityStore, FileRuntimeHaReferenceAuthorityStore>();
        services.AddSingleton<RuntimeHaProtectionCoordinator>();
        services.AddHostedService<RuntimeHaProtectionHostedService>();
        return services;
    }
}
