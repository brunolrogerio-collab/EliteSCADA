using Scada.Drivers.Abstractions;

namespace Scada.Drivers.OpcUa;

/// <summary>
/// Library-independent evidence returned by an OPC UA node inspection. It is used by
/// Engineering reconciliation and deliberately contains no SDK node/session objects.
/// </summary>
public sealed record OpcUaNodeInspectionEvidence(
    OpcUaNodeIdentity RequestedIdentity,
    bool Exists,
    string? ResolvedNodeId = null,
    string? NamespaceUri = null,
    OpcUaBrowseNodeClass NodeClass = OpcUaBrowseNodeClass.Other,
    bool IsReadable = false,
    bool IsWritable = false,
    OpcUaBuiltInDataType? BuiltInDataType = null,
    int ValueRank = -1,
    IReadOnlyDictionary<string, string>? Metadata = null,
    IReadOnlyCollection<DriverEngineeringIssue>? Issues = null)
{
    public OpcUaNodeIdentity? ResolvedIdentity =>
        Exists && !string.IsNullOrWhiteSpace(ResolvedNodeId)
            ? new OpcUaNodeIdentity(ResolvedNodeId, NamespaceUri)
            : null;
}

public interface IOpcUaNodeInspectionTransport
{
    ValueTask<IReadOnlyCollection<OpcUaNodeInspectionEvidence>> InspectAsync(
        DriverEngineeringDataSourceContext context,
        IReadOnlyCollection<OpcUaNodeIdentity> nodes,
        CancellationToken cancellationToken = default);
}
