using Scada.Core.Tags;

namespace Scada.Drivers.Abstractions;

/// <summary>
/// Engineering-only capabilities exposed by a driver type. These capabilities
/// are intentionally separate from the active runtime driver so discovery,
/// browse and import tooling cannot become a second source of runtime truth.
/// </summary>
[Flags]
public enum DriverEngineeringCapabilities
{
    None = 0,
    ConnectionTest = 1 << 0,
    Discover = 1 << 1,
    Browse = 1 << 2,
    FileImport = 1 << 3,
    Reconcile = 1 << 4
}

/// <summary>
/// High-level acquisition models a driver type can use internally. The host
/// consumes resulting TAG values through the common TAG/cache/event boundary;
/// these values never expose protocol-library subscription objects publicly.
/// </summary>
public enum DriverAcquisitionMode
{
    Polling,
    Subscription,
    EventDriven,
    Hybrid
}

public enum DriverConfigurationValueKind
{
    String,
    Boolean,
    Integer,
    Number,
    Duration,
    Host,
    Port,
    Identifier,
    Enum,
    SecretReference,
    CertificateReference
}

public enum DriverEngineeringIssueSeverity
{
    Information,
    Warning,
    Error
}

/// <summary>
/// Public description of one driver-owned configuration field. A concrete
/// protocol library may use richer internal types, but canonical Engineering
/// remains library-independent and versioned through this schema identity.
/// </summary>
public sealed record DriverConfigurationFieldDescriptor(
    string Key,
    DriverConfigurationValueKind ValueKind,
    bool Required = false,
    string? DisplayName = null,
    string? Description = null,
    string? DefaultValue = null,
    IReadOnlyCollection<string>? AllowedValues = null,
    double? Minimum = null,
    double? Maximum = null,
    bool Advanced = false);

public sealed record DriverConfigurationSchemaDescriptor(
    string SchemaId,
    int SchemaVersion,
    IReadOnlyCollection<DriverConfigurationFieldDescriptor> DataSourceFields,
    IReadOnlyCollection<DriverConfigurationFieldDescriptor> TagBindingFields);

/// <summary>
/// Stable public identity/capability declaration for one Driver type. The
/// descriptor is owned by EliteSCADA contracts, not by MQTTnet, OPC Foundation,
/// libplctag, S7.NetPlus or another implementation library.
/// </summary>
public sealed record CommunicationDriverTypeDescriptor(
    string DriverType,
    string DisplayName,
    int DriverContractVersion,
    DriverCapabilities RuntimeCapabilities,
    DriverEngineeringCapabilities EngineeringCapabilities,
    IReadOnlyCollection<DriverAcquisitionMode> AcquisitionModes,
    DriverConfigurationSchemaDescriptor ConfigurationSchema,
    bool SupportsSharedTransportInfrastructure = false,
    string? Description = null);

/// <summary>
/// Non-authoritative snapshot passed to protected Engineering tooling. Settings
/// and secret references originate from canonical Engineering; resolved secret
/// values are deliberately absent from this contract.
/// </summary>
public sealed record DriverEngineeringDataSourceContext(
    string DataSourceKey,
    string DataSourceName,
    string DriverType,
    IReadOnlyDictionary<string, string> Settings,
    IReadOnlyDictionary<string, string> SecretReferences);

public sealed record DriverEngineeringIssue(
    string Code,
    DriverEngineeringIssueSeverity Severity,
    string Message,
    string? FieldKey = null);

public sealed record DriverConnectionTestResult(
    bool Succeeded,
    string? SanitizedEndpoint,
    string? ObservedIdentity,
    IReadOnlyDictionary<string, string>? ObservedProperties = null,
    IReadOnlyCollection<DriverEngineeringIssue>? Issues = null);

public sealed record DriverDiscoveryRequest(
    DriverEngineeringDataSourceContext? Context = null,
    IReadOnlyDictionary<string, string>? Parameters = null,
    int? MaximumResults = null);

/// <summary>
/// A transient discovery result. SuggestedSettings may help construct a Data
/// Source candidate, but the result never mutates canonical Engineering by
/// itself and never carries resolved secrets.
/// </summary>
public sealed record DriverDiscoveryCandidate(
    string CandidateId,
    string StableIdentity,
    string DisplayName,
    string? SanitizedEndpoint = null,
    IReadOnlyDictionary<string, string>? SuggestedSettings = null,
    IReadOnlyDictionary<string, string>? Metadata = null,
    IReadOnlyCollection<DriverEngineeringIssue>? Issues = null);

public sealed record DriverBrowseRequest(
    DriverEngineeringDataSourceContext Context,
    string? ParentNodeId = null,
    string? ContinuationToken = null,
    int? PageSize = null,
    IReadOnlyDictionary<string, string>? Parameters = null);

/// <summary>
/// Transient browse/import evidence. PortableAddress is the protocol-owned,
/// library-independent identity that may later become a canonical TAG binding
/// only through normal validate/preview/apply processing.
/// </summary>
public sealed record DriverBrowseNode(
    string NodeId,
    string StableIdentity,
    string DisplayName,
    bool IsContainer,
    bool IsReadable,
    bool IsWritable,
    string? PortableAddress = null,
    TagDataType? SuggestedDataType = null,
    string? EngineeringUnit = null,
    IReadOnlyDictionary<string, string>? Metadata = null,
    IReadOnlyCollection<DriverEngineeringIssue>? Issues = null);

public sealed record DriverBrowsePage(
    IReadOnlyCollection<DriverBrowseNode> Nodes,
    string? ContinuationToken = null,
    bool IsPartial = false,
    IReadOnlyCollection<DriverEngineeringIssue>? Issues = null);

/// <summary>
/// Optional Engineering adapter for communication Driver types. Runtime driver
/// instances must not implement discovery/browse merely to satisfy this API;
/// tooling may use a short-lived protected protocol session instead.
/// </summary>
public interface ICommunicationDriverEngineeringAdapter
{
    CommunicationDriverTypeDescriptor Descriptor { get; }

    ValueTask<DriverConnectionTestResult> TestConnectionAsync(
        DriverEngineeringDataSourceContext context,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<DriverDiscoveryCandidate> DiscoverAsync(
        DriverDiscoveryRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<DriverBrowsePage> BrowseAsync(
        DriverBrowseRequest request,
        CancellationToken cancellationToken = default);
}
