using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Scada.Api.Runtime;

public sealed record RuntimeHaHostNodeConfiguration(
    string NodeId,
    string? LocalEndpoint,
    string? RemoteEndpoint);

public sealed record RuntimeHaHostPeerTransportConfiguration(
    bool Enabled,
    string? PeerEndpoint,
    string? SharedSecret);

public sealed record RuntimeHaHostProtectionConfiguration(
    bool Enabled,
    bool AutomaticFailoverEnabled,
    string ReferenceStoreMode,
    string? ReferencePath,
    int LeaseSeconds,
    int PollMilliseconds,
    int ReadyWitnessMaximumAgeSeconds,
    int ClockSkewSafetyMarginSeconds);

public sealed record RuntimeHaHostConfigurationDocument(
    string Schema,
    int SchemaVersion,
    long Generation,
    DateTimeOffset UpdatedAtUtc,
    bool Enabled,
    string? ClusterId,
    string LocalNodeId,
    string? InitialActiveNodeId,
    long TopologyVersion,
    int FreshnessSeconds,
    IReadOnlyCollection<RuntimeHaHostNodeConfiguration> Nodes,
    RuntimeHaHostPeerTransportConfiguration PeerTransport,
    RuntimeHaHostProtectionConfiguration Protection)
{
    public const string SchemaName = "elitescada.runtime-ha-host-configuration";
    public const int CurrentSchemaVersion = 1;
}

public sealed record RuntimeHaHostPeerTransportUpdate(
    bool Enabled,
    string? PeerEndpoint,
    string? PeerSharedSecret = null,
    bool ClearPeerSharedSecret = false);

public sealed record RuntimeHaHostConfigurationUpdateRequest(
    long ExpectedGeneration,
    bool Enabled,
    string? ClusterId,
    string LocalNodeId,
    string? InitialActiveNodeId,
    long TopologyVersion,
    int FreshnessSeconds,
    IReadOnlyCollection<RuntimeHaHostNodeConfiguration>? Nodes,
    RuntimeHaHostPeerTransportUpdate? PeerTransport,
    RuntimeHaHostProtectionConfiguration? Protection);

public sealed record RuntimeHaHostPeerTransportView(
    bool Enabled,
    string? PeerEndpoint,
    bool AuthenticationConfigured);

public sealed record RuntimeHaHostConfigurationView(
    bool Enabled,
    string? ClusterId,
    string LocalNodeId,
    string? InitialActiveNodeId,
    long TopologyVersion,
    int FreshnessSeconds,
    IReadOnlyCollection<RuntimeHaHostNodeConfiguration> Nodes,
    RuntimeHaHostPeerTransportView PeerTransport,
    RuntimeHaHostProtectionConfiguration Protection);

public sealed record RuntimeHaReferenceStoreOperationalRequirements(
    string Mode,
    bool FailClosedWhenUnavailable,
    string Summary,
    IReadOnlyCollection<string> RequiredSemantics);

public sealed record RuntimeHaHostConfigurationSnapshot(
    string Schema,
    int SchemaVersion,
    long Generation,
    DateTimeOffset UpdatedAtUtc,
    bool PendingRestart,
    string ApplyMode,
    bool IndustrialEffectsBlocked,
    RuntimeHaHostConfigurationView Running,
    RuntimeHaHostConfigurationView Desired,
    RuntimeHaReferenceStoreOperationalRequirements ReferenceStoreRequirements);

public sealed record RuntimeHaHostConfigurationUpdateResult(
    bool Accepted,
    string ReasonCode,
    RuntimeHaHostConfigurationSnapshot Snapshot,
    IReadOnlyCollection<string> Errors);

