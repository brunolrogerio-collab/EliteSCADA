using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Scada.Api.Security;
using Scada.Core.Product.Licensing;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Security.Authorization;

namespace Scada.Api.Runtime;

public sealed record RuntimeHaPeerTransportOptions(
    bool Enabled,
    string LocalNodeId,
    string? PeerNodeId,
    Uri? PeerEndpoint,
    string? SharedSecret,
    int MaxPayloadBytes,
    TimeSpan RequestTimeout,
    TimeSpan PollInterval,
    TimeSpan MaximumRetryDelay,
    TimeSpan AuthenticationFreshness,
    TimeSpan PeerFreshnessWindow,
    string? StatePath)
{
    public const string ReplicationPath = "/api/runtime/ha/peer/replicate";

    public bool AuthenticationReady =>
        Enabled &&
        PeerNodeId is not null &&
        PeerEndpoint is not null &&
        !string.IsNullOrEmpty(SharedSecret) &&
        Encoding.UTF8.GetByteCount(SharedSecret) >= 32;

    public static RuntimeHaPeerTransportOptions FromConfiguration(
        IConfiguration configuration,
        RuntimeHaTopologyDefinition topology)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(topology);

        if (!topology.Enabled)
        {
            return new RuntimeHaPeerTransportOptions(
                false,
                topology.LocalNodeId,
                null,
                null,
                null,
                8 * 1024 * 1024,
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(15),
                TimeSpan.FromSeconds(15),
                topology.FreshnessWindow,
                null);
        }

        var section = configuration.GetSection("HighAvailability:PeerTransport");
        var enabled = section.GetValue<bool?>("Enabled") ?? true;
        var peer = topology.Nodes.Single(node =>
            !node.NodeId.Equals(topology.LocalNodeId, StringComparison.OrdinalIgnoreCase));

        Uri? peerEndpoint = null;
        var explicitEndpoint = section["PeerEndpoint"];
        if (!string.IsNullOrWhiteSpace(explicitEndpoint))
        {
            if (!Uri.TryCreate(explicitEndpoint.Trim(), UriKind.Absolute, out peerEndpoint) ||
                (peerEndpoint.Scheme != Uri.UriSchemeHttp &&
                 peerEndpoint.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException(
                    "HighAvailability:PeerTransport:PeerEndpoint must be an absolute HTTP(S) endpoint.");
            }

            if (!peerEndpoint.AbsolutePath.EndsWith(
                    ReplicationPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                peerEndpoint = new Uri(
                    new Uri(peerEndpoint.ToString().TrimEnd('/') + "/"),
                    ReplicationPath.TrimStart('/'));
            }
        }
        else
        {
            var address = peer.Endpoints
                .OrderBy(endpoint => endpoint.Priority)
                .Select(endpoint => endpoint.Address)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(address))
            {
                peerEndpoint = new Uri(
                    new Uri(address.TrimEnd('/') + "/"),
                    ReplicationPath.TrimStart('/'));
            }
        }

        var maxPayloadBytes = Math.Clamp(
            section.GetValue<int?>("MaxPayloadBytes") ?? 8 * 1024 * 1024,
            64 * 1024,
            16 * 1024 * 1024);
        var requestTimeoutSeconds = Math.Clamp(
            section.GetValue<int?>("RequestTimeoutSeconds") ?? 5,
            1,
            30);
        var pollMilliseconds = Math.Clamp(
            section.GetValue<int?>("PollMilliseconds") ?? 1000,
            250,
            10_000);
        var maximumRetrySeconds = Math.Clamp(
            section.GetValue<int?>("MaximumRetrySeconds") ?? 15,
            1,
            60);
        var authFreshnessSeconds = Math.Clamp(
            section.GetValue<int?>("AuthenticationFreshnessSeconds")
                ?? Math.Max(10, (int)Math.Ceiling(topology.FreshnessWindow.TotalSeconds)),
            5,
            120);

        var configuredStatePath = section["StatePath"];
        var statePath = string.IsNullOrWhiteSpace(configuredStatePath)
            ? Path.Combine(
                AppContext.BaseDirectory,
                "data",
                "ha",
                $"{SafeFileName(topology.LocalNodeId)}.peer-mirror.json")
            : Path.GetFullPath(configuredStatePath.Trim());

        return new RuntimeHaPeerTransportOptions(
            enabled,
            topology.LocalNodeId,
            peer.NodeId,
            peerEndpoint,
            section["SharedSecret"],
            maxPayloadBytes,
            TimeSpan.FromSeconds(requestTimeoutSeconds),
            TimeSpan.FromMilliseconds(pollMilliseconds),
            TimeSpan.FromSeconds(maximumRetrySeconds),
            TimeSpan.FromSeconds(authFreshnessSeconds),
            topology.FreshnessWindow,
            statePath);
    }

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        return new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }
}

public sealed record RuntimeHaPeerLicenseEvidence(
    bool LicenseValid,
    bool HaRuntimeEntitled,
    int? MaximumTags,
    int? InteractiveSeats,
    int? ViewOnlySeats)
{
    public static RuntimeHaPeerLicenseEvidence From(LicenseVerificationResult verification)
    {
        ArgumentNullException.ThrowIfNull(verification);
        var valid = verification.State == LicenseState.Valid &&
            verification.License is not null;
        return new RuntimeHaPeerLicenseEvidence(
            valid,
            valid && verification.SessionEntitlements?.HaRuntime == true,
            valid ? LicensingPolicy.MaximumTags(verification.License!.Tier) : 0,
            valid ? verification.SessionEntitlements?.InteractiveSeats : 0,
            valid ? verification.SessionEntitlements?.ViewOnlySeats : 0);
    }
}

public sealed record RuntimeHaMirroredTagValue(
    Guid TagId,
    JsonElement Value,
    DateTimeOffset Timestamp,
    TagQuality Quality,
    string? Source,
    DateTimeOffset? SourceTimestamp,
    DateTimeOffset? ServerTimestamp)
{
    public static RuntimeHaMirroredTagValue From(TagValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var projected = value.Value is null
            ? JsonSerializer.SerializeToElement<object?>(null)
            : JsonSerializer.SerializeToElement(value.Value, value.Value.GetType());
        return new RuntimeHaMirroredTagValue(
            value.TagId,
            projected,
            value.Timestamp,
            value.Quality,
            value.Source,
            value.SourceTimestamp,
            value.ServerTimestamp);
    }
}

public sealed record RuntimeHaAuthoritativeStateSnapshot(
    RuntimeHaRuntimeIdentity Runtime,
    int ProjectTagCount,
    RuntimeHaPeerLicenseEvidence License,
    IReadOnlyCollection<RuntimeHaMirroredTagValue> Tags,
    IReadOnlyCollection<RuntimeSessionLeaseContinuityEnvelope> Sessions,
    IReadOnlyCollection<RuntimeSessionLeaseContinuityTombstoneEnvelope> SessionTombstones,
    DateTimeOffset CapturedAtUtc,
    EngineeringPackage? Application = null,
    DateTimeOffset? RuntimeActivatedAtUtc = null);

