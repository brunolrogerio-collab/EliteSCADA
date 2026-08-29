using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.OpcUa;

/// <summary>
/// Engineering adapter for bounded OPC UA endpoint discovery and lazy browse.
/// It is intentionally runtime-neutral and cannot mutate canonical Engineering.
/// </summary>
public sealed class OpcUaEngineeringAdapter :
    ICommunicationDriverDiscoverySource,
    ICommunicationDriverBrowser
{
    public const int DefaultDiscoveryMaximumResults = 100;
    public const int HardDiscoveryMaximumResults = 500;
    public const int DefaultBrowsePageSize = 200;
    public const int HardBrowsePageSize = 500;

    private readonly IOpcUaEndpointDiscoveryTransport _discoveryTransport;
    private readonly IOpcUaBrowseTransport _browseTransport;

    public OpcUaEngineeringAdapter(IOpcUaEngineeringTransport transport)
        : this(transport, transport)
    {
    }

    public OpcUaEngineeringAdapter(
        IOpcUaEndpointDiscoveryTransport discoveryTransport,
        IOpcUaBrowseTransport browseTransport)
    {
        _discoveryTransport = discoveryTransport ?? throw new ArgumentNullException(nameof(discoveryTransport));
        _browseTransport = browseTransport ?? throw new ArgumentNullException(nameof(browseTransport));
    }

    public CommunicationDriverTypeDescriptor Descriptor => OpcUaDriverDescriptorProvider.Definition;

    public async IAsyncEnumerable<DriverDiscoveryCandidate> DiscoverAsync(
        DriverDiscoveryRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var maximumResults = NormalizeDiscoveryMaximum(request.MaximumResults);
        var discoveryUrl = GetDiscoveryUrl(request);
        var transportRequest = new OpcUaEndpointDiscoveryRequest(
            request.Context,
            discoveryUrl,
            maximumResults,
            request.Parameters);

        var identities = new HashSet<string>(StringComparer.Ordinal);
        var emitted = 0;

        await foreach (var endpoint in _discoveryTransport.DiscoverEndpointsAsync(transportRequest, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(endpoint.EndpointUrl))
            {
                continue;
            }

            var sanitizedEndpoint = SanitizeEndpoint(endpoint.EndpointUrl);
            var stableIdentity = CreateEndpointStableIdentity(endpoint, sanitizedEndpoint);
            if (!identities.Add(stableIdentity))
            {
                continue;
            }

            yield return new DriverDiscoveryCandidate(
                CandidateId: CreateDeterministicId("opcua-endpoint", stableIdentity),
                StableIdentity: stableIdentity,
                DisplayName: GetEndpointDisplayName(endpoint, sanitizedEndpoint),
                SanitizedEndpoint: sanitizedEndpoint,
                SuggestedSettings: BuildSuggestedSettings(endpoint, sanitizedEndpoint),
                Metadata: BuildDiscoveryMetadata(endpoint),
                Issues: endpoint.Issues);

            emitted++;
            if (emitted >= maximumResults)
            {
                yield break;
            }
        }
    }

    public async ValueTask<DriverBrowsePage> BrowseAsync(
        DriverBrowseRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var pageSize = NormalizeBrowsePageSize(request.PageSize);
        var transportRequest = new OpcUaBrowseTransportRequest(
            request.Context,
            request.ParentNodeId,
            request.ContinuationToken,
            pageSize,
            request.Parameters);

        var transportPage = await _browseTransport.BrowseAsync(transportRequest, cancellationToken);
        if (transportPage.Nodes.Count > pageSize)
        {
            throw new InvalidOperationException(
                $"OPC UA transport returned {transportPage.Nodes.Count} browse nodes for a requested page size of {pageSize}.");
        }

        var nodes = new List<DriverBrowseNode>(transportPage.Nodes.Count);
        var pageIssues = transportPage.Issues is null
            ? new List<DriverEngineeringIssue>()
            : new List<DriverEngineeringIssue>(transportPage.Issues);

        foreach (var evidence in transportPage.Nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(evidence.NodeId))
            {
                pageIssues.Add(new DriverEngineeringIssue(
                    "OPCUA_BROWSE_INVALID_NODE",
                    DriverEngineeringIssueSeverity.Warning,
                    "The OPC UA server returned a browse node without a NodeId; the node was skipped."));
                continue;
            }

            var identity = new OpcUaNodeIdentity(evidence.NodeId, evidence.NamespaceUri);
            var nodeIssues = evidence.Issues is null
                ? new List<DriverEngineeringIssue>()
                : new List<DriverEngineeringIssue>(evidence.Issues);

            Scada.Core.Tags.TagDataType? suggestedDataType = null;
            if (evidence.NodeClass == OpcUaBrowseNodeClass.Variable && evidence.BuiltInDataType is not null)
            {
                var mapping = OpcUaDataTypeMapper.Map(evidence.BuiltInDataType.Value, evidence.ValueRank);
                suggestedDataType = mapping.DataType;
                if (!mapping.Supported)
                {
                    nodeIssues.Add(new DriverEngineeringIssue(
                        "OPCUA_BROWSE_UNSUPPORTED_TYPE",
                        DriverEngineeringIssueSeverity.Warning,
                        mapping.Reason ?? "The OPC UA variable type is not supported by the current TAG mapping."));
                }
                else if (mapping.RequiresAdaptation)
                {
                    nodeIssues.Add(new DriverEngineeringIssue(
                        "OPCUA_BROWSE_TYPE_ADAPTATION",
                        DriverEngineeringIssueSeverity.Information,
                        mapping.Reason ?? "The OPC UA variable requires a canonical TAG adaptation."));
                }
            }

            nodes.Add(new DriverBrowseNode(
                NodeId: evidence.NodeId,
                StableIdentity: identity.StableIdentity,
                DisplayName: GetBrowseDisplayName(evidence),
                IsContainer: evidence.NodeClass is OpcUaBrowseNodeClass.Object or OpcUaBrowseNodeClass.View,
                IsReadable: evidence.IsReadable,
                IsWritable: evidence.IsWritable,
                PortableAddress: identity.PortableAddress,
                SuggestedDataType: suggestedDataType,
                EngineeringUnit: evidence.EngineeringUnit,
                Metadata: BuildBrowseMetadata(evidence),
                Issues: nodeIssues));
        }

        return new DriverBrowsePage(
            Nodes: nodes,
            ContinuationToken: transportPage.ContinuationToken,
            IsPartial: transportPage.IsPartial || !string.IsNullOrWhiteSpace(transportPage.ContinuationToken),
            Issues: pageIssues);
    }

    private static int NormalizeDiscoveryMaximum(int? requested)
    {
        if (requested is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requested), "Maximum discovery results must be greater than zero.");
        }

        return Math.Min(requested ?? DefaultDiscoveryMaximumResults, HardDiscoveryMaximumResults);
    }

    private static int NormalizeBrowsePageSize(int? requested)
    {
        if (requested is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requested), "Browse page size must be greater than zero.");
        }

        return Math.Min(requested ?? DefaultBrowsePageSize, HardBrowsePageSize);
    }

    private static string? GetDiscoveryUrl(DriverDiscoveryRequest request)
    {
        if (request.Parameters is not null &&
            request.Parameters.TryGetValue("discoveryUrl", out var discoveryUrl) &&
            !string.IsNullOrWhiteSpace(discoveryUrl))
        {
            return SanitizeEndpoint(discoveryUrl);
        }

        if (request.Context?.Settings.TryGetValue("endpointUrl", out var endpointUrl) == true &&
            !string.IsNullOrWhiteSpace(endpointUrl))
        {
            return SanitizeEndpoint(endpointUrl);
        }

        return null;
    }

    private static IReadOnlyDictionary<string, string> BuildSuggestedSettings(
        OpcUaEndpointDiscoveryEvidence endpoint,
        string sanitizedEndpoint)
    {
        var settings = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["endpointUrl"] = sanitizedEndpoint,
            ["securityMode"] = endpoint.SecurityMode,
            ["securityPolicyUri"] = endpoint.SecurityPolicyUri
        };

        var authenticationMode = SelectAuthenticationMode(endpoint.UserTokenTypes);
        if (authenticationMode is not null)
        {
            settings["authenticationMode"] = authenticationMode;
        }

        return settings;
    }

    private static string? SelectAuthenticationMode(IReadOnlyCollection<string> tokenTypes)
    {
        if (tokenTypes.Any(value => string.Equals(value, "Anonymous", StringComparison.OrdinalIgnoreCase)))
        {
            return "Anonymous";
        }

        if (tokenTypes.Any(value => string.Equals(value, "UserName", StringComparison.OrdinalIgnoreCase)))
        {
            return "UserName";
        }

        if (tokenTypes.Any(value => string.Equals(value, "Certificate", StringComparison.OrdinalIgnoreCase)))
        {
            return "Certificate";
        }

        return null;
    }

    private static IReadOnlyDictionary<string, string> BuildDiscoveryMetadata(OpcUaEndpointDiscoveryEvidence endpoint)
    {
        var metadata = CopyMetadata(endpoint.Metadata);
        AddIfPresent(metadata, "opcUa.applicationUri", endpoint.ApplicationUri);
        AddIfPresent(metadata, "opcUa.productUri", endpoint.ProductUri);
        AddIfPresent(metadata, "opcUa.transportProfileUri", endpoint.TransportProfileUri);
        AddIfPresent(metadata, "opcUa.serverCertificateThumbprint", endpoint.ServerCertificateThumbprint);
        AddIfPresent(metadata, "opcUa.serverCertificateSubject", endpoint.ServerCertificateSubject);
        metadata["opcUa.securityMode"] = endpoint.SecurityMode;
        metadata["opcUa.securityPolicyUri"] = endpoint.SecurityPolicyUri;
        metadata["opcUa.userTokenTypes"] = string.Join(",", endpoint.UserTokenTypes.Order(StringComparer.Ordinal));

        if (endpoint.IsServerCertificateTrusted is not null)
        {
            metadata["opcUa.serverCertificateTrusted"] = endpoint.IsServerCertificateTrusted.Value ? "true" : "false";
        }

        return metadata;
    }

    private static IReadOnlyDictionary<string, string> BuildBrowseMetadata(OpcUaBrowseNodeEvidence evidence)
    {
        var metadata = CopyMetadata(evidence.Metadata);
        metadata["opcUa.nodeClass"] = evidence.NodeClass.ToString();
        metadata["opcUa.browseName"] = evidence.BrowseName;
        metadata["opcUa.valueRank"] = evidence.ValueRank.ToString(CultureInfo.InvariantCulture);
        metadata["opcUa.historizing"] = evidence.IsHistorizing ? "true" : "false";
        AddIfPresent(metadata, "opcUa.namespaceUri", evidence.NamespaceUri);
        AddIfPresent(metadata, "opcUa.browsePath", evidence.BrowsePath);
        AddIfPresent(metadata, "opcUa.description", evidence.Description);

        if (evidence.BuiltInDataType is not null)
        {
            metadata["opcUa.builtInDataType"] = evidence.BuiltInDataType.Value.ToString();
        }

        return metadata;
    }

    private static Dictionary<string, string> CopyMetadata(IReadOnlyDictionary<string, string>? source)
    {
        var copy = new Dictionary<string, string>(StringComparer.Ordinal);
        if (source is null)
        {
            return copy;
        }

        foreach (var pair in source)
        {
            copy[pair.Key] = pair.Value;
        }

        return copy;
    }

    private static void AddIfPresent(IDictionary<string, string> metadata, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            metadata[key] = value;
        }
    }

    private static string CreateEndpointStableIdentity(OpcUaEndpointDiscoveryEvidence endpoint, string sanitizedEndpoint) =>
        string.Join("|",
            endpoint.ApplicationUri?.Trim() ?? string.Empty,
            sanitizedEndpoint,
            endpoint.SecurityMode.Trim(),
            endpoint.SecurityPolicyUri.Trim());

    private static string CreateDeterministicId(string prefix, string stableIdentity)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(stableIdentity));
        return $"{prefix}-{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static string GetEndpointDisplayName(OpcUaEndpointDiscoveryEvidence endpoint, string sanitizedEndpoint) =>
        !string.IsNullOrWhiteSpace(endpoint.ApplicationName)
            ? endpoint.ApplicationName
            : sanitizedEndpoint;

    private static string GetBrowseDisplayName(OpcUaBrowseNodeEvidence evidence) =>
        !string.IsNullOrWhiteSpace(evidence.DisplayName)
            ? evidence.DisplayName
            : !string.IsNullOrWhiteSpace(evidence.BrowseName)
                ? evidence.BrowseName
                : evidence.NodeId;

    private static string SanitizeEndpoint(string endpoint)
    {
        var trimmed = endpoint.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.UserInfo))
        {
            return trimmed;
        }

        var builder = new UriBuilder(uri)
        {
            UserName = string.Empty,
            Password = string.Empty
        };
        return builder.Uri.ToString();
    }
}
