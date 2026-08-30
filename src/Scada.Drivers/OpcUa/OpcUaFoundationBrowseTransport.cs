using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Opc.Ua;
using Opc.Ua.Client;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.OpcUa;

/// <summary>
/// Foundation-backed Engineering browse transport. Browse continuation points remain
/// private to this adapter and are bound to the exact secure session that created them.
/// External callers only receive short-lived opaque tokens.
/// </summary>
public sealed class OpcUaFoundationBrowseTransport : IOpcUaBrowseTransport, IAsyncDisposable
{
    private const string DefaultParentNodeId = "i=85"; // ObjectsFolder
    private const int DefaultPageSize = 200;
    private const int HardPageSize = 500;
    private const int AttributeVariableBatchSize = 50;
    private const int DefaultMaximumActiveContinuations = 64;
    private static readonly TimeSpan DefaultContinuationLifetime = TimeSpan.FromMinutes(2);

    private readonly IOpcUaRuntimeSecurityMaterialProvider _securityMaterialProvider;
    private readonly ConcurrentDictionary<string, ContinuationState> _continuations = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _browseGate = new(1, 1);
    private readonly TimeSpan _continuationLifetime;
    private readonly int _maximumActiveContinuations;
    private int _disposed;

    public OpcUaFoundationBrowseTransport(
        IOpcUaRuntimeSecurityMaterialProvider securityMaterialProvider,
        TimeSpan? continuationLifetime = null,
        int maximumActiveContinuations = DefaultMaximumActiveContinuations)
    {
        _securityMaterialProvider = securityMaterialProvider ??
            throw new ArgumentNullException(nameof(securityMaterialProvider));
        _continuationLifetime = continuationLifetime ?? DefaultContinuationLifetime;
        if (_continuationLifetime <= TimeSpan.Zero || _continuationLifetime > TimeSpan.FromMinutes(10))
        {
            throw new ArgumentOutOfRangeException(
                nameof(continuationLifetime),
                "OPC UA Engineering browse continuation lifetime must be greater than zero and no more than 10 minutes.");
        }
        if (maximumActiveContinuations is < 1 or > 256)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumActiveContinuations),
                "OPC UA Engineering browse active continuation limit must be between 1 and 256.");
        }

        _maximumActiveContinuations = maximumActiveContinuations;
    }

    public async ValueTask<OpcUaBrowseTransportPage> BrowseAsync(
        OpcUaBrowseTransportRequest request,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        await _browseGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (!string.Equals(
                request.Context.DriverType,
                OpcUaDriverDescriptorProvider.DriverTypeId,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"OPC UA browse transport cannot browse driver type '{request.Context.DriverType}'.");
            }

            int pageSize = NormalizePageSize(request.PageSize);
            await CleanupExpiredContinuationsAsync().ConfigureAwait(false);

            OpcUaRuntimeConnectionOptions options = OpcUaRuntimeDriverComposer.ParseConnectionOptions(request.Context);
            string contextFingerprint = CreateContextFingerprint(request.Context, options);

            if (!string.IsNullOrWhiteSpace(request.ContinuationToken))
            {
                return await ContinueAsync(
                    request,
                    contextFingerprint,
                    pageSize,
                    cancellationToken).ConfigureAwait(false);
            }

            return await BrowseFirstPageAsync(
                request,
                options,
                contextFingerprint,
                pageSize,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _browseGate.Release();
        }
    }

    private async ValueTask<OpcUaBrowseTransportPage> BrowseFirstPageAsync(
        OpcUaBrowseTransportRequest request,
        OpcUaRuntimeConnectionOptions options,
        string contextFingerprint,
        int pageSize,
        CancellationToken cancellationToken)
    {
        OpcUaNodeIdentity parentIdentity = ParseParent(request.ParentNodeId);
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [OpcUaRuntimeBinding.NodeIdMetadataKey] = parentIdentity.NodeId
        };
        if (!string.IsNullOrWhiteSpace(parentIdentity.NamespaceUri))
        {
            metadata[OpcUaRuntimeBinding.NamespaceUriMetadataKey] = parentIdentity.NamespaceUri;
        }

        TagDefinition probeTag = TagDefinition.Create(
            "BrowseParent",
            $"__engineering.opcua.{Guid.NewGuid():N}.BrowseParent",
            TagDataType.String,
            source: request.Context.DataSourceKey,
            readOnly: true,
            metadata: metadata);

        OpcUaRuntimeBinding probeBinding = OpcUaRuntimeBinding.FromTag(probeTag);
        var sessionFactory = new OpcUaFoundationRuntimeSessionFactory(options, _securityMaterialProvider);
        IOpcUaRuntimeSession? runtimeSession = null;
        byte[]? continuationPoint = null;

        try
        {
            runtimeSession = await sessionFactory
                .ConnectAsync([probeBinding], cancellationToken)
                .ConfigureAwait(false);

            ISession foundationSession = GetFoundationSession(runtimeSession);
            NodeId parentNodeId = ResolveNodeId(foundationSession, parentIdentity);

            var browseDescription = new BrowseDescription
            {
                NodeId = parentNodeId,
                BrowseDirection = BrowseDirection.Forward,
                ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences,
                IncludeSubtypes = true,
                NodeClassMask = 0,
                ResultMask = (uint)BrowseResultMask.All
            };

            BrowseResponse response = await foundationSession
                .BrowseAsync(
                    requestHeader: null,
                    view: null,
                    requestedMaxReferencesPerNode: checked((uint)pageSize),
                    nodesToBrowse: new BrowseDescriptionCollection { browseDescription },
                    ct: cancellationToken)
                .ConfigureAwait(false);

            BrowseResult result = ValidateSingleBrowseResult(response.Results, "Browse");
            continuationPoint = CloneContinuationPoint(result.ContinuationPoint);
            var issues = new List<DriverEngineeringIssue>();
            IReadOnlyCollection<OpcUaBrowseNodeEvidence> nodes = await MapReferencesAsync(
                foundationSession,
                result.References ?? [],
                issues,
                cancellationToken).ConfigureAwait(false);

            string? externalToken = null;
            if (continuationPoint is { Length: > 0 })
            {
                externalToken = await StoreContinuationAsync(
                    runtimeSession,
                    foundationSession,
                    continuationPoint,
                    contextFingerprint,
                    request.ParentNodeId,
                    cancellationToken).ConfigureAwait(false);
                runtimeSession = null;
                continuationPoint = null;
            }

            return new OpcUaBrowseTransportPage(
                Nodes: nodes,
                ContinuationToken: externalToken,
                IsPartial: externalToken is not null,
                Issues: issues);
        }
        catch
        {
            if (runtimeSession is not null)
            {
                if (continuationPoint is { Length: > 0 } &&
                    runtimeSession is IOpcUaFoundationSessionAccessor accessor)
                {
                    await TryReleaseContinuationAsync(accessor.FoundationSession, continuationPoint)
                        .ConfigureAwait(false);
                }
                await runtimeSession.DisposeAsync().ConfigureAwait(false);
                runtimeSession = null;
            }
            throw;
        }
        finally
        {
            if (runtimeSession is not null)
            {
                await runtimeSession.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async ValueTask<OpcUaBrowseTransportPage> ContinueAsync(
        OpcUaBrowseTransportRequest request,
        string contextFingerprint,
        int pageSize,
        CancellationToken cancellationToken)
    {
        _ = pageSize; // Page size is fixed by the server continuation point after the first Browse call.
        string token = request.ContinuationToken!.Trim();
        if (!_continuations.TryGetValue(token, out ContinuationState? state))
        {
            throw new InvalidOperationException(
                "OPC UA browse continuation token is unknown or has expired. Restart browsing from the parent node.");
        }

        if (!CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(state.ContextFingerprint),
            Convert.FromHexString(contextFingerprint)))
        {
            throw new InvalidOperationException(
                "OPC UA browse continuation token does not belong to the supplied data source context.");
        }

        if (!string.IsNullOrWhiteSpace(request.ParentNodeId) &&
            !string.Equals(request.ParentNodeId.Trim(), state.ParentNodeId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "OPC UA browse continuation token does not belong to the supplied parent node.");
        }

        if (state.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            if (_continuations.TryRemove(token, out ContinuationState? expired))
            {
                await ReleaseAndDisposeAsync(expired).ConfigureAwait(false);
            }
            throw new InvalidOperationException(
                "OPC UA browse continuation token has expired. Restart browsing from the parent node.");
        }

        if (!_continuations.TryRemove(token, out state))
        {
            throw new InvalidOperationException(
                "OPC UA browse continuation token was already consumed. Restart browsing from the parent node.");
        }

        byte[]? nextContinuationPoint = null;
        try
        {
            BrowseNextResponse response = await state.FoundationSession
                .BrowseNextAsync(
                    requestHeader: null,
                    releaseContinuationPoints: false,
                    continuationPoints: new ByteStringCollection(state.ContinuationPoint),
                    ct: cancellationToken)
                .ConfigureAwait(false);

            BrowseResult result = ValidateSingleBrowseResult(response.Results, "BrowseNext");
            nextContinuationPoint = CloneContinuationPoint(result.ContinuationPoint);
            var issues = new List<DriverEngineeringIssue>();
            IReadOnlyCollection<OpcUaBrowseNodeEvidence> nodes = await MapReferencesAsync(
                state.FoundationSession,
                result.References ?? [],
                issues,
                cancellationToken).ConfigureAwait(false);

            string? nextToken = null;
            if (nextContinuationPoint is { Length: > 0 })
            {
                nextToken = await StoreContinuationAsync(
                    state.RuntimeSession,
                    state.FoundationSession,
                    nextContinuationPoint,
                    state.ContextFingerprint,
                    state.ParentNodeId,
                    cancellationToken).ConfigureAwait(false);
                nextContinuationPoint = null;
            }
            else
            {
                await state.RuntimeSession.DisposeAsync().ConfigureAwait(false);
            }

            return new OpcUaBrowseTransportPage(
                Nodes: nodes,
                ContinuationToken: nextToken,
                IsPartial: nextToken is not null,
                Issues: issues);
        }
        catch
        {
            await TryReleaseContinuationAsync(
                state.FoundationSession,
                nextContinuationPoint is { Length: > 0 }
                    ? nextContinuationPoint
                    : state.ContinuationPoint).ConfigureAwait(false);
            await state.RuntimeSession.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private async ValueTask<string> StoreContinuationAsync(
        IOpcUaRuntimeSession runtimeSession,
        ISession foundationSession,
        byte[] continuationPoint,
        string contextFingerprint,
        string? parentNodeId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await CleanupExpiredContinuationsAsync().ConfigureAwait(false);

        if (_continuations.Count >= _maximumActiveContinuations)
        {
            throw new InvalidOperationException(
                "OPC UA Engineering browse has reached its active continuation limit. Finish or allow existing browse pages to expire before opening another paged browse.");
        }

        for (int attempt = 0; attempt < 8; attempt++)
        {
            string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
            var state = new ContinuationState(
                runtimeSession,
                foundationSession,
                continuationPoint.ToArray(),
                contextFingerprint,
                string.IsNullOrWhiteSpace(parentNodeId) ? null : parentNodeId.Trim(),
                DateTimeOffset.UtcNow + _continuationLifetime);
            if (_continuations.TryAdd(token, state))
            {
                return token;
            }
        }

        throw new InvalidOperationException("Could not allocate an opaque OPC UA browse continuation token.");
    }

    private static async Task<IReadOnlyCollection<OpcUaBrowseNodeEvidence>> MapReferencesAsync(
        ISession session,
        ReferenceDescriptionCollection references,
        List<DriverEngineeringIssue> pageIssues,
        CancellationToken cancellationToken)
    {
        var drafts = new List<NodeDraft>(references.Count);
        foreach (ReferenceDescription reference in references)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reference.NodeId is null)
            {
                continue;
            }
            if (reference.NodeId.ServerIndex != 0)
            {
                pageIssues.Add(new DriverEngineeringIssue(
                    "OPCUA_BROWSE_REMOTE_SERVER_REFERENCE_SKIPPED",
                    DriverEngineeringIssueSeverity.Warning,
                    $"Browse reference '{reference.BrowseName}' targets remote ServerIndex {reference.NodeId.ServerIndex} and was not followed."));
                continue;
            }

            NodeId? nodeId = ExpandedNodeId.ToNodeId(reference.NodeId, session.NamespaceUris);
            if (nodeId is null)
            {
                pageIssues.Add(new DriverEngineeringIssue(
                    "OPCUA_BROWSE_NODEID_UNRESOLVED",
                    DriverEngineeringIssueSeverity.Warning,
                    $"Browse reference '{reference.BrowseName}' could not be resolved against the active session namespace table."));
                continue;
            }

            string? namespaceUri = session.NamespaceUris.GetString(nodeId.NamespaceIndex);
            drafts.Add(new NodeDraft(
                nodeId,
                namespaceUri,
                reference.BrowseName?.ToString() ?? nodeId.ToString(),
                reference.DisplayName?.Text ?? reference.BrowseName?.Name ?? nodeId.ToString(),
                MapNodeClass(reference.NodeClass),
                reference.ReferenceTypeId?.ToString(),
                reference.TypeDefinition?.ToString()));
        }

        NodeDraft[] variables = drafts
            .Where(draft => draft.NodeClass == OpcUaBrowseNodeClass.Variable)
            .ToArray();

        foreach (NodeDraft[] batch in variables.Chunk(AttributeVariableBatchSize))
        {
            try
            {
                await EnrichVariableBatchAsync(session, batch, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                pageIssues.Add(new DriverEngineeringIssue(
                    "OPCUA_BROWSE_ATTRIBUTE_READ_FAILED",
                    DriverEngineeringIssueSeverity.Warning,
                    "One bounded OPC UA browse attribute-read batch failed. References remain visible with conservative access/type metadata."));
            }
        }

        return drafts.Select(draft => draft.ToEvidence()).ToArray();
    }

    private static async Task EnrichVariableBatchAsync(
        ISession session,
        IReadOnlyList<NodeDraft> batch,
        CancellationToken cancellationToken)
    {
        const int AttributesPerVariable = 5;
        var reads = new ReadValueIdCollection();
        foreach (NodeDraft draft in batch)
        {
            reads.Add(new ReadValueId { NodeId = draft.NodeId, AttributeId = Attributes.UserAccessLevel });
            reads.Add(new ReadValueId { NodeId = draft.NodeId, AttributeId = Attributes.AccessLevel });
            reads.Add(new ReadValueId { NodeId = draft.NodeId, AttributeId = Attributes.DataType });
            reads.Add(new ReadValueId { NodeId = draft.NodeId, AttributeId = Attributes.ValueRank });
            reads.Add(new ReadValueId { NodeId = draft.NodeId, AttributeId = Attributes.Historizing });
        }

        ReadResponse response = await session
            .ReadAsync(
                requestHeader: null,
                maxAge: 0,
                timestampsToReturn: TimestampsToReturn.Neither,
                nodesToRead: reads,
                ct: cancellationToken)
            .ConfigureAwait(false);

        if (response.Results is null || response.Results.Count != reads.Count)
        {
            throw new InvalidOperationException("OPC UA browse attribute read returned an invalid result count.");
        }

        for (int index = 0; index < batch.Count; index++)
        {
            NodeDraft draft = batch[index];
            int offset = index * AttributesPerVariable;
            DataValue userAccess = response.Results[offset];
            DataValue access = response.Results[offset + 1];
            DataValue dataType = response.Results[offset + 2];
            DataValue valueRank = response.Results[offset + 3];
            DataValue historizing = response.Results[offset + 4];

            byte? accessLevel = TryReadByte(userAccess) ?? TryReadByte(access);
            if (accessLevel.HasValue)
            {
                draft.IsReadable = (accessLevel.Value & 0x01) != 0;
                draft.IsWritable = (accessLevel.Value & 0x02) != 0;
            }

            if (TryReadValue<NodeId>(dataType, out NodeId? typeNodeId) && typeNodeId is not null)
            {
                draft.BuiltInDataType = await OpcUaFoundationDataTypeResolver
                    .ResolveAsync(session, typeNodeId, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (TryReadInt32(valueRank, out int rank))
            {
                draft.ValueRank = rank;
            }

            if (TryReadValue<bool>(historizing, out bool isHistorizing))
            {
                draft.IsHistorizing = isHistorizing;
            }
        }
    }

    private static byte? TryReadByte(DataValue value)
    {
        if (!StatusCode.IsGood(value.StatusCode) || value.Value is null) return null;
        try
        {
            return Convert.ToByte(value.Value, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            return null;
        }
    }

    private static bool TryReadInt32(DataValue value, out int result)
    {
        result = default;
        if (!StatusCode.IsGood(value.StatusCode) || value.Value is null) return false;
        try
        {
            result = Convert.ToInt32(value.Value, CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            return false;
        }
    }

    private static bool TryReadValue<T>(DataValue value, out T? result)
    {
        if (StatusCode.IsGood(value.StatusCode) && value.Value is T typed)
        {
            result = typed;
            return true;
        }
        result = default;
        return false;
    }

    private static OpcUaBrowseNodeClass MapNodeClass(NodeClass nodeClass) =>
        nodeClass switch
        {
            NodeClass.Object => OpcUaBrowseNodeClass.Object,
            NodeClass.Variable => OpcUaBrowseNodeClass.Variable,
            NodeClass.Method => OpcUaBrowseNodeClass.Method,
            NodeClass.View => OpcUaBrowseNodeClass.View,
            _ => OpcUaBrowseNodeClass.Other
        };

    private static BrowseResult ValidateSingleBrowseResult(BrowseResultCollection? results, string operation)
    {
        if (results is null || results.Count != 1)
        {
            throw new InvalidOperationException($"OPC UA {operation} returned an invalid result count.");
        }
        BrowseResult result = results[0];
        if (StatusCode.IsBad(result.StatusCode))
        {
            throw new InvalidOperationException($"OPC UA {operation} failed with status '{result.StatusCode}'.");
        }
        return result;
    }

    private static byte[]? CloneContinuationPoint(byte[]? continuationPoint) =>
        continuationPoint is { Length: > 0 } ? continuationPoint.ToArray() : null;

    private static ISession GetFoundationSession(IOpcUaRuntimeSession runtimeSession) =>
        runtimeSession is IOpcUaFoundationSessionAccessor accessor
            ? accessor.FoundationSession
            : throw new InvalidOperationException(
                "OPC UA Foundation browse requires a Foundation-backed runtime session.");

    private static OpcUaNodeIdentity ParseParent(string? parentNodeId)
    {
        if (string.IsNullOrWhiteSpace(parentNodeId))
        {
            return new OpcUaNodeIdentity(DefaultParentNodeId);
        }

        string trimmed = parentNodeId.Trim();
        return trimmed.StartsWith("node=", StringComparison.Ordinal)
            ? OpcUaNodeIdentity.ParsePortableAddress(trimmed)
            : new OpcUaNodeIdentity(trimmed);
    }

    private static NodeId ResolveNodeId(ISession session, OpcUaNodeIdentity identity) =>
        NodeId.Parse(
            OpcUaRuntimeProtocolSupport.ResolveSessionNodeId(
                identity,
                namespaceUri => session.NamespaceUris.GetIndex(namespaceUri)));

    private static int NormalizePageSize(int pageSize)
    {
        int normalized = pageSize <= 0 ? DefaultPageSize : pageSize;
        if (normalized > HardPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                $"OPC UA Foundation browse page size cannot exceed {HardPageSize} nodes.");
        }
        return normalized;
    }

    private static string CreateContextFingerprint(
        DriverEngineeringDataSourceContext context,
        OpcUaRuntimeConnectionOptions options)
    {
        string material = string.Join("\n",
            context.DataSourceKey.Trim(),
            NormalizeEndpointForFingerprint(options.EndpointUrl),
            options.SecurityMode.Trim(),
            options.SecurityPolicyUri.Trim(),
            options.AuthenticationMode.ToString(),
            options.UserName?.Trim() ?? string.Empty,
            options.ApprovedServerApplicationUri?.Trim() ?? string.Empty,
            options.NormalizedApprovedServerCertificateSha256 ?? string.Empty,
            options.PasswordSecretReference?.Trim() ?? string.Empty,
            options.ClientCertificateReference?.Trim() ?? string.Empty,
            options.UserCertificateReference?.Trim() ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    private static string NormalizeEndpointForFingerprint(string endpoint)
    {
        if (!Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out Uri? uri)) return endpoint.Trim();
        var builder = new UriBuilder(uri)
        {
            UserName = string.Empty,
            Password = string.Empty,
            Query = string.Empty,
            Fragment = string.Empty
        };
        return builder.Uri.AbsoluteUri.TrimEnd('/');
    }

    private async Task CleanupExpiredContinuationsAsync()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach (var pair in _continuations)
        {
            if (pair.Value.ExpiresAtUtc > now) continue;
            if (_continuations.TryRemove(pair.Key, out ContinuationState? expired))
            {
                await ReleaseAndDisposeAsync(expired).ConfigureAwait(false);
            }
        }
    }

    private static async Task ReleaseAndDisposeAsync(ContinuationState state)
    {
        await TryReleaseContinuationAsync(state.FoundationSession, state.ContinuationPoint)
            .ConfigureAwait(false);
        await state.RuntimeSession.DisposeAsync().ConfigureAwait(false);
    }

    private static async Task TryReleaseContinuationAsync(ISession session, byte[] continuationPoint)
    {
        if (continuationPoint.Length == 0) return;
        try
        {
            await session.BrowseNextAsync(
                requestHeader: null,
                releaseContinuationPoints: true,
                continuationPoints: new ByteStringCollection(continuationPoint),
                ct: CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Session disposal is still required if the server/channel can no longer release it.
        }
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(OpcUaFoundationBrowseTransport));
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        await _browseGate.WaitAsync().ConfigureAwait(false);
        try
        {
            foreach (var pair in _continuations.ToArray())
            {
                if (_continuations.TryRemove(pair.Key, out ContinuationState? state))
                {
                    await ReleaseAndDisposeAsync(state).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _browseGate.Release();
            _browseGate.Dispose();
        }
    }

    private sealed class ContinuationState(
        IOpcUaRuntimeSession runtimeSession,
        ISession foundationSession,
        byte[] continuationPoint,
        string contextFingerprint,
        string? parentNodeId,
        DateTimeOffset expiresAtUtc)
    {
        public IOpcUaRuntimeSession RuntimeSession { get; } = runtimeSession;
        public ISession FoundationSession { get; } = foundationSession;
        public byte[] ContinuationPoint { get; } = continuationPoint;
        public string ContextFingerprint { get; } = contextFingerprint;
        public string? ParentNodeId { get; } = parentNodeId;
        public DateTimeOffset ExpiresAtUtc { get; } = expiresAtUtc;
    }

    private sealed class NodeDraft(
        NodeId nodeId,
        string? namespaceUri,
        string browseName,
        string displayName,
        OpcUaBrowseNodeClass nodeClass,
        string? referenceTypeId,
        string? typeDefinition)
    {
        public NodeId NodeId { get; } = nodeId;
        public string? NamespaceUri { get; } = namespaceUri;
        public string BrowseName { get; } = browseName;
        public string DisplayName { get; } = displayName;
        public OpcUaBrowseNodeClass NodeClass { get; } = nodeClass;
        public string? ReferenceTypeId { get; } = referenceTypeId;
        public string? TypeDefinition { get; } = typeDefinition;
        public bool IsReadable { get; set; }
        public bool IsWritable { get; set; }
        public OpcUaBuiltInDataType? BuiltInDataType { get; set; }
        public int ValueRank { get; set; } = -1;
        public bool IsHistorizing { get; set; }

        public OpcUaBrowseNodeEvidence ToEvidence()
        {
            var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(ReferenceTypeId)) metadata["opcUa.referenceTypeId"] = ReferenceTypeId;
            if (!string.IsNullOrWhiteSpace(TypeDefinition)) metadata["opcUa.typeDefinition"] = TypeDefinition;

            return new OpcUaBrowseNodeEvidence(
                NodeId: NodeId.ToString(),
                NamespaceUri: NamespaceUri,
                BrowseName: BrowseName,
                DisplayName: DisplayName,
                NodeClass: NodeClass,
                IsReadable: IsReadable,
                IsWritable: IsWritable,
                BuiltInDataType: BuiltInDataType,
                ValueRank: ValueRank,
                IsHistorizing: IsHistorizing,
                EngineeringUnit: null,
                Description: null,
                BrowsePath: null,
                Metadata: metadata,
                Issues: null);
        }
    }
}