public sealed record RuntimeHaPeerReplicationEnvelope(
    string Schema,
    int SchemaVersion,
    string ClusterId,
    long TopologyVersion,
    string SourceNodeId,
    Guid SourceTransportInstanceId,
    long ReplicationSequence,
    RuntimeHaPeerObservationEnvelope Observation,
    RuntimeHaAuthoritativeStateSnapshot? AuthoritativeState,
    DateTimeOffset SentAtUtc)
{
    public const string SchemaName = "elitescada.runtime-ha-peer-replication";
    public const int CurrentSchemaVersion = 1;
}

public sealed record RuntimeHaPeerReplicationAck(
    bool Accepted,
    string ReasonCode,
    string LocalNodeId,
    long AcceptedSequence,
    RuntimeHaTopologySnapshot Topology);

public sealed record RuntimeHaPeerAuthenticationHeaders(
    string NodeId,
    string Timestamp,
    string Nonce,
    string Signature);

public sealed record RuntimeHaPeerAuthenticationResult(
    bool Accepted,
    string ReasonCode);

public sealed class RuntimeHaPeerAuthenticator
{
    public const string NodeHeader = "X-EliteSCADA-HA-Node";
    public const string TimestampHeader = "X-EliteSCADA-HA-Timestamp";
    public const string NonceHeader = "X-EliteSCADA-HA-Nonce";
    public const string SignatureHeader = "X-EliteSCADA-HA-Signature";

    private readonly RuntimeHaPeerTransportOptions _options;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly object _gate = new();
    private readonly Dictionary<string, DateTimeOffset> _acceptedNonces =
        new(StringComparer.Ordinal);

    public RuntimeHaPeerAuthenticator(
        RuntimeHaPeerTransportOptions options,
        TimeProvider timeProvider)
        : this(options, () => timeProvider.GetUtcNow())
    {
    }

    internal RuntimeHaPeerAuthenticator(
        RuntimeHaPeerTransportOptions options,
        Func<DateTimeOffset> utcNow)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
    }

    public RuntimeHaPeerAuthenticationHeaders Sign(
        string method,
        string path,
        ReadOnlySpan<byte> body,
        string sourceNodeId)
    {
        if (!_options.AuthenticationReady || string.IsNullOrEmpty(_options.SharedSecret))
            throw new InvalidOperationException(
                "HA peer authentication is not configured with a sufficiently strong shared secret.");

        var timestamp = _utcNow().ToUnixTimeMilliseconds()
            .ToString(System.Globalization.CultureInfo.InvariantCulture);
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16))
            .ToLowerInvariant();
        var signature = ComputeSignature(
            method,
            path,
            timestamp,
            nonce,
            body,
            _options.SharedSecret);
        return new RuntimeHaPeerAuthenticationHeaders(
            sourceNodeId,
            timestamp,
            nonce,
            signature);
    }

    public RuntimeHaPeerAuthenticationResult Verify(
        RuntimeHaPeerAuthenticationHeaders headers,
        string method,
        string path,
        ReadOnlySpan<byte> body,
        string expectedPeerNodeId)
    {
        ArgumentNullException.ThrowIfNull(headers);
        if (!_options.AuthenticationReady || string.IsNullOrEmpty(_options.SharedSecret))
            return new RuntimeHaPeerAuthenticationResult(false, "peer-auth-not-configured");
        if (!headers.NodeId.Equals(expectedPeerNodeId, StringComparison.OrdinalIgnoreCase))
            return new RuntimeHaPeerAuthenticationResult(false, "peer-auth-node-mismatch");
        if (!long.TryParse(
                headers.Timestamp,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var unixMilliseconds))
        {
            return new RuntimeHaPeerAuthenticationResult(false, "peer-auth-timestamp-invalid");
        }

        DateTimeOffset timestamp;
        try
        {
            timestamp = DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return new RuntimeHaPeerAuthenticationResult(false, "peer-auth-timestamp-invalid");
        }

        var now = _utcNow();
        if ((now - timestamp).Duration() > _options.AuthenticationFreshness)
            return new RuntimeHaPeerAuthenticationResult(false, "peer-auth-timestamp-stale");
        if (headers.Nonce.Length is < 16 or > 128)
            return new RuntimeHaPeerAuthenticationResult(false, "peer-auth-nonce-invalid");

        byte[] suppliedSignature;
        try
        {
            suppliedSignature = Convert.FromBase64String(headers.Signature);
        }
        catch (FormatException)
        {
            return new RuntimeHaPeerAuthenticationResult(false, "peer-auth-signature-invalid");
        }

        var expectedSignature = Convert.FromBase64String(ComputeSignature(
            method,
            path,
            headers.Timestamp,
            headers.Nonce,
            body,
            _options.SharedSecret));
        if (suppliedSignature.Length != expectedSignature.Length ||
            !CryptographicOperations.FixedTimeEquals(
                suppliedSignature,
                expectedSignature))
        {
            return new RuntimeHaPeerAuthenticationResult(false, "peer-auth-signature-invalid");
        }

        var replayKey = string.Concat(
            expectedPeerNodeId.ToUpperInvariant(),
            "\n",
            headers.Nonce);
        lock (_gate)
        {
            var oldestAccepted = now - (_options.AuthenticationFreshness * 2);
            foreach (var expired in _acceptedNonces
                         .Where(pair => pair.Value < oldestAccepted)
                         .Select(pair => pair.Key)
                         .ToArray())
            {
                _acceptedNonces.Remove(expired);
            }

            if (_acceptedNonces.ContainsKey(replayKey))
                return new RuntimeHaPeerAuthenticationResult(false, "peer-auth-replay");
            _acceptedNonces[replayKey] = timestamp;
        }

        return new RuntimeHaPeerAuthenticationResult(true, "peer-authenticated");
    }

    public static RuntimeHaPeerAuthenticationHeaders FromRequest(HttpRequest request) =>
        new(
            request.Headers[NodeHeader].ToString(),
            request.Headers[TimestampHeader].ToString(),
            request.Headers[NonceHeader].ToString(),
            request.Headers[SignatureHeader].ToString());

    public static void Apply(
        HttpRequestMessage request,
        RuntimeHaPeerAuthenticationHeaders headers)
    {
        request.Headers.TryAddWithoutValidation(NodeHeader, headers.NodeId);
        request.Headers.TryAddWithoutValidation(TimestampHeader, headers.Timestamp);
        request.Headers.TryAddWithoutValidation(NonceHeader, headers.Nonce);
        request.Headers.TryAddWithoutValidation(SignatureHeader, headers.Signature);
    }

    private static string ComputeSignature(
        string method,
        string path,
        string timestamp,
        string nonce,
        ReadOnlySpan<byte> body,
        string sharedSecret)
    {
        var bodyHash = Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant();
        var canonical = string.Join(
            "\n",
            method.ToUpperInvariant(),
            path,
            timestamp,
            nonce,
            bodyHash);
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(sharedSecret));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical)));
    }
}