/// <summary>
/// Deployment/host-owned HA configuration authority. It is intentionally independent from
/// Engineering Workspace and .escadapkg state. Administrative changes are persisted atomically,
/// but are never applied as an in-process authority transition: a changed configuration requires
/// restart/rebind and the running node is fenced from industrial effects while that restart is
/// pending.
/// </summary>
public sealed class RuntimeHaHostConfigurationAuthority
{
    public const string ReferenceStoreModeSharedExternalFile = "shared-external-file";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly RuntimeHaReferenceStoreOperationalRequirements ReferenceRequirements =
        new(
            ReferenceStoreModeSharedExternalFile,
            FailClosedWhenUnavailable: true,
            Summary:
                "The HA reference path is a deployment fencing authority, not ordinary local application data. " +
                "Both HA nodes must resolve it to the same external/shared storage domain.",
            RequiredSemantics: new[]
            {
                "single shared/external storage domain visible to both HA nodes",
                "cross-host exclusive locking semantics for the reference lock",
                "atomic replace/rename semantics for reference updates",
                "durable read-after-write visibility across both nodes",
                "loss, invalid data, or incompatible topology must fail closed"
            });

    private readonly IConfiguration _bootstrapConfiguration;
    private readonly string _path;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly object _stateGate = new();
    private readonly RuntimeHaHostConfigurationDocument _running;
    private RuntimeHaHostConfigurationDocument _desired;
    private bool _pendingRestart;

    public RuntimeHaHostConfigurationAuthority(IConfiguration configuration)
    {
        _bootstrapConfiguration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _path = ResolveConfigurationPath(configuration);

        var persisted = File.Exists(_path);
        var running = persisted
            ? ReadPersisted(_path)
            : FromBootstrapConfiguration(configuration);

        ValidateDocument(
            running,
            enforceAdministrativeBounds: persisted,
            configurationPath: _path);
        _running = running;
        _desired = running;
    }

    public RuntimeHaTopologyDefinition RunningTopology => ToTopology(_running);

    public bool PendingRestart
    {
        get
        {
            lock (_stateGate)
                return _pendingRestart;
        }
    }

    public bool AllowsIndustrialEffects
    {
        get
        {
            lock (_stateGate)
                return !_pendingRestart;
        }
    }

    public RuntimeHaProtectionOptions CreateProtectionOptions()
    {
        var protection = _running.Protection;
        return new RuntimeHaProtectionOptions(
            protection.Enabled,
            protection.AutomaticFailoverEnabled,
            string.IsNullOrWhiteSpace(protection.ReferencePath)
                ? null
                : Path.GetFullPath(protection.ReferencePath),
            TimeSpan.FromSeconds(protection.LeaseSeconds),
            TimeSpan.FromMilliseconds(protection.PollMilliseconds),
            TimeSpan.FromSeconds(protection.ReadyWitnessMaximumAgeSeconds),
            TimeSpan.FromSeconds(protection.ClockSkewSafetyMarginSeconds));
    }

