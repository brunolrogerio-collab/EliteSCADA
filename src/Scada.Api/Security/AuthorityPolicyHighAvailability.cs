using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Scada.Api.Licensing;
using Scada.Api.Runtime;
using Scada.Engineering.Contracts;
using Scada.Engineering.Security;
using Scada.Security.Authorization;

namespace Scada.Api.Security;

public sealed record AuthorityPolicyHaReadiness(
    bool Allowed,
    string ReasonCode,
    long PolicyVersion,
    string? PolicyDigest,
    long HaEpoch);

public sealed record AuthorityPolicyPeerSnapshotEnvelope(
    string Schema,
    int SchemaVersion,
    string ClusterId,
    long TopologyVersion,
    string SourceNodeId,
    Guid SourceLeaseId,
    long SourceEpoch,
    AuthorityPolicySnapshot Snapshot,
    string Digest,
    DateTimeOffset SentAtUtc)
{
    public const string SchemaName = "elitescada.authority-policy-ha-snapshot";
    public const int CurrentSchemaVersion = 1;
}

public sealed record AuthorityPolicyPeerSnapshotAck(
    bool Accepted,
    string ReasonCode,
    string LocalNodeId,
    long AppliedVersion,
    string? AppliedDigest);

/// <summary>Non-owning adapter avoids registering the same disposable DB store twice.</summary>
public sealed class AuthorityPolicyReplicationStoreAdapter(IAuthorityPolicyStore policyStore) : IAuthorityPolicyReplicationStore
{
    private readonly IAuthorityPolicyReplicationStore _inner = policyStore as IAuthorityPolicyReplicationStore
        ?? throw new InvalidOperationException("The configured Authority policy store does not support HA replication.");

    public Task<AuthorityPolicySnapshot> RefreshAsync(CancellationToken cancellationToken = default) =>
        _inner.RefreshAsync(cancellationToken);

    public Task<AuthorityPolicyReplicationApplyResult> ApplyReplicatedSnapshotAsync(
        AuthorityPolicySnapshot snapshot,
        CancellationToken cancellationToken = default) =>
        _inner.ApplyReplicatedSnapshotAsync(snapshot, cancellationToken);
}

/// <summary>
/// Coordinates Stage 1 Intelligence policy changes and replica reconciliation
/// using the existing HA reference authority and authenticated peer channel.
/// It never changes Runtime HA election or general Runtime readiness.
/// </summary>
public sealed class AuthorityPolicyHaCoordinator : IDisposable
{
    public const string PeerReplicationPath = "/api/runtime/ha/peer/authority-policy";

    private static readonly JsonSerializerOptions WireJson = CreateWireJson();
    private readonly IAuthorityPolicyStore _policyStore;
    private readonly IAuthorityPolicyReplicationStore _replicationStore;
    private readonly RuntimeHighAvailabilityService _highAvailability;
    private readonly IRuntimeHaReferenceAuthorityStore _referenceStore;
    private readonly RuntimeHaPeerTransportOptions _peerOptions;
    private readonly RuntimeHaPeerAuthenticator _authenticator;
    private readonly AssetProcessIntelligenceExecutionGate _executionGate;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AuthorityPolicyHaCoordinator> _logger;
    private readonly HttpClient _httpClient;

    public AuthorityPolicyHaCoordinator(
        IAuthorityPolicyStore policyStore,
        IAuthorityPolicyReplicationStore replicationStore,
        RuntimeHighAvailabilityService highAvailability,
        IRuntimeHaReferenceAuthorityStore referenceStore,
        RuntimeHaPeerTransportOptions peerOptions,
        RuntimeHaPeerAuthenticator authenticator,
        AssetProcessIntelligenceExecutionGate executionGate,
        TimeProvider timeProvider,
        ILogger<AuthorityPolicyHaCoordinator> logger,
        HttpMessageHandler? peerHandler = null)
    {
        _policyStore = policyStore;
        _replicationStore = replicationStore;
        _highAvailability = highAvailability;
        _referenceStore = referenceStore;
        _peerOptions = peerOptions;
        _authenticator = authenticator;
        _executionGate = executionGate;
        _timeProvider = timeProvider;
        _logger = logger;
        _httpClient = new HttpClient(
            peerHandler ?? new SocketsHttpHandler { AllowAutoRedirect = false },
            disposeHandler: peerHandler is null)
        {
            Timeout = _peerOptions.RequestTimeout
        };
    }