public sealed record RuntimeHaPeerMirrorSnapshot(
    bool HasState,
    bool LiveSynchronized,
    string? SourceNodeId,
    Guid? SourceTransportInstanceId,
    long? ReplicationSequence,
    RuntimeHaAuthoritativeStateSnapshot? AuthoritativeState,
    DateTimeOffset? ReceivedAtUtc,
    string? ReasonCode);

public sealed record RuntimeHaPeerMirrorApplyResult(
    bool Accepted,
    string ReasonCode,
    RuntimeHaPeerMirrorSnapshot Snapshot);

public sealed class RuntimeHaPeerMirrorStore
{
    private static readonly JsonSerializerOptions PersistenceJson =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = false
        };

    private readonly RuntimeHaPeerTransportOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RuntimeHaPeerMirrorStore> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private PersistedPeerMirror? _current;
    private bool _liveSynchronized;
    private string? _reasonCode;

    public RuntimeHaPeerMirrorStore(
        RuntimeHaPeerTransportOptions options,
        TimeProvider timeProvider,
        ILogger<RuntimeHaPeerMirrorStore> logger)
    {
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
        LoadPersisted();
    }

    public RuntimeHaPeerMirrorSnapshot Snapshot()
    {
        var current = _current;
        return current is null
            ? new RuntimeHaPeerMirrorSnapshot(
                false,
                false,
                null,
                null,
                null,
                null,
                null,
                _reasonCode)
            : new RuntimeHaPeerMirrorSnapshot(
                true,
                _liveSynchronized,
                current.SourceNodeId,
                current.SourceTransportInstanceId,
                current.ReplicationSequence,
                current.AuthoritativeState,
                current.ReceivedAtUtc,
                _reasonCode);
    }

    public async Task<RuntimeHaPeerMirrorApplyResult> ApplyAsync(
        RuntimeHaPeerReplicationEnvelope envelope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.AuthoritativeState is null)
        {
            return new RuntimeHaPeerMirrorApplyResult(
                false,
                "peer-authoritative-state-required",
                Snapshot());
        }

        var state = envelope.AuthoritativeState;
        if (!state.Runtime.CompatibleWith(envelope.Observation.Readiness.Runtime))
        {
            return new RuntimeHaPeerMirrorApplyResult(
                false,
                "peer-runtime-observation-mismatch",
                Snapshot());
        }
        if (state.License.HaRuntimeEntitled !=
            envelope.Observation.Readiness.HaLicenseEntitled)
        {
            return new RuntimeHaPeerMirrorApplyResult(
                false,
                "peer-license-observation-mismatch",
                Snapshot());
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_current is { } current &&
                current.SourceTransportInstanceId == envelope.SourceTransportInstanceId &&
                envelope.ReplicationSequence <= current.ReplicationSequence)
            {
                return new RuntimeHaPeerMirrorApplyResult(
                    false,
                    "peer-replication-stale",
                    Snapshot());
            }

            var receivedAtUtc = _timeProvider.GetUtcNow();
            var next = new PersistedPeerMirror(
                envelope.SourceNodeId,
                envelope.SourceTransportInstanceId,
                envelope.ReplicationSequence,
                state,
                receivedAtUtc);
            try
            {
                await PersistAsync(next, cancellationToken);
            }
            catch (Exception ex) when (
                ex is IOException or UnauthorizedAccessException or JsonException)
            {
                _liveSynchronized = false;
                _reasonCode = "durable-state-write-failed";
                _logger.LogError(
                    ex,
                    "HA peer mirror durable state could not be persisted.");
                return new RuntimeHaPeerMirrorApplyResult(
                    false,
                    _reasonCode,
                    Snapshot());
            }

            _current = next;
            _liveSynchronized = true;
            _reasonCode = "peer-state-synchronized";
            return new RuntimeHaPeerMirrorApplyResult(
                true,
                _reasonCode,
                Snapshot());
        }
        finally
        {
            _gate.Release();
        }
    }

    public void MarkStale(string reasonCode)
    {
        _liveSynchronized = false;
        _reasonCode = reasonCode;
    }

    private async Task PersistAsync(
        PersistedPeerMirror next,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.StatePath))
            return;

        var path = _options.StatePath;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var temporary = path + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(
                temporary,
                JsonSerializer.SerializeToUtf8Bytes(next, PersistenceJson),
                cancellationToken);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private void LoadPersisted()
    {
        if (string.IsNullOrWhiteSpace(_options.StatePath) ||
            !File.Exists(_options.StatePath))
        {
            return;
        }

        try
        {
            _current = JsonSerializer.Deserialize<PersistedPeerMirror>(
                File.ReadAllBytes(_options.StatePath),
                PersistenceJson);
            if (_current is not null)
            {
                _liveSynchronized = false;
                _reasonCode = "durable-state-loaded-awaiting-live-resync";
            }
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _current = null;
            _liveSynchronized = false;
            _reasonCode = "durable-state-load-failed";
            _logger.LogWarning(
                ex,
                "HA peer mirror durable state could not be loaded; readiness remains fail-closed.");
        }
    }

    private sealed record PersistedPeerMirror(
        string SourceNodeId,
        Guid SourceTransportInstanceId,
        long ReplicationSequence,
        RuntimeHaAuthoritativeStateSnapshot AuthoritativeState,
        DateTimeOffset ReceivedAtUtc);
}

public sealed record RuntimeHaPeerTransportDiagnostics(
    string ConnectionState,
    bool AuthenticationConfigured,
    string LocalNodeId,
    string? PeerNodeId,
    string? PeerEndpoint,
    Guid TransportInstanceId,
    long LastOutboundSequence,
    long? LastInboundSequence,
    DateTimeOffset? LastOutboundSuccessAtUtc,
    DateTimeOffset? LastInboundAtUtc,
    int ConsecutiveFailures,
    string? ReasonCode,
    RuntimeHaPeerMirrorSnapshot Mirror);

public sealed class RuntimeHaPeerTransportState
{
    private readonly object _gate = new();
    private readonly RuntimeHaPeerTransportOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly RuntimeHaPeerMirrorStore _mirror;
    private long _lastOutboundSequence;
    private long? _lastInboundSequence;
    private DateTimeOffset? _lastOutboundSuccessAtUtc;
    private DateTimeOffset? _lastInboundAtUtc;
    private int _consecutiveFailures;
    private string? _reasonCode;

