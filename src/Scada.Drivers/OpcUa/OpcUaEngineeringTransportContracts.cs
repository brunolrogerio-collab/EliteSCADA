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
/// Discovery-only seam. Implementations may enumerate endpoints without opening
/// an authenticated OPC UA Session and without depending on browse/session state.
/// </summary>
public interface IOpcUaEndpointDiscoveryTransport
{
    IAsyncEnumerable<OpcUaEndpointDiscoveryEvidence> DiscoverEndpointsAsync(
        OpcUaEndpointDiscoveryRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Browse-only seam. Implementations may require a protected temporary session,
/// but SDK session and continuation-point types stay private to the transport.
/// </summary>
public interface IOpcUaBrowseTransport
{
    ValueTask<OpcUaBrowseTransportPage> BrowseAsync(
        OpcUaBrowseTransportRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Convenience aggregate for transports that provide both discovery and browse.
/// Consumers should depend on the narrower interfaces whenever only one capability is needed.
/// </summary>
public interface IOpcUaEngineeringTransport :
    IOpcUaEndpointDiscoveryTransport,
    IOpcUaBrowseTransport
{
}