    public RuntimeHaPeerTransportOptions CreatePeerTransportOptions(
        RuntimeHaTopologyDefinition topology)
    {
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

        var section = _bootstrapConfiguration.GetSection("HighAvailability:PeerTransport");
        var peer = topology.Nodes.Single(node =>
            !node.NodeId.Equals(topology.LocalNodeId, StringComparison.OrdinalIgnoreCase));

        Uri? peerEndpoint = null;
        var configuredEndpoint = _running.PeerTransport.PeerEndpoint;
        if (!string.IsNullOrWhiteSpace(configuredEndpoint))
        {
            peerEndpoint = NormalizePeerEndpoint(configuredEndpoint);
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
                    RuntimeHaPeerTransportOptions.ReplicationPath.TrimStart('/'));
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
            _running.PeerTransport.Enabled,
            topology.LocalNodeId,
            peer.NodeId,
            peerEndpoint,
            _running.PeerTransport.SharedSecret,
            maxPayloadBytes,
            TimeSpan.FromSeconds(requestTimeoutSeconds),
            TimeSpan.FromMilliseconds(pollMilliseconds),
            TimeSpan.FromSeconds(maximumRetrySeconds),
            TimeSpan.FromSeconds(authFreshnessSeconds),
            topology.FreshnessWindow,
            statePath);
    }

    public RuntimeHaHostConfigurationSnapshot Snapshot()
    {
        lock (_stateGate)
            return SnapshotLocked();
    }

    public async Task<RuntimeHaHostConfigurationUpdateResult> UpdateAsync(
        RuntimeHaHostConfigurationUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            RuntimeHaHostConfigurationDocument current;
            lock (_stateGate)
                current = _desired;

            if (request.ExpectedGeneration != current.Generation)
            {
                return Rejected(
                    "host-configuration-generation-stale",
                    new[]
                    {
                        $"Expected generation {request.ExpectedGeneration} does not match current generation {current.Generation}."
                    });
            }

            RuntimeHaHostConfigurationDocument candidate;
            try
            {
                candidate = BuildCandidate(request, current);
                ValidateDocument(
                    candidate,
                    enforceAdministrativeBounds: true,
                    configurationPath: _path);
            }
            catch (Exception ex) when (
                ex is ArgumentException or
                InvalidOperationException or
                UriFormatException or
                NotSupportedException)
            {
                return Rejected(
                    "host-configuration-invalid",
                    new[] { ex.Message });
            }

            if (TopologyShapeChanged(current, candidate) &&
                candidate.TopologyVersion <= current.TopologyVersion)
            {
                return Rejected(
                    "topology-version-must-increase",
                    new[]
                    {
                        "Changing HA cluster/node identity or advertised endpoints requires a strictly newer topologyVersion."
                    });
            }

            if (EquivalentPayload(current, candidate))
            {
                return new RuntimeHaHostConfigurationUpdateResult(
                    true,
                    "host-configuration-unchanged",
                    Snapshot(),
                    Array.Empty<string>());
            }

            candidate = candidate with
            {
                Generation = checked(current.Generation + 1),
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };

            try
            {
                await PersistAsync(candidate, cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return Rejected(
                    "host-configuration-persist-failed",
                    new[] { ex.Message });
            }

            lock (_stateGate)
            {
                _desired = candidate;
                _pendingRestart = !EquivalentPayload(_running, candidate);
                return new RuntimeHaHostConfigurationUpdateResult(
                    true,
                    _pendingRestart
                        ? "host-configuration-persisted-restart-required"
                        : "host-configuration-restored-running-state",
                    SnapshotLocked(),
                    Array.Empty<string>());
            }
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private RuntimeHaHostConfigurationUpdateResult Rejected(
        string reasonCode,
        IReadOnlyCollection<string> errors) =>
        new(false, reasonCode, Snapshot(), errors);

    private RuntimeHaHostConfigurationDocument BuildCandidate(
        RuntimeHaHostConfigurationUpdateRequest request,
        RuntimeHaHostConfigurationDocument current)
    {
        if (request.Enabled != _running.Enabled)
        {
            throw new InvalidOperationException(
                "Changing HighAvailability:Enabled is not an online administrative operation. " +
                "Use deployment break-glass configuration and a controlled cold start.");
        }
        if (request.Nodes is null)
            throw new InvalidOperationException("nodes is required.");
        if (request.PeerTransport is null)
            throw new InvalidOperationException("peerTransport is required.");
        if (request.Protection is null)
            throw new InvalidOperationException("protection is required.");
        if (request.PeerTransport.ClearPeerSharedSecret &&
            !string.IsNullOrWhiteSpace(request.PeerTransport.PeerSharedSecret))
        {
            throw new InvalidOperationException(
                "peerSharedSecret and clearPeerSharedSecret cannot be supplied together.");
        }

        var sharedSecret = request.PeerTransport.ClearPeerSharedSecret
            ? null
            : string.IsNullOrWhiteSpace(request.PeerTransport.PeerSharedSecret)
                ? current.PeerTransport.SharedSecret
                : request.PeerTransport.PeerSharedSecret.Trim();

        var nodes = request.Nodes
            .Select(node => new RuntimeHaHostNodeConfiguration(
                NormalizeIdentity(node.NodeId, "nodeId"),
                NormalizeOptionalEndpoint(node.LocalEndpoint, "localEndpoint"),
                NormalizeOptionalEndpoint(node.RemoteEndpoint, "remoteEndpoint")))
            .ToArray();

        var referencePath = string.IsNullOrWhiteSpace(request.Protection.ReferencePath)
            ? null
            : Path.GetFullPath(request.Protection.ReferencePath.Trim());

        var peerEndpoint = string.IsNullOrWhiteSpace(request.PeerTransport.PeerEndpoint)
            ? null
            : NormalizePeerEndpoint(request.PeerTransport.PeerEndpoint).ToString();

        return new RuntimeHaHostConfigurationDocument(
            RuntimeHaHostConfigurationDocument.SchemaName,
            RuntimeHaHostConfigurationDocument.CurrentSchemaVersion,
            current.Generation,
            current.UpdatedAtUtc,
            request.Enabled,
            NormalizeOptionalIdentity(request.ClusterId, "clusterId"),
            NormalizeIdentity(request.LocalNodeId, "localNodeId"),
            NormalizeOptionalIdentity(request.InitialActiveNodeId, "initialActiveNodeId"),
            request.TopologyVersion,
            request.FreshnessSeconds,
            nodes,
            new RuntimeHaHostPeerTransportConfiguration(
                request.PeerTransport.Enabled,
                peerEndpoint,
                sharedSecret),
            request.Protection with
            {
                ReferenceStoreMode = request.Protection.ReferenceStoreMode?.Trim() ?? string.Empty,
                ReferencePath = referencePath
            });
    }

    private RuntimeHaHostConfigurationSnapshot SnapshotLocked() =>
        new(
            RuntimeHaHostConfigurationDocument.SchemaName,
            RuntimeHaHostConfigurationDocument.CurrentSchemaVersion,
            _desired.Generation,
            _desired.UpdatedAtUtc,
            _pendingRestart,
            _pendingRestart ? "restart-required" : "running",
            _pendingRestart,
            Sanitize(_running),
            Sanitize(_desired),
            ReferenceRequirements);

    private static RuntimeHaHostConfigurationView Sanitize(
        RuntimeHaHostConfigurationDocument document) =>
        new(
            document.Enabled,
            document.ClusterId,
            document.LocalNodeId,
            document.InitialActiveNodeId,
            document.TopologyVersion,
            document.FreshnessSeconds,
            document.Nodes.ToArray(),
            new RuntimeHaHostPeerTransportView(
                document.PeerTransport.Enabled,
                document.PeerTransport.PeerEndpoint,
                HasStrongSharedSecret(document.PeerTransport.SharedSecret)),
            document.Protection);

    private static RuntimeHaHostConfigurationDocument FromBootstrapConfiguration(
        IConfiguration configuration)
    {
        var topology = RuntimeHaTopologyDefinition.FromConfiguration(configuration);
        var peerSection = configuration.GetSection("HighAvailability:PeerTransport");
        var protection = RuntimeHaProtectionOptions.FromConfiguration(configuration);

        var nodes = topology.Nodes
            .Select(node => new RuntimeHaHostNodeConfiguration(
                node.NodeId,
                node.Endpoints.SingleOrDefault(endpoint =>
                    endpoint.Kind == RuntimeHaEndpointKind.Local)?.Address,
                node.Endpoints.SingleOrDefault(endpoint =>
                    endpoint.Kind == RuntimeHaEndpointKind.Remote)?.Address))
            .ToArray();

        return new RuntimeHaHostConfigurationDocument(
            RuntimeHaHostConfigurationDocument.SchemaName,
            RuntimeHaHostConfigurationDocument.CurrentSchemaVersion,
            Generation: 1,
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            topology.Enabled,
            topology.ClusterId,
            topology.LocalNodeId,
            topology.InitialActiveNodeId,
            topology.TopologyVersion,
            Math.Max(1, (int)Math.Ceiling(topology.FreshnessWindow.TotalSeconds)),
            nodes,
            new RuntimeHaHostPeerTransportConfiguration(
                topology.Enabled && (peerSection.GetValue<bool?>("Enabled") ?? true),
                NormalizeOptionalPeerEndpoint(peerSection["PeerEndpoint"]),
                peerSection["SharedSecret"]),
            new RuntimeHaHostProtectionConfiguration(
                protection.Enabled,
                protection.AutomaticFailoverEnabled,
                ReferenceStoreModeSharedExternalFile,
                protection.ReferencePath,
                Math.Max(1, (int)Math.Round(protection.LeaseDuration.TotalSeconds)),
                Math.Max(1, (int)Math.Round(protection.PollInterval.TotalMilliseconds)),
                Math.Max(1, (int)Math.Round(protection.ReadyWitnessMaximumAge.TotalSeconds)),
                Math.Max(0, (int)Math.Round(protection.ClockSkewSafetyMargin.TotalSeconds))));
    }

    private static RuntimeHaTopologyDefinition ToTopology(
        RuntimeHaHostConfigurationDocument document)
    {
        if (!document.Enabled)
        {
            return new RuntimeHaTopologyDefinition(
                false,
                null,
                document.LocalNodeId,
                document.LocalNodeId,
                Math.Max(1, document.TopologyVersion),
                TimeSpan.FromSeconds(Math.Max(1, document.FreshnessSeconds)),
                new[]
                {
                    new RuntimeHaNodeDefinition(
                        document.LocalNodeId,
                        Array.Empty<RuntimeHaEndpoint>())
                });
        }

        var nodes = document.Nodes
            .Select(node => new RuntimeHaNodeDefinition(
                node.NodeId,
                new[]
                {
                    new RuntimeHaEndpoint(
                        RuntimeHaEndpointKind.Local,
                        node.LocalEndpoint!,
                        0),
                    new RuntimeHaEndpoint(
                        RuntimeHaEndpointKind.Remote,
                        node.RemoteEndpoint!,
                        1)
                }))
            .ToArray();

        return new RuntimeHaTopologyDefinition(
            true,
            document.ClusterId,
            document.LocalNodeId,
            document.InitialActiveNodeId,
            document.TopologyVersion,
            TimeSpan.FromSeconds(document.FreshnessSeconds),
            nodes);
    }

    private static void ValidateDocument(
        RuntimeHaHostConfigurationDocument document,
        bool enforceAdministrativeBounds,
        string configurationPath)
    {
        if (!string.Equals(
                document.Schema,
                RuntimeHaHostConfigurationDocument.SchemaName,
                StringComparison.Ordinal) ||
            document.SchemaVersion != RuntimeHaHostConfigurationDocument.CurrentSchemaVersion)
        {
            throw new InvalidOperationException("HA host configuration schema is incompatible.");
        }
        if (document.Generation < 1)
            throw new InvalidOperationException("generation must be positive.");
        if (document.TopologyVersion < 1)
            throw new InvalidOperationException("topologyVersion must be positive.");
        _ = NormalizeIdentity(document.LocalNodeId, "localNodeId");

        if (enforceAdministrativeBounds &&
            (document.FreshnessSeconds < 1 || document.FreshnessSeconds > 120))
        {
            throw new InvalidOperationException("freshnessSeconds must be between 1 and 120.");
        }

        if (document.Enabled)
        {
            var clusterId = NormalizeOptionalIdentity(document.ClusterId, "clusterId");
            var initialActive = NormalizeOptionalIdentity(
                document.InitialActiveNodeId,
                "initialActiveNodeId");
            if (clusterId is null)
                throw new InvalidOperationException("clusterId is required while HA is enabled.");
            if (initialActive is null)
                throw new InvalidOperationException("initialActiveNodeId is required while HA is enabled.");
            if (document.Nodes.Count != 2)
                throw new InvalidOperationException("Wave 15 HA requires exactly two nodes.");

            var ids = document.Nodes
                .Select(node => NormalizeIdentity(node.NodeId, "nodeId"))
                .ToArray();
            if (ids.Distinct(StringComparer.OrdinalIgnoreCase).Count() != ids.Length)
                throw new InvalidOperationException("HA node identities must be unique.");
            if (!ids.Contains(document.LocalNodeId, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException("localNodeId must identify one configured node.");
            if (!ids.Contains(initialActive, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException("initialActiveNodeId must identify one configured node.");

            var advertised = new List<string>();
            foreach (var node in document.Nodes)
            {
                advertised.Add(NormalizeRequiredEndpoint(
                    node.LocalEndpoint,
                    node.NodeId,
                    "localEndpoint"));
                advertised.Add(NormalizeRequiredEndpoint(
                    node.RemoteEndpoint,
                    node.NodeId,
                    "remoteEndpoint"));
            }
            if (advertised.Distinct(StringComparer.OrdinalIgnoreCase).Count() != advertised.Count)
            {
                throw new InvalidOperationException(
                    "Advertised Local/Remote endpoints must be unique across the HA topology.");
            }

            if (document.PeerTransport.Enabled)
            {
                if (!HasStrongSharedSecret(document.PeerTransport.SharedSecret))
                {
                    throw new InvalidOperationException(
                        "Enabled HA peer transport requires a shared secret of at least 32 UTF-8 bytes.");
                }

                if (!string.IsNullOrWhiteSpace(document.PeerTransport.PeerEndpoint))
                {
                    var peerEndpoint = NormalizePeerEndpoint(document.PeerTransport.PeerEndpoint);
                    var local = document.Nodes.Single(node =>
                        node.NodeId.Equals(
                            document.LocalNodeId,
                            StringComparison.OrdinalIgnoreCase));
                    var localEndpoints = new[] { local.LocalEndpoint, local.RemoteEndpoint }
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => new Uri(value!.TrimEnd('/') + "/"))
                        .ToArray();
                    if (localEndpoints.Any(localEndpoint =>
                            Uri.Compare(
                                localEndpoint,
                                new Uri(peerEndpoint.GetLeftPart(UriPartial.Authority) + "/"),
                                UriComponents.SchemeAndServer,
                                UriFormat.Unescaped,
                                StringComparison.OrdinalIgnoreCase) == 0))
                    {
                        throw new InvalidOperationException(
                            "peerEndpoint must not resolve to the configured local HA node.");
                    }
                }
            }

            if (document.Protection.AutomaticFailoverEnabled && !document.Protection.Enabled)
            {
                throw new InvalidOperationException(
                    "automaticFailoverEnabled requires HA protection to be enabled.");
            }
            if (document.Protection.Enabled)
            {
                if (!string.Equals(
                        document.Protection.ReferenceStoreMode,
                        ReferenceStoreModeSharedExternalFile,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"referenceStoreMode must be '{ReferenceStoreModeSharedExternalFile}'.");
                }
                if (string.IsNullOrWhiteSpace(document.Protection.ReferencePath))
                {
                    throw new InvalidOperationException(
                        "referencePath is required while HA protection is enabled.");
                }

                var referencePath = Path.GetFullPath(document.Protection.ReferencePath);
                if (PathEquals(referencePath, configurationPath))
                {
                    throw new InvalidOperationException(
                        "The external HA reference authority must not use the host configuration file itself.");
                }
            }
        }

        if (enforceAdministrativeBounds)
        {
            var protection = document.Protection;
            if (protection.LeaseSeconds < 3 || protection.LeaseSeconds > 120)
                throw new InvalidOperationException("leaseSeconds must be between 3 and 120.");
            if (protection.PollMilliseconds < 100 || protection.PollMilliseconds > 10_000)
                throw new InvalidOperationException("pollMilliseconds must be between 100 and 10000.");
            if (protection.ReadyWitnessMaximumAgeSeconds < protection.LeaseSeconds ||
                protection.ReadyWitnessMaximumAgeSeconds > 600)
            {
                throw new InvalidOperationException(
                    "readyWitnessMaximumAgeSeconds must be between leaseSeconds and 600.");
            }

            var maximumSkew = protection.LeaseSeconds / 3;
            if (protection.ClockSkewSafetyMarginSeconds < 0 ||
                protection.ClockSkewSafetyMarginSeconds > maximumSkew)
            {
                throw new InvalidOperationException(
                    "clockSkewSafetyMarginSeconds must be between 0 and one third of leaseSeconds.");
            }
        }
    }

    private static bool TopologyShapeChanged(
        RuntimeHaHostConfigurationDocument left,
        RuntimeHaHostConfigurationDocument right)
    {
        var leftShape = left with
        {
            Generation = 1,
            UpdatedAtUtc = DateTimeOffset.UnixEpoch,
            TopologyVersion = 1,
            PeerTransport = left.PeerTransport with
            {
                Enabled = false,
                PeerEndpoint = null,
                SharedSecret = null
            },
            Protection = left.Protection with
            {
                Enabled = false,
                AutomaticFailoverEnabled = false,
                ReferencePath = null
            }
        };
        var rightShape = right with
        {
            Generation = 1,
            UpdatedAtUtc = DateTimeOffset.UnixEpoch,
            TopologyVersion = 1,
            PeerTransport = right.PeerTransport with
            {
                Enabled = false,
                PeerEndpoint = null,
                SharedSecret = null
            },
            Protection = right.Protection with
            {
                Enabled = false,
                AutomaticFailoverEnabled = false,
                ReferencePath = null
            }
        };
        return !EquivalentPayload(leftShape, rightShape);
    }

    private static bool EquivalentPayload(
        RuntimeHaHostConfigurationDocument left,
        RuntimeHaHostConfigurationDocument right)
    {
        var normalizedLeft = left with
        {
            Generation = 0,
            UpdatedAtUtc = DateTimeOffset.UnixEpoch
        };
        var normalizedRight = right with
        {
            Generation = 0,
            UpdatedAtUtc = DateTimeOffset.UnixEpoch
        };
        return JsonSerializer.Serialize(normalizedLeft, Json)
            .Equals(
                JsonSerializer.Serialize(normalizedRight, Json),
                StringComparison.Ordinal);
    }

    private async Task PersistAsync(
        RuntimeHaHostConfigurationDocument document,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(
                temporary,
                JsonSerializer.SerializeToUtf8Bytes(document, Json),
                cancellationToken);
            File.Move(temporary, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private static RuntimeHaHostConfigurationDocument ReadPersisted(string path)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            return JsonSerializer.Deserialize<RuntimeHaHostConfigurationDocument>(bytes, Json)
                ?? throw new InvalidDataException("HA host configuration is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("HA host configuration JSON is invalid.", ex);
        }
    }

    private static string ResolveConfigurationPath(IConfiguration configuration)
    {
        var configured = configuration["HighAvailability:Administration:ConfigurationPath"];
        return string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(
                AppContext.BaseDirectory,
                "data",
                "ha",
                "host-configuration.json")
            : Path.GetFullPath(configured.Trim());
    }

    private static string NormalizeIdentity(string? value, string field)
    {
        var normalized = NormalizeOptionalIdentity(value, field);
        return normalized
            ?? throw new InvalidOperationException($"{field} is required.");
    }

    private static string? NormalizeOptionalIdentity(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var normalized = value.Trim();
        if (normalized.Length > 128)
            throw new InvalidOperationException($"{field} must not exceed 128 characters.");
        return normalized;
    }

    private static string? NormalizeOptionalEndpoint(string? value, string field) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : NormalizeAbsoluteHttpEndpoint(value, field);

    private static string NormalizeRequiredEndpoint(
        string? value,
        string nodeId,
        string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"HA node '{nodeId}' requires {field}.");
        return NormalizeAbsoluteHttpEndpoint(value, field);
    }

    private static string NormalizeAbsoluteHttpEndpoint(string value, string field)
    {
        var normalized = value.Trim();
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"{field} must be an absolute HTTP(S) endpoint.");
        }

        return uri.ToString().TrimEnd('/');
    }

    private static string? NormalizeOptionalPeerEndpoint(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : NormalizePeerEndpoint(value).ToString();

    private static Uri NormalizePeerEndpoint(string value)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var peerEndpoint) ||
            (peerEndpoint.Scheme != Uri.UriSchemeHttp &&
             peerEndpoint.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                "peerEndpoint must be an absolute HTTP(S) endpoint.");
        }

        if (!peerEndpoint.AbsolutePath.EndsWith(
                RuntimeHaPeerTransportOptions.ReplicationPath,
                StringComparison.OrdinalIgnoreCase))
        {
            peerEndpoint = new Uri(
                new Uri(peerEndpoint.ToString().TrimEnd('/') + "/"),
                RuntimeHaPeerTransportOptions.ReplicationPath.TrimStart('/'));
        }

        return peerEndpoint;
    }

    private static bool HasStrongSharedSecret(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        Encoding.UTF8.GetByteCount(value) >= 32;

    private static bool PathEquals(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        return new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }
}