    public RuntimeHaPeerTransportState(
        RuntimeHaPeerTransportOptions options,
        TimeProvider timeProvider,
        RuntimeHaPeerMirrorStore mirror)
    {
        _options = options;
        _timeProvider = timeProvider;
        _mirror = mirror;
        TransportInstanceId = Guid.NewGuid();
    }

    public Guid TransportInstanceId { get; }

    public void RecordOutbound(long sequence)
    {
        lock (_gate)
            _lastOutboundSequence = Math.Max(_lastOutboundSequence, sequence);
    }

    public void RecordOutboundSuccess(long sequence)
    {
        lock (_gate)
        {
            _lastOutboundSequence = Math.Max(_lastOutboundSequence, sequence);
            _lastOutboundSuccessAtUtc = _timeProvider.GetUtcNow();
            _consecutiveFailures = 0;
            _reasonCode = "peer-send-accepted";
        }
    }

    public void RecordInbound(long sequence)
    {
        lock (_gate)
        {
            _lastInboundSequence = sequence;
            _lastInboundAtUtc = _timeProvider.GetUtcNow();
            _reasonCode = "peer-replication-accepted";
        }
    }

    public void RecordFailure(string reasonCode)
    {
        lock (_gate)
        {
            _consecutiveFailures = checked(Math.Min(int.MaxValue, _consecutiveFailures + 1));
            _reasonCode = reasonCode;
        }
    }

    public DateTimeOffset? LastSuccessfulContact()
    {
        lock (_gate)
        {
            if (_lastOutboundSuccessAtUtc is null) return _lastInboundAtUtc;
            if (_lastInboundAtUtc is null) return _lastOutboundSuccessAtUtc;
            return _lastInboundAtUtc > _lastOutboundSuccessAtUtc
                ? _lastInboundAtUtc
                : _lastOutboundSuccessAtUtc;
        }
    }

    public RuntimeHaPeerTransportDiagnostics Snapshot()
    {
        lock (_gate)
        {
            var now = _timeProvider.GetUtcNow();
            var latest = LastSuccessfulContactLocked();
            string state;
            if (!_options.Enabled)
                state = "disabled";
            else if (!_options.AuthenticationReady)
                state = "incompatible";
            else if (latest is null)
                state = "unavailable";
            else if (now - latest.Value <= _options.PeerFreshnessWindow)
                state = "connected";
            else
                state = "stale";

            return new RuntimeHaPeerTransportDiagnostics(
                state,
                _options.AuthenticationReady,
                _options.LocalNodeId,
                _options.PeerNodeId,
                _options.PeerEndpoint?.ToString(),
                TransportInstanceId,
                _lastOutboundSequence,
                _lastInboundSequence,
                _lastOutboundSuccessAtUtc,
                _lastInboundAtUtc,
                _consecutiveFailures,
                _reasonCode,
                _mirror.Snapshot());
        }
    }

    private DateTimeOffset? LastSuccessfulContactLocked()
    {
        if (_lastOutboundSuccessAtUtc is null) return _lastInboundAtUtc;
        if (_lastInboundAtUtc is null) return _lastOutboundSuccessAtUtc;
        return _lastInboundAtUtc > _lastOutboundSuccessAtUtc
            ? _lastInboundAtUtc
            : _lastOutboundSuccessAtUtc;
    }
}