    public async Task<AuthorityPolicyHaReadiness> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var snapshot = await _replicationStore.RefreshAsync(cancellationToken);
            var digest = AuthorityPolicySnapshotDigest.Compute(snapshot);
            if (!_highAvailability.Enabled)
                return new(true, "standalone", snapshot.Version, digest, 0);

            var reference = await _referenceStore.ReadAsync(cancellationToken);
            if (reference is null)
                return Deny("authority-policy-reference-unavailable", snapshot);
            var topology = _highAvailability.Snapshot();
            if (!_highAvailability.CanOwnIndustrialEffects() ||
                !string.Equals(reference.ActiveNodeId, _highAvailability.LocalNodeId, StringComparison.OrdinalIgnoreCase) ||
                reference.Epoch != topology.AuthorityEpoch ||
                !reference.IsLiveAt(_timeProvider.GetUtcNow()))
                return Deny("authority-policy-effective-active-required", snapshot, reference.Epoch);

            var fence = reference.AuthorityPolicy;
            if (fence is null || fence.CommittedDigest is null)
                return Deny("authority-policy-reference-uninitialized", snapshot, reference.Epoch);
            if (fence.PendingVersion.HasValue)
                return Deny("authority-policy-update-pending", snapshot, reference.Epoch);
            if (snapshot.Version != fence.CommittedVersion ||
                !DigestEquals(digest, fence.CommittedDigest))
                return Deny("authority-policy-reconciliation-required", snapshot, reference.Epoch);

