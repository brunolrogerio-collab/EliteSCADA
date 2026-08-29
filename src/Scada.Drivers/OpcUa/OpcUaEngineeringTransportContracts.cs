using Scada.Drivers.Abstractions;

namespace Scada.Drivers.OpcUa;

public enum OpcUaBrowseNodeClass
{
    Object,
    Variable,
    Method,
    View,
    ObjectType,
    VariableType,
    ReferenceType,
    DataType,
    Other
}

/// <summary>
/// Library-independent endpoint evidence returned by the OPC UA transport.
/// Raw certificates and resolved secrets deliberately stay behind the transport boundary.
/// </summary>
public sealed record OpcUaEndpointDiscoveryEvidence(
    string EndpointUrl,
    string? ApplicationUri,
    string? ApplicationName,
    string? ProductUri,
    string? TransportProfileUri,
    string SecurityMode,
    string SecurityPolicyUri,
    IReadOnlyCollection<string> UserTokenTypes,
    string? ServerCertificateThumbprint = null,
    string? ServerCertificateSubject = null,
    bool? IsServerCertificateTrusted = null,
    IReadOnlyDictionary<string, string>? Metadata = null,
    IReadOnlyCollection<DriverEngineeringIssue>? Issues = null);

public sealed record OpcUaEndpointDiscoveryRequest(
    DriverEngineeringDataSourceContext? Context,
    string? DiscoveryUrl,
    int MaximumResults,
    IReadOnlyDictionary<string, string>? Parameters = null);

/// <summary>
/// One lazy browse node as observed from the server. BrowsePath is optional but,
/// when available, should be namespace-aware and portable enough for later reconcile.
/// </summary>
public sealed record OpcUaBrowseNodeEvidence(
    string NodeId,
    string? NamespaceUri,
    string BrowseName,
    string DisplayName,
    OpcUaBrowseNodeClass NodeClass,
    bool IsReadable,
    bool IsWritable,
    bool IsHistorizing = false,
    OpcUaBuiltInDataType? BuiltInDataType = null,
    int ValueRank = -1,
    string? BrowsePath = null,
    string? Description = null,
    string? EngineeringUnit = null,
    IReadOnlyDictionary<string, string>? Metadata = null,
    IReadOnlyCollection<DriverEngineeringIssue>? Issues = null);

public sealed record OpcUaBrowseTransportRequest(
    DriverEngineeringDataSourceContext Context,
    string? ParentNodeId,
    string? ContinuationToken,
    int PageSize,
    IReadOnlyDictionary<string, string>? Parameters = null);

public sealed record OpcUaBrowseTransportPage(
    IReadOnlyCollection<OpcUaBrowseNodeEvidence> Nodes,
    string? ContinuationToken = null,
    bool IsPartial = false,
    IReadOnlyCollection<DriverEngineeringIssue>? Issues = null);

/// <summary>
/// Narrow transport seam used by Engineering. The official OPC Foundation client
/// stack will live behind this interface so its session, endpoint and continuation
/// point types never become public EliteSCADA contracts.
/// </summary>
public interface IOpcUaEngineeringTransport
{
    IAsyncEnumerable<OpcUaEndpointDiscoveryEvidence> DiscoverEndpointsAsync(
        OpcUaEndpointDiscoveryRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<OpcUaBrowseTransportPage> BrowseAsync(
        OpcUaBrowseTransportRequest request,
        CancellationToken cancellationToken = default);
}