public sealed class RuntimeHaPeerReplicationCoordinator
{
    internal static readonly JsonSerializerOptions WireJson =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = false,
            WriteIndented = false
        };

    private readonly RuntimeHaPeerTransportOptions _options;
    private readonly RuntimeHaPeerAuthenticator _authenticator;
    private readonly RuntimeHaPeerMirrorStore _mirror;
    private readonly RuntimeHaPeerTransportState _transportState;
    private readonly RuntimeHighAvailabilityService _highAvailability;
    private readonly ScadaRuntimeFacade _runtime;
    private readonly HighAvailabilityRuntimeCoordinator _runtimeCoordinator;
    private readonly IRuntimeSessionLeaseStore _sessionLeaseStore;
    private readonly IProductLicenseService _licensing;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RuntimeHaPeerReplicationCoordinator> _logger;
    private long _replicationSequence;
    private string? _lastReadinessDiagnostic;
    private string? _lastPeerObservationDiagnostic;

    public RuntimeHaPeerReplicationCoordinator(
        RuntimeHaPeerTransportOptions options,
        RuntimeHaPeerAuthenticator authenticator,
        RuntimeHaPeerMirrorStore mirror,
        RuntimeHaPeerTransportState transportState,
        RuntimeHighAvailabilityService highAvailability,
        ScadaRuntimeFacade runtime,
        HighAvailabilityRuntimeCoordinator runtimeCoordinator,
        IRuntimeSessionLeaseStore sessionLeaseStore,
        IProductLicenseService licensing,
        TimeProvider timeProvider,
        ILogger<RuntimeHaPeerReplicationCoordinator> logger)
    {
        _options = options;
        _authenticator = authenticator;
        _mirror = mirror;
        _transportState = transportState;
        _highAvailability = highAvailability;
        _runtime = runtime;
        _runtimeCoordinator = runtimeCoordinator;
        _sessionLeaseStore = sessionLeaseStore;
        _licensing = licensing;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public RuntimeHaPeerTransportDiagnostics Diagnostics()
    {
        RefreshLocalReadiness();
        return _transportState.Snapshot();
    }

    public RuntimeHaPeerReplicationEnvelope CreateOutboundEnvelope()
    {
        if (!_highAvailability.Enabled ||
            _highAvailability.ClusterId is null ||
            _highAvailability.LocalNodeId is null)
        {
            throw new InvalidOperationException("HA peer replication requires enabled HA.");
        }

        RefreshLocalReadiness();
        var localNodeId = _highAvailability.LocalNodeId;
        var topology = _highAvailability.Snapshot();
        var observation = _highAvailability.CreatePeerObservation();
        RuntimeHaAuthoritativeStateSnapshot? authoritative = null;

        if (!topology.AmbiguousAuthority &&
            topology.PendingTransfer is null &&
            topology.EffectiveActiveNodeId is not null &&
            topology.EffectiveActiveNodeId.Equals(
                localNodeId,
                StringComparison.OrdinalIgnoreCase))
        {
            var descriptor = _runtime.Describe();
            var application = _runtimeCoordinator.CaptureApplication();
            if (descriptor.Revision.HasValue &&
                !string.IsNullOrWhiteSpace(descriptor.ProjectKey) &&
                application is not null &&
                _highAvailability.CanOwnIndustrialEffects())
            {
                var now = _timeProvider.GetUtcNow();
                var sessions = _highAvailability.SessionContinuity.Snapshot()
                    .Select(lease => lease with
                    {
                        SourceNodeId = localNodeId,
                        ReplicatedAtUtc = now
                    })
                    .ToArray();
                var tombstones = _highAvailability.SessionContinuity
                    .TombstoneSnapshot(_highAvailability.ClusterId)
                    .Select(tombstone => tombstone with
                    {
                        SourceNodeId = localNodeId
                    })
                    .ToArray();

                authoritative = new RuntimeHaAuthoritativeStateSnapshot(
                    RuntimeHaRuntimeIdentity.From(descriptor),
                    descriptor.TagCount,
                    RuntimeHaPeerLicenseEvidence.From(_licensing.CurrentVerification),
                    _runtime.CurrentValues()
                        .Select(RuntimeHaMirroredTagValue.From)
                        .ToArray(),
                    sessions,
                    tombstones,
                    now,
                    application,
                    descriptor.ActivatedAtUtc);
            }
        }

        var sequence = Interlocked.Increment(ref _replicationSequence);
        _transportState.RecordOutbound(sequence);
        return new RuntimeHaPeerReplicationEnvelope(
            RuntimeHaPeerReplicationEnvelope.SchemaName,
            RuntimeHaPeerReplicationEnvelope.CurrentSchemaVersion,
            _highAvailability.ClusterId,
            _highAvailability.Authority.Definition.TopologyVersion,
            localNodeId,
            _transportState.TransportInstanceId,
            sequence,
            observation,
            authoritative,
            _timeProvider.GetUtcNow());
    }

    public byte[] Serialize(RuntimeHaPeerReplicationEnvelope envelope)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(envelope, WireJson);
        if (body.Length > _options.MaxPayloadBytes)
            throw new InvalidOperationException("HA peer replication payload exceeds the configured bound.");
        return body;
    }

    public RuntimeHaPeerAuthenticationHeaders Sign(
        RuntimeHaPeerReplicationEnvelope envelope,
        ReadOnlySpan<byte> body) =>
        _authenticator.Sign(
            HttpMethod.Post.Method,
            RuntimeHaPeerTransportOptions.ReplicationPath,
            body,
            envelope.SourceNodeId);

    public async Task<IResult> ReceiveAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (!_highAvailability.Enabled)
            return Results.NotFound();
        if (!_options.AuthenticationReady ||
            _options.PeerNodeId is null ||
            _highAvailability.ClusterId is null)
        {
            _transportState.RecordFailure("peer-auth-not-configured");
            RefreshLocalReadiness();
            return Results.Json(
                new { error = "HA peer transport is not securely configured.", code = "peer-auth-not-configured" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var bodyResult = await ReadBoundedBodyAsync(
            request,
            _options.MaxPayloadBytes,
            cancellationToken);
        if (bodyResult.Body is null)
        {
            _transportState.RecordFailure(bodyResult.ReasonCode);
            return Results.Json(
                new { error = "HA peer payload is invalid.", code = bodyResult.ReasonCode },
                statusCode: bodyResult.StatusCode);
        }

        var auth = _authenticator.Verify(
            RuntimeHaPeerAuthenticator.FromRequest(request),
            request.Method,
            request.Path.Value ?? RuntimeHaPeerTransportOptions.ReplicationPath,
            bodyResult.Body,
            _options.PeerNodeId);
        if (!auth.Accepted)
        {
            _transportState.RecordFailure(auth.ReasonCode);
            return Results.Json(
                new { error = "HA peer authentication failed.", code = auth.ReasonCode },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        RuntimeHaPeerReplicationEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<RuntimeHaPeerReplicationEnvelope>(
                bodyResult.Body,
                WireJson);
        }
        catch (JsonException)
        {
            _transportState.RecordFailure("peer-payload-json-invalid");
            return Results.BadRequest(new
            {
                error = "HA peer replication payload is invalid.",
                code = "peer-payload-json-invalid"
            });
        }

        if (envelope is null)
            return Results.BadRequest(new { error = "HA peer replication payload is missing." });

        var envelopeFailure = ValidateEnvelope(envelope, _options.PeerNodeId);
        if (envelopeFailure is not null)
        {
            _transportState.RecordFailure(envelopeFailure);
            return Conflict(envelopeFailure, envelope.ReplicationSequence);
        }

        RuntimeHaPeerApplyResult observation;
        try
        {
            observation = _highAvailability.ApplyPeerObservation(envelope.Observation);
        }
        catch (KeyNotFoundException)
        {
            _transportState.RecordFailure("peer-source-node-unknown");
            return Conflict("peer-source-node-unknown", envelope.ReplicationSequence);
        }

        if (!observation.Accepted)
        {
            _transportState.RecordFailure(observation.ReasonCode);
            LogPeerObservation(envelope, observation);
            RefreshLocalReadiness();
            return Conflict(observation.ReasonCode, envelope.ReplicationSequence);
        }

        LogPeerObservation(envelope, observation);

        if (envelope.AuthoritativeState is not null)
        {
            if (envelope.Observation.AmbiguousAuthority ||
                envelope.Observation.EffectiveActiveNodeId is null ||
                !envelope.Observation.EffectiveActiveNodeId.Equals(
                    envelope.SourceNodeId,
                    StringComparison.OrdinalIgnoreCase))
            {
                _transportState.RecordFailure("peer-authoritative-state-without-active-authority");
                return Conflict(
                    "peer-authoritative-state-without-active-authority",
                    envelope.ReplicationSequence);
            }

            var mirror = await _mirror.ApplyAsync(envelope, cancellationToken);
            if (!mirror.Accepted)
            {
                _transportState.RecordFailure(mirror.ReasonCode);
                RefreshLocalReadiness();
                return Conflict(mirror.ReasonCode, envelope.ReplicationSequence);
            }

            var authoritativeState = envelope.AuthoritativeState;
            if (authoritativeState.Application is null ||
                !authoritativeState.Runtime.Revision.HasValue ||
                string.IsNullOrWhiteSpace(authoritativeState.Runtime.ProjectKey))
            {
                _transportState.RecordFailure("peer-application-required");
                RefreshLocalReadiness();
                return Conflict("peer-application-required", envelope.ReplicationSequence);
            }

            var localLicense = RuntimeHaPeerLicenseEvidence.From(
                _licensing.CurrentVerification);
            if (localLicense.LicenseValid && localLicense.HaRuntimeEntitled)
            {
                var materialized = await _runtimeCoordinator.MaterializePassiveAsync(
                    envelope.SourceNodeId,
                    authoritativeState.Runtime.ProjectKey,
                    authoritativeState.Runtime.Revision.Value,
                    authoritativeState.Application,
                    authoritativeState.RuntimeActivatedAtUtc,
                    cancellationToken);
                if (!materialized.Activated)
                {
                    var reason = materialized.RuntimeIssues
                        .FirstOrDefault(issue => issue.IsError)?.Code
                        ?? "passive-runtime-materialization-failed";
                    _transportState.RecordFailure(reason);
                    RefreshLocalReadiness();
                    return Conflict(reason, envelope.ReplicationSequence);
                }

                var sessionFailure = await ApplySessionStateAsync(
                    envelope.SourceNodeId,
                    authoritativeState,
                    cancellationToken);
                if (sessionFailure is not null)
                {
                    _transportState.RecordFailure(sessionFailure);
                    RefreshLocalReadiness();
                    return Conflict(sessionFailure, envelope.ReplicationSequence);
                }
            }
        }

        _transportState.RecordInbound(envelope.ReplicationSequence);
        RefreshLocalReadiness();

        return Results.Ok(new RuntimeHaPeerReplicationAck(
            true,
            "peer-replication-applied",
            _highAvailability.LocalNodeId!,
            envelope.ReplicationSequence,
            _highAvailability.Snapshot()));
    }

    public void RecordOutboundSuccess(long sequence) =>
        _transportState.RecordOutboundSuccess(sequence);

    public void RecordTransportFailure(string reasonCode)
    {
        _transportState.RecordFailure(reasonCode);
        var lastContact = _transportState.LastSuccessfulContact();
        if (lastContact is null ||
            _timeProvider.GetUtcNow() - lastContact.Value >
            _options.PeerFreshnessWindow)
        {
            _mirror.MarkStale("peer-link-stale");
        }
        RefreshLocalReadiness();
    }

    private void LogPeerObservation(
        RuntimeHaPeerReplicationEnvelope envelope,
        RuntimeHaPeerApplyResult result)
    {
        var peer = result.Snapshot.Nodes.FirstOrDefault(node =>
            node.NodeId.Equals(
                envelope.SourceNodeId,
                StringComparison.OrdinalIgnoreCase));
        var readiness = envelope.Observation.Readiness;
        var diagnostic = string.Join(
            '|',
            result.Accepted,
            result.ReasonCode,
            readiness.Healthy,
            readiness.SynchronizationComplete,
            readiness.HaLicenseEntitled,
            readiness.Runtime.Mode,
            readiness.Runtime.Revision,
            peer?.State,
            peer?.ReadinessReason,
            result.Snapshot.EffectiveActiveNodeId,
            result.Snapshot.AmbiguousAuthority);
        var previous = Interlocked.Exchange(
            ref _lastPeerObservationDiagnostic,
            diagnostic);
        if (string.Equals(previous, diagnostic, StringComparison.Ordinal))
            return;

        _logger.LogWarning(
            "HA node {LocalNodeId} peer observation from {PeerNodeId}: accepted={Accepted}, " +
            "reason={ReasonCode}, peerSync={PeerSynchronizationComplete}, peerHealthy={PeerHealthy}, " +
            "peerHaLicensed={PeerHaLicensed}, runtimeMode={RuntimeMode}, revision={RuntimeRevision}, " +
            "projectedState={ProjectedState}, readinessReason={ReadinessReason}, " +
            "effectiveActive={EffectiveActiveNodeId}, ambiguous={AmbiguousAuthority}.",
            _highAvailability.LocalNodeId,
            envelope.SourceNodeId,
            result.Accepted,
            result.ReasonCode,
            readiness.SynchronizationComplete,
            readiness.Healthy,
            readiness.HaLicenseEntitled,
            readiness.Runtime.Mode,
            readiness.Runtime.Revision,
            peer?.State,
            peer?.ReadinessReason,
            result.Snapshot.EffectiveActiveNodeId,
            result.Snapshot.AmbiguousAuthority);
    }

    public void RefreshLocalReadiness()
    {
        if (!_highAvailability.Enabled ||
            _highAvailability.LocalNodeId is null)
        {
            return;
        }

        var snapshot = _highAvailability.Snapshot();
        if (snapshot.EffectiveActiveNodeId is not null &&
            snapshot.EffectiveActiveNodeId.Equals(
                _highAvailability.LocalNodeId,
                StringComparison.OrdinalIgnoreCase))
        {
            _highAvailability.RefreshLocalReadiness(
                _runtime.Describe(),
                _licensing.CurrentVerification);
            return;
        }

        var now = _timeProvider.GetUtcNow();
        var localLicense = RuntimeHaPeerLicenseEvidence.From(
            _licensing.CurrentVerification);
        var mirror = _mirror.Snapshot();
        var (synchronized, reasonCode) = EvaluateStandbyReadiness(
            mirror,
            localLicense,
            now);
        var diagnostic = synchronized ? "ready" : reasonCode ?? "synchronization-incomplete";
        var previousDiagnostic = Interlocked.Exchange(
            ref _lastReadinessDiagnostic,
            diagnostic);
        if (!string.Equals(previousDiagnostic, diagnostic, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "HA node {NodeId} standby readiness changed: {ReasonCode}.",
                _highAvailability.LocalNodeId,
                diagnostic);
        }

        var localDescriptor = _runtime.Describe();
        var runtimeIdentity =
            localDescriptor.Revision.HasValue &&
            !string.IsNullOrWhiteSpace(localDescriptor.ProjectKey)
                ? RuntimeHaRuntimeIdentity.From(localDescriptor)
                : new RuntimeHaRuntimeIdentity("unknown", null, null);

        _highAvailability.ObserveLocalReadiness(
            new RuntimeHaNodeReadinessEvidence(
                Healthy: true,
                SynchronizationComplete: synchronized,
                HaLicenseEntitled:
                    localLicense.LicenseValid &&
                    localLicense.HaRuntimeEntitled,
                Runtime: runtimeIdentity,
                ObservedAtUtc: now,
                Diagnostic: reasonCode));
    }

    private (bool Synchronized, string? ReasonCode) EvaluateStandbyReadiness(
        RuntimeHaPeerMirrorSnapshot mirror,
        RuntimeHaPeerLicenseEvidence localLicense,
        DateTimeOffset now)
    {
        if (!_options.AuthenticationReady)
            return (false, "peer-auth-not-configured");
        if (!localLicense.LicenseValid)
            return (false, "local-license-invalid");
        if (!localLicense.HaRuntimeEntitled)
            return (false, "local-license-not-ha-entitled");
        if (!mirror.HasState || mirror.AuthoritativeState is null)
            return (false, mirror.ReasonCode ?? "peer-state-not-replicated");
        if (!mirror.LiveSynchronized)
            return (false, mirror.ReasonCode ?? "peer-state-not-live");
        if (mirror.ReceivedAtUtc is null ||
            now - mirror.ReceivedAtUtc.Value > _options.PeerFreshnessWindow)
        {
            return (false, "peer-state-stale");
        }

        var state = mirror.AuthoritativeState;
        var localRuntime = _runtime.Describe();
        if (!localRuntime.Revision.HasValue ||
            string.IsNullOrWhiteSpace(localRuntime.ProjectKey))
        {
            return (false, "passive-runtime-not-materialized");
        }
        if (!RuntimeHaRuntimeIdentity.From(localRuntime).CompatibleWith(state.Runtime))
            return (false, "passive-runtime-not-compatible");
        if (state.RuntimeActivatedAtUtc is null ||
            localRuntime.ActivatedAtUtc is null ||
            localRuntime.ActivatedAtUtc.Value.ToUniversalTime() !=
                state.RuntimeActivatedAtUtc.Value.ToUniversalTime())
        {
            return (false, "passive-runtime-activation-identity-mismatch");
        }
        if (state.Application is null)
            return (false, "peer-application-unavailable");
        if (state.Application.Tags.Count != state.ProjectTagCount)
            return (false, "peer-application-tag-count-mismatch");

        if (!state.License.LicenseValid)
            return (false, "peer-license-invalid");
        if (!state.License.HaRuntimeEntitled)
            return (false, "peer-license-not-ha-entitled");
        if (!state.Runtime.Revision.HasValue ||
            string.IsNullOrWhiteSpace(state.Runtime.ProjectKey))
        {
            return (false, "peer-runtime-unavailable");
        }
        if (localLicense.MaximumTags is { } localTagLimit &&
            state.ProjectTagCount > localTagLimit)
        {
            return (false, "local-tag-capacity-incompatible");
        }
        if (localLicense.InteractiveSeats is null ||
            localLicense.ViewOnlySeats is null)
        {
            return (false, "local-session-capacity-unavailable");
        }

        var interactive = state.Sessions.Count(lease =>
            lease.ConnectionClass == RuntimeConnectionClass.Interactive);
        var viewOnly = state.Sessions.Count(lease =>
            lease.ConnectionClass != RuntimeConnectionClass.Interactive);
        if (interactive > localLicense.InteractiveSeats.Value)
            return (false, "local-interactive-capacity-incompatible");
        if (viewOnly > localLicense.ViewOnlySeats.Value)
            return (false, "local-view-only-capacity-incompatible");

        return (true, null);
    }

    private async Task<string?> ApplySessionStateAsync(
        string sourceNodeId,
        RuntimeHaAuthoritativeStateSnapshot state,
        CancellationToken cancellationToken)
    {
        var clusterId = _highAvailability.ClusterId!;
        var authority = await _sessionLeaseStore.GetAuthorityStateAsync(cancellationToken);
        if (authority.TransitionPending)
            return "authority-transition-pending";

        foreach (var tombstone in state.SessionTombstones)
        {
            var mirrored = _highAvailability.SessionContinuity.ApplyPeerTombstone(
                tombstone,
                clusterId,
                sourceNodeId);
            if (!mirrored.Accepted &&
                mirrored.ReasonCode != "session-tombstone-not-newer")
            {
                return mirrored.ReasonCode;
            }

            var canonical = await _sessionLeaseStore.ApplyReplicatedTombstoneAsync(
                new RuntimeSessionLeaseTombstoneState(
                    tombstone.ClusterId,
                    tombstone.UserId,
                    tombstone.ClientInstanceId,
                    tombstone.SessionId,
                    tombstone.Generation,
                    tombstone.AuthorityRevision,
                    tombstone.IssuedAtUtc,
                    tombstone.SourceNodeId,
                    tombstone.RecordedAtUtc,
                    tombstone.ReasonCode),
                clusterId,
                sourceNodeId,
                cancellationToken);
            if (!canonical.Accepted)
                return canonical.ReasonCode;
        }

        var expectedRuntime = new RuntimeSessionRuntimeIdentity(
            state.Runtime.Mode,
            state.Runtime.ProjectKey,
            state.Runtime.Revision,
            state.RuntimeActivatedAtUtc);

        foreach (var lease in state.Sessions)
        {
            var mirrored = _highAvailability.ApplyPeerSessionContinuity(lease);
            if (!mirrored.Accepted &&
                mirrored.ReasonCode != "session-state-not-newer")
            {
                return mirrored.ReasonCode;
            }

            var canonical = await _sessionLeaseStore.AdoptReplicatedAsync(
                new RuntimeSessionLeaseState(
                    lease.SessionId,
                    lease.UserId,
                    lease.ClientInstanceId,
                    lease.ConnectionClass == RuntimeConnectionClass.Interactive
                        ? "interactive"
                        : "viewer",
                    lease.Generation,
                    lease.IssuedAtUtc,
                    lease.LastHeartbeatUtc,
                    lease.ExpiresAtUtc,
                    new RuntimeSessionRuntimeIdentity(
                        lease.Runtime.Mode,
                        lease.Runtime.ProjectKey,
                        lease.Runtime.Revision,
                        lease.RuntimeActivatedAtUtc),
                    sourceNodeId,
                    clusterId,
                    IsActive: true,
                    lease.AuthorityRevision),
                clusterId,
                sourceNodeId,
                expectedRuntime,
                authority.AuthorityRevision,
                cancellationToken);
            if (!canonical.Accepted)
                return canonical.ReasonCode;
        }

        return null;
    }

    private string? ValidateEnvelope(
        RuntimeHaPeerReplicationEnvelope envelope,
        string expectedPeerNodeId)
    {
        if (!string.Equals(
                envelope.Schema,
                RuntimeHaPeerReplicationEnvelope.SchemaName,
                StringComparison.Ordinal) ||
            envelope.SchemaVersion !=
                RuntimeHaPeerReplicationEnvelope.CurrentSchemaVersion)
        {
            return "peer-replication-schema-mismatch";
        }
        if (!_highAvailability.ClusterId!.Equals(
                envelope.ClusterId,
                StringComparison.OrdinalIgnoreCase))
        {
            return "peer-replication-cluster-mismatch";
        }
        if (_highAvailability.Authority.Definition.TopologyVersion !=
            envelope.TopologyVersion)
        {
            return "peer-replication-topology-mismatch";
        }
        if (!envelope.SourceNodeId.Equals(
                expectedPeerNodeId,
                StringComparison.OrdinalIgnoreCase))
        {
            return "peer-replication-source-mismatch";
        }
        if (!envelope.Observation.SourceNodeId.Equals(
                envelope.SourceNodeId,
                StringComparison.OrdinalIgnoreCase) ||
            !envelope.Observation.ClusterId.Equals(
                envelope.ClusterId,
                StringComparison.OrdinalIgnoreCase) ||
            envelope.Observation.TopologyVersion != envelope.TopologyVersion)
        {
            return "peer-replication-observation-identity-mismatch";
        }
        if (envelope.ReplicationSequence < 1)
            return "peer-replication-sequence-invalid";
        if ((_timeProvider.GetUtcNow() - envelope.SentAtUtc).Duration() >
            _options.PeerFreshnessWindow)
        {
            return "peer-replication-stale";
        }

        return null;
    }

    private IResult Conflict(string reasonCode, long sequence) =>
        Results.Json(
            new RuntimeHaPeerReplicationAck(
                false,
                reasonCode,
                _highAvailability.LocalNodeId ?? _options.LocalNodeId,
                sequence,
                _highAvailability.Snapshot()),
            statusCode: StatusCodes.Status409Conflict);

    private static async Task<BoundedBodyResult> ReadBoundedBodyAsync(
        HttpRequest request,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength is > 0 &&
            request.ContentLength > maximumBytes)
        {
            return new BoundedBodyResult(
                null,
                "peer-payload-too-large",
                StatusCodes.Status413PayloadTooLarge);
        }

        await using var buffer = new MemoryStream(
            request.ContentLength is > 0
                ? (int)Math.Min(request.ContentLength.Value, maximumBytes)
                : 0);
        var chunk = new byte[16 * 1024];
        while (true)
        {
            var read = await request.Body.ReadAsync(
                chunk.AsMemory(0, chunk.Length),
                cancellationToken);
            if (read == 0) break;
            if (buffer.Length + read > maximumBytes)
            {
                return new BoundedBodyResult(
                    null,
                    "peer-payload-too-large",
                    StatusCodes.Status413PayloadTooLarge);
            }
            await buffer.WriteAsync(
                chunk.AsMemory(0, read),
                cancellationToken);
        }

        if (buffer.Length == 0)
        {
            return new BoundedBodyResult(
                null,
                "peer-payload-empty",
                StatusCodes.Status400BadRequest);
        }

        return new BoundedBodyResult(
            buffer.ToArray(),
            "peer-payload-read",
            StatusCodes.Status200OK);
    }

    private sealed record BoundedBodyResult(
        byte[]? Body,
        string ReasonCode,
        int StatusCode);
}

public sealed class RuntimeHaPeerTransportHostedService : BackgroundService
{
    private readonly RuntimeHaPeerTransportOptions _options;
    private readonly RuntimeHaPeerReplicationCoordinator _coordinator;
    private readonly RuntimeHaPeerAuthenticator _authenticator;
    private readonly ILogger<RuntimeHaPeerTransportHostedService> _logger;
    private readonly HttpClient _client;

    public RuntimeHaPeerTransportHostedService(
        RuntimeHaPeerTransportOptions options,
        RuntimeHaPeerReplicationCoordinator coordinator,
        RuntimeHaPeerAuthenticator authenticator,
        ILogger<RuntimeHaPeerTransportHostedService> logger)
    {
        _options = options;
        _coordinator = coordinator;
        _authenticator = authenticator;
        _logger = logger;
        _client = new HttpClient
        {
            Timeout = _options.RequestTimeout
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
            return;

        var retryDelay = _options.PollInterval;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_options.AuthenticationReady ||
                    _options.PeerEndpoint is null)
                {
                    _coordinator.RecordTransportFailure("peer-auth-not-configured");
                    retryDelay = NextRetry(retryDelay);
                }
                else
                {
                    var envelope = _coordinator.CreateOutboundEnvelope();
                    var body = _coordinator.Serialize(envelope);
                    using var request = new HttpRequestMessage(
                        HttpMethod.Post,
                        _options.PeerEndpoint)
                    {
                        Content = new ByteArrayContent(body)
                    };
                    request.Content.Headers.ContentType =
                        new System.Net.Http.Headers.MediaTypeHeaderValue(
                            "application/json");
                    RuntimeHaPeerAuthenticator.Apply(
                        request,
                        _authenticator.Sign(
                            HttpMethod.Post.Method,
                            RuntimeHaPeerTransportOptions.ReplicationPath,
                            body,
                            envelope.SourceNodeId));

                    using var response = await _client.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        stoppingToken);
                    if (!response.IsSuccessStatusCode)
                    {
                        var reason = $"peer-http-{(int)response.StatusCode}";
                        _coordinator.RecordTransportFailure(reason);
                        retryDelay = NextRetry(retryDelay);
                    }
                    else
                    {
                        _coordinator.RecordOutboundSuccess(
                            envelope.ReplicationSequence);
                        retryDelay = _options.PollInterval;
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (
                ex is HttpRequestException or
                TaskCanceledException or
                InvalidOperationException or
                JsonException)
            {
                _coordinator.RecordTransportFailure(
                    ex is TaskCanceledException
                        ? "peer-request-timeout"
                        : "peer-transport-failure");
                _logger.LogWarning(
                    ex,
                    "HA peer replication attempt failed.");
                retryDelay = NextRetry(retryDelay);
            }

            await Task.Delay(retryDelay, stoppingToken);
        }
    }

    public override void Dispose()
    {
        _client.Dispose();
        base.Dispose();
    }

    private TimeSpan NextRetry(TimeSpan current)
    {
        var doubled = TimeSpan.FromMilliseconds(
            Math.Max(
                _options.PollInterval.TotalMilliseconds,
                current.TotalMilliseconds * 2));
        return doubled <= _options.MaximumRetryDelay
            ? doubled
            : _options.MaximumRetryDelay;
    }
}

public static class RuntimeHighAvailabilityPeerTransportComposition
{
    public static IServiceCollection AddRuntimeHighAvailabilityPeerTransport(
        this IServiceCollection services)
    {
        services.AddSingleton(sp =>
            sp.GetRequiredService<RuntimeHaHostConfigurationAuthority>()
                .CreatePeerTransportOptions(
                    sp.GetRequiredService<RuntimeHighAvailabilityService>()
                        .Authority.Definition));
        services.AddSingleton<RuntimeHaPeerAuthenticator>();
        services.AddSingleton<RuntimeHaPeerMirrorStore>();
        services.AddSingleton<RuntimeHaPeerTransportState>();
        services.AddSingleton<RuntimeHaPeerReplicationCoordinator>();
        services.AddHostedService<RuntimeHaPeerTransportHostedService>();
        return services;
    }

    public static IEndpointRouteBuilder MapRuntimeHighAvailabilityPeerTransportEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
            RuntimeHaPeerTransportOptions.ReplicationPath,
            async (
                HttpRequest request,
                RuntimeHaPeerReplicationCoordinator coordinator,
                CancellationToken cancellationToken) =>
                await coordinator.ReceiveAsync(request, cancellationToken));

        endpoints.MapGet("/api/runtime/ha/peer/status", async (
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            RuntimeHaPeerReplicationCoordinator coordinator,
            CancellationToken cancellationToken) =>
        {
            var authorization = await security.CheckRuntimeAsync(
                context,
                runtime,
                Scada.Security.Authorization.SecurityCapability.HighAvailabilityObserve,
                cancellationToken: cancellationToken);
            var failure = authorization.FailureResult();
            return failure ?? Results.Ok(coordinator.Diagnostics());
        });

        return endpoints;
    }
}