            return new(true, "authority-policy-ready", snapshot.Version, digest, reference.Epoch);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _executionGate.InvalidateAll();
            _logger.LogWarning(exception, "Stage 1 Intelligence Authority HA fence could not be read; permits remain closed.");
            return new(false, "authority-policy-reference-unavailable", _policyStore.Snapshot().Version, null, 0);
        }
    }

    public Task<AuthorityPolicySnapshot> ReadCurrentSnapshotAsync(CancellationToken cancellationToken = default) =>
        _replicationStore.RefreshAsync(cancellationToken);

    public async Task<AuthorityPolicyWriteResult> ApplyMutationAsync(
        long expectedVersion,
        IReadOnlyCollection<SecurityRoleEngineeringDto> roles,
        IReadOnlyCollection<SecurityScopeEngineeringDto> scopes,
        CancellationToken cancellationToken = default)
    {
        if (!_highAvailability.Enabled)
        {
            _executionGate.InvalidateAll();
            return await _policyStore.TryReplaceAsync(expectedVersion, roles, scopes, cancellationToken);
        }

        var readiness = await CheckAsync(cancellationToken);
        if (!readiness.Allowed)
            return new(false, _policyStore.Snapshot(), readiness.ReasonCode);

        var before = await _replicationStore.RefreshAsync(cancellationToken);
        if (before.Version != expectedVersion)
            return new(false, before, "AUTHORITY_POLICY_CONCURRENCY_CONFLICT");
        var candidate = InMemoryAuthorityPolicyStore.Copy(checked(expectedVersion + 1), roles, scopes);
        InMemoryAuthorityPolicyStore.Validate(candidate.Roles, candidate.Scopes);
        var digest = AuthorityPolicySnapshotDigest.Compute(candidate);
        var reference = await _referenceStore.ReadAsync(cancellationToken);
        if (reference is null || reference.LeaseId == Guid.Empty || _highAvailability.LocalNodeId is null)
            return new(false, before, "AUTHORITY_POLICY_HA_REFERENCE_UNAVAILABLE");

        RuntimeHaReferenceMutationResult pending;
        try
        {
            pending = await _referenceStore.BeginAuthorityPolicyUpdateAsync(
                _highAvailability.LocalNodeId,
                reference.LeaseId,
                readiness.HaEpoch,
                readiness.PolicyVersion,
                readiness.PolicyDigest,
                candidate.Version,
                digest,
                cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _executionGate.InvalidateAll();
            _logger.LogWarning(exception, "Could not durably begin Stage 1 Authority policy update; the mutation was not attempted.");
            return new(false, before, "AUTHORITY_POLICY_HA_REFERENCE_WRITE_FAILED");
        }
        if (!pending.Accepted || pending.Authority is null)
            return new(false, before, pending.ReasonCode.ToUpperInvariant().Replace('-', '_'));

        _executionGate.InvalidateAll();
        var write = await _policyStore.TryReplaceAsync(expectedVersion, candidate.Roles, candidate.Scopes, cancellationToken);
        if (!write.Applied)
        {
            var aborted = await _referenceStore.AbortAuthorityPolicyUpdateAsync(
                _highAvailability.LocalNodeId,
                reference.LeaseId,
                readiness.HaEpoch,
                candidate.Version,
                digest,
                cancellationToken);
            if (!aborted.Accepted)
                _logger.LogWarning("Authority policy CAS failed and its pending HA fence could not be aborted: {ReasonCode}.", aborted.ReasonCode);
            return write;
        }

        if (write.Snapshot.Version != candidate.Version ||
            !DigestEquals(AuthorityPolicySnapshotDigest.Compute(write.Snapshot), digest))
        {
            _logger.LogError("Authority policy persistence returned a different version/digest than the HA pending fence.");
            return new(false, write.Snapshot, "AUTHORITY_POLICY_HA_PERSISTED_SNAPSHOT_MISMATCH");
        }

        var synchronized = await SendSnapshotToPeerAsync(
            pending.Authority,
            write.Snapshot,
            digest,
            cancellationToken);
        if (!synchronized)
            return new(false, write.Snapshot, "AUTHORITY_POLICY_HA_REPLICATION_PENDING");

        RuntimeHaReferenceMutationResult committed;
        try
        {
            committed = await _referenceStore.CommitAuthorityPolicyUpdateAsync(
                _highAvailability.LocalNodeId,
                reference.LeaseId,
                readiness.HaEpoch,
                candidate.Version,
                digest,
                cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            // The peer has acknowledged, but until the durable reference commit
            // is confirmed this process must remain fenced and report an
            // explicit pending outcome instead of leaking an HTTP 500.
            _logger.LogError(
                exception,
                "Authority policy replica acknowledged, but the HA fence commit could not be persisted; version {PolicyVersion} remains pending.",
                candidate.Version);
            _executionGate.InvalidateAll();
            return new(false, write.Snapshot, "AUTHORITY_POLICY_HA_COMMIT_UNCONFIRMED");
        }
        if (!committed.Accepted)
        {
            _logger.LogWarning("Authority policy replica acknowledged but HA fence commit failed: {ReasonCode}.", committed.ReasonCode);
            _executionGate.InvalidateAll();
            return new(false, write.Snapshot, "AUTHORITY_POLICY_HA_COMMIT_UNCONFIRMED");
        }

        return write;
    }

    public async Task SynchronizeOnceAsync(CancellationToken cancellationToken = default)
    {
        if (!_highAvailability.Enabled) return;
        try
        {
            var reference = await _referenceStore.ReadAsync(cancellationToken);
            if (reference is null)
            {
                _executionGate.InvalidateAll();
                return;
            }

            var snapshot = await _replicationStore.RefreshAsync(cancellationToken);
            var digest = AuthorityPolicySnapshotDigest.Compute(snapshot);
            var localNodeId = _highAvailability.LocalNodeId;
            if (string.IsNullOrWhiteSpace(localNodeId))
            {
                _executionGate.InvalidateAll();
                return;
            }

            var fence = reference.AuthorityPolicy;
            if (fence is null || fence.CommittedDigest is null)
            {
                if (!IsCurrentEffectiveActive(reference))
                {
                    _executionGate.InvalidateAll();
                    return;
                }

                var begun = await _referenceStore.BeginAuthorityPolicyUpdateAsync(
                    localNodeId,
                    reference.LeaseId,
                    reference.Epoch,
                    expectedCommittedVersion: 0,
                    expectedCommittedDigest: null,
                    pendingVersion: snapshot.Version,
                    pendingDigest: digest,
                    cancellationToken);
                if (!begun.Accepted || begun.Authority is null)
                {
                    _executionGate.InvalidateAll();
                    return;
                }
                reference = begun.Authority;
                fence = reference.AuthorityPolicy;
            }

            if (fence?.PendingVersion is { } pendingVersion)
            {
                _executionGate.InvalidateAll();
                if (!IsCurrentEffectiveActive(reference) ||
                    !string.Equals(fence.PendingNodeId, localNodeId, StringComparison.OrdinalIgnoreCase) ||
                    fence.PendingEpoch != reference.Epoch ||
                    fence.PendingDigest is null)
                    return;

                // A process can stop after the shared fence records intent but
                // before the local policy transaction commits. If this node is
                // still the fenced writer, nobody has acknowledged the pending
                // version, and its durable store is byte-for-byte at the last
                // committed policy, the mutation never became durable. Clear
                // only that provably un-applied intent. Any acknowledged,
                // divergent, or otherwise ambiguous state remains fail-closed.
                if (!fence.AcknowledgedVersion.HasValue &&
                    MatchesCommittedPolicy(fence, snapshot, digest))
                {
                    var aborted = await _referenceStore.AbortAuthorityPolicyUpdateAsync(
                        localNodeId,
                        reference.LeaseId,
                        reference.Epoch,
                        pendingVersion,
                        fence.PendingDigest,
                        cancellationToken);
                    if (!aborted.Accepted)
                        _logger.LogWarning(
                            "A provably unapplied Authority policy update could not be aborted during recovery: {ReasonCode}.",
                            aborted.ReasonCode);
                    return;
                }

                if (snapshot.Version != pendingVersion ||
                    !DigestEquals(digest, fence.PendingDigest))
                    return;

                if (await SendSnapshotToPeerAsync(reference, snapshot, digest, cancellationToken))
                {
                    var commit = await _referenceStore.CommitAuthorityPolicyUpdateAsync(
                        localNodeId,
                        reference.LeaseId,
                        reference.Epoch,
                        pendingVersion,
                        digest,
                        cancellationToken);
                    if (!commit.Accepted)
                        _logger.LogWarning("Recovered Authority policy peer ACK could not commit its HA fence: {ReasonCode}.", commit.ReasonCode);
                }
                return;
            }

            if (fence is null || fence.CommittedDigest is null ||
                snapshot.Version != fence.CommittedVersion ||
                !DigestEquals(digest, fence.CommittedDigest))
            {
                _executionGate.InvalidateAll();
                return;
            }

            // Sending a committed exact snapshot from either node is a repair,
            // not a policy write: the receiver accepts only this fenced digest.
            await SendSnapshotToPeerAsync(reference, snapshot, digest, cancellationToken);
            var readiness = await CheckAsync(cancellationToken);
            if (!readiness.Allowed) _executionGate.InvalidateAll();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _executionGate.InvalidateAll();
            _logger.LogWarning(exception, "Stage 1 Authority HA synchronization failed closed.");
        }
    }

    private static bool MatchesCommittedPolicy(
        RuntimeHaAuthorityPolicyFence fence,
        AuthorityPolicySnapshot snapshot,
        string digest)
    {
        // A missing committed digest represents first-time fence bootstrap, not
        // a known-good prior state. In particular, version 0 with an empty
        // policy must still be replicated and committed instead of aborted.
        if (fence.CommittedDigest is null || snapshot.Version != fence.CommittedVersion)
            return false;
        return DigestEquals(digest, fence.CommittedDigest);
    }

    public async Task<IResult> ReceivePeerSnapshotAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!_highAvailability.Enabled || !_peerOptions.AuthenticationReady ||
            _peerOptions.PeerNodeId is null || _highAvailability.ClusterId is null)
            return Results.NotFound();

        var body = await ReadBoundedBodyAsync(request, _peerOptions.MaxPayloadBytes, cancellationToken);
        if (body is null)
            return Results.Json(new { error = "AUTHORITY_POLICY_PEER_PAYLOAD_INVALID" }, statusCode: StatusCodes.Status413PayloadTooLarge);

        var authentication = _authenticator.Verify(
            RuntimeHaPeerAuthenticator.FromRequest(request),
            request.Method,
            request.Path.Value ?? PeerReplicationPath,
            body,
            _peerOptions.PeerNodeId);
        if (!authentication.Accepted)
            return Results.Json(new { error = authentication.ReasonCode }, statusCode: StatusCodes.Status401Unauthorized);

        AuthorityPolicyPeerSnapshotEnvelope? envelope;
        try { envelope = JsonSerializer.Deserialize<AuthorityPolicyPeerSnapshotEnvelope>(body, WireJson); }
        catch (JsonException) { return Results.BadRequest(new { error = "AUTHORITY_POLICY_PEER_JSON_INVALID" }); }
        if (envelope is null ||
            envelope.Schema != AuthorityPolicyPeerSnapshotEnvelope.SchemaName ||
            envelope.SchemaVersion != AuthorityPolicyPeerSnapshotEnvelope.CurrentSchemaVersion ||
            !string.Equals(envelope.ClusterId, _highAvailability.ClusterId, StringComparison.OrdinalIgnoreCase) ||
            envelope.TopologyVersion != _highAvailability.Authority.Definition.TopologyVersion ||
            !string.Equals(envelope.SourceNodeId, _peerOptions.PeerNodeId, StringComparison.OrdinalIgnoreCase) ||
            envelope.SourceLeaseId == Guid.Empty || envelope.SourceEpoch < 1 ||
            Math.Abs((_timeProvider.GetUtcNow() - envelope.SentAtUtc).TotalSeconds) > _peerOptions.AuthenticationFreshness.TotalSeconds ||
            envelope.Snapshot is null || envelope.Snapshot.Version < 0 ||
            !IsDigest(envelope.Digest))
            return Results.BadRequest(new { error = "AUTHORITY_POLICY_PEER_CONTRACT_INVALID" });

        string computedDigest;
        try { computedDigest = AuthorityPolicySnapshotDigest.Compute(envelope.Snapshot); }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or InvalidOperationException)
        {
            return Results.BadRequest(new { error = "AUTHORITY_POLICY_PEER_SNAPSHOT_INVALID" });
        }
        if (!DigestEquals(computedDigest, envelope.Digest))
            return Results.Conflict(new { error = "AUTHORITY_POLICY_PEER_DIGEST_MISMATCH" });

        RuntimeHaReferenceAuthority? reference;
        try { reference = await _referenceStore.ReadAsync(cancellationToken); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _executionGate.InvalidateAll();
            return Results.Json(new { error = "AUTHORITY_POLICY_HA_REFERENCE_UNAVAILABLE" }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        if (reference?.AuthorityPolicy is not { } fence)
            return Results.Conflict(new { error = "AUTHORITY_POLICY_HA_REFERENCE_UNINITIALIZED" });

        var pending = fence.PendingVersion.HasValue;
        var acceptedByFence = pending
            ? fence.PendingVersion == envelope.Snapshot.Version &&
              DigestEquals(fence.PendingDigest, envelope.Digest) &&
              fence.PendingEpoch == envelope.SourceEpoch &&
              string.Equals(fence.PendingNodeId, envelope.SourceNodeId, StringComparison.OrdinalIgnoreCase) &&
              reference.Epoch == envelope.SourceEpoch &&
              reference.LeaseId == envelope.SourceLeaseId &&
              reference.LeaseUntilUtc > _timeProvider.GetUtcNow() &&
              string.Equals(reference.ActiveNodeId, envelope.SourceNodeId, StringComparison.OrdinalIgnoreCase)
            : fence.CommittedVersion == envelope.Snapshot.Version &&
              DigestEquals(fence.CommittedDigest, envelope.Digest);
        if (!acceptedByFence)
            return Results.Conflict(new { error = pending ? "AUTHORITY_POLICY_HA_PENDING_FENCE_MISMATCH" : "AUTHORITY_POLICY_HA_COMMITTED_FENCE_MISMATCH" });

        AuthorityPolicyReplicationApplyResult applied;
        try
        {
            applied = await _replicationStore.ApplyReplicatedSnapshotAsync(envelope.Snapshot, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _executionGate.InvalidateAll();
            _logger.LogWarning(exception, "Peer Authority policy snapshot could not be durably applied.");
            return Results.Json(new { error = "AUTHORITY_POLICY_REPLICA_STORE_UNAVAILABLE" }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        if (!applied.Applied || applied.Snapshot.Version != envelope.Snapshot.Version ||
            !DigestEquals(AuthorityPolicySnapshotDigest.Compute(applied.Snapshot), envelope.Digest))
            return Results.Conflict(new { error = applied.Error ?? "AUTHORITY_POLICY_REPLICA_NOT_APPLIED" });

        if (pending)
        {
            var acknowledgement = await _referenceStore.AcknowledgeAuthorityPolicyUpdateAsync(
                _peerOptions.LocalNodeId,
                envelope.SourceEpoch,
                envelope.Snapshot.Version,
                envelope.Digest,
                cancellationToken);
            if (!acknowledgement.Accepted)
            {
                _executionGate.InvalidateAll();
                return Results.Conflict(new { error = acknowledgement.ReasonCode });
            }
        }

        return Results.Ok(new AuthorityPolicyPeerSnapshotAck(
            true,
            "authority-policy-snapshot-applied",
            _peerOptions.LocalNodeId,
            applied.Snapshot.Version,
            envelope.Digest));
    }

    private async Task<bool> SendSnapshotToPeerAsync(
        RuntimeHaReferenceAuthority reference,
        AuthorityPolicySnapshot snapshot,
        string digest,
        CancellationToken cancellationToken)
    {
        if (!_peerOptions.AuthenticationReady || _peerOptions.PeerEndpoint is null ||
            _highAvailability.LocalNodeId is null)
            return false;

        var envelope = new AuthorityPolicyPeerSnapshotEnvelope(
            AuthorityPolicyPeerSnapshotEnvelope.SchemaName,
            AuthorityPolicyPeerSnapshotEnvelope.CurrentSchemaVersion,
            reference.ClusterId,
            reference.TopologyVersion,
            _highAvailability.LocalNodeId,
            reference.LeaseId,
            reference.Epoch,
            snapshot,
            digest,
            _timeProvider.GetUtcNow());
        var body = JsonSerializer.SerializeToUtf8Bytes(envelope, WireJson);
        if (body.Length > _peerOptions.MaxPayloadBytes)
        {
            _logger.LogWarning("Authority policy snapshot exceeds the HA peer payload limit.");
            return false;
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, PeerEndpoint())
        {
            Content = new ByteArrayContent(body)
        };
        message.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        RuntimeHaPeerAuthenticator.Apply(message, _authenticator.Sign(
            HttpMethod.Post.Method,
            PeerReplicationPath,
            body,
            envelope.SourceNodeId));

        try
        {
            using var response = await _httpClient.SendAsync(
                message,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
                return false;
            var responseBody = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (responseBody.Length > 16 * 1024)
                return false;
            var ack = JsonSerializer.Deserialize<AuthorityPolicyPeerSnapshotAck>(responseBody, WireJson);
            if (ack is not { Accepted: true } ||
                !string.Equals(ack.LocalNodeId, _peerOptions.PeerNodeId, StringComparison.OrdinalIgnoreCase) ||
                ack.AppliedVersion != snapshot.Version ||
                !DigestEquals(ack.AppliedDigest, digest))
                return false;

            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient's peer timeout is a replication-pending outcome, not a
            // caller cancellation. Keep the durable HA fence pending and let the
            // policy endpoint return its explicit 503 fail-closed response.
            _logger.LogWarning(
                "Authority policy snapshot to peer {PeerNodeId} timed out; version {PolicyVersion} remains pending.",
                _peerOptions.PeerNodeId,
                snapshot.Version);
            return false;
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                exception,
                "Authority policy snapshot to peer {PeerNodeId} failed; version {PolicyVersion} remains pending.",
                _peerOptions.PeerNodeId,
                snapshot.Version);
            return false;
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(
                exception,
                "Authority policy acknowledgement from peer {PeerNodeId} was malformed; version {PolicyVersion} remains pending.",
                _peerOptions.PeerNodeId,
                snapshot.Version);
            return false;
        }
        catch (IOException exception)
        {
            _logger.LogWarning(
                exception,
                "Authority policy acknowledgement body from peer {PeerNodeId} could not be read; version {PolicyVersion} remains pending.",
                _peerOptions.PeerNodeId,
                snapshot.Version);
            return false;
        }
    }

    private bool IsCurrentEffectiveActive(RuntimeHaReferenceAuthority reference) =>
        _highAvailability.CanOwnIndustrialEffects() &&
        _highAvailability.LocalNodeId is { } localNodeId &&
        string.Equals(reference.ActiveNodeId, localNodeId, StringComparison.OrdinalIgnoreCase) &&
        reference.Epoch == _highAvailability.Snapshot().AuthorityEpoch &&
        reference.LeaseUntilUtc > _timeProvider.GetUtcNow();

    private AuthorityPolicyHaReadiness Deny(string reason, AuthorityPolicySnapshot snapshot, long epoch = 0)
    {
        _executionGate.InvalidateAll();
        return new(false, reason, snapshot.Version, null, epoch);
    }

    private Uri PeerEndpoint() =>
        new UriBuilder(_peerOptions.PeerEndpoint!)
        {
            Path = PeerReplicationPath,
            Query = string.Empty,
            Fragment = string.Empty
        }.Uri;

    private static async Task<byte[]?> ReadBoundedBodyAsync(HttpRequest request, int maximumBytes, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream();
        var buffer = new byte[16 * 1024];
        while (true)
        {
            var read = await request.Body.ReadAsync(buffer, cancellationToken);
            if (read == 0) return stream.ToArray();
            if (stream.Length + read > maximumBytes) return null;
            await stream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private static JsonSerializerOptions CreateWireJson()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new SecurityCapabilityJsonConverter());
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }

    private static bool IsDigest(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static bool DigestEquals(string? left, string? right) =>
        left is not null && right is not null && CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(left),
            Convert.FromHexString(right));

    public void Dispose() => _httpClient.Dispose();
}

public sealed class AuthorityPolicyHaSynchronizationHostedService(
    AuthorityPolicyHaCoordinator coordinator,
    RuntimeHaPeerTransportOptions options,
    ILogger<AuthorityPolicyHaSynchronizationHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await coordinator.SynchronizeOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Authority policy HA synchronization pass failed closed.");
            }
            await Task.Delay(options.PollInterval, stoppingToken);
        }
    }
}

public static class AuthorityPolicyHaPeerEndpointComposition
{
    public static IEndpointRouteBuilder MapAuthorityPolicyHaPeerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
            AuthorityPolicyHaCoordinator.PeerReplicationPath,
            (HttpRequest request, AuthorityPolicyHaCoordinator coordinator, CancellationToken cancellationToken) =>
                coordinator.ReceivePeerSnapshotAsync(request, cancellationToken));
        return endpoints;
    }
}
