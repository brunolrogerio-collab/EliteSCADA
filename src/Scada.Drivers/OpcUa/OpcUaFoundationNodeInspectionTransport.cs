using System.Globalization;
using Opc.Ua;
using Opc.Ua.Client;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.OpcUa;

/// <summary>
/// Secure Foundation-backed inspection of known OPC UA nodes for Engineering reconciliation.
/// It performs no browse expansion and never follows remote-server references.
/// </summary>
public sealed class OpcUaFoundationNodeInspectionTransport : IOpcUaNodeInspectionTransport
{
    private const int HardMaximumNodes = 5000;
    private const int BatchSize = 50;
    private readonly IOpcUaRuntimeSecurityMaterialProvider _securityMaterialProvider;

    public OpcUaFoundationNodeInspectionTransport(
        IOpcUaRuntimeSecurityMaterialProvider securityMaterialProvider)
    {
        _securityMaterialProvider = securityMaterialProvider ??
            throw new ArgumentNullException(nameof(securityMaterialProvider));
    }

    public async ValueTask<IReadOnlyCollection<OpcUaNodeInspectionEvidence>> InspectAsync(
        DriverEngineeringDataSourceContext context,
        IReadOnlyCollection<OpcUaNodeIdentity> nodes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(nodes);
        cancellationToken.ThrowIfCancellationRequested();

        if (nodes.Count == 0) return Array.Empty<OpcUaNodeInspectionEvidence>();
        if (nodes.Count > HardMaximumNodes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nodes),
                $"OPC UA node inspection is bounded to {HardMaximumNodes} nodes per request.");
        }

        OpcUaRuntimeConnectionOptions options = OpcUaRuntimeDriverComposer.ParseConnectionOptions(context);
        TagDefinition probeTag = TagDefinition.Create(
            "InspectionProbe",
            $"__engineering.opcua.{Guid.NewGuid():N}.InspectionProbe",
            TagDataType.String,
            source: context.DataSourceKey,
            readOnly: true,
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [OpcUaRuntimeBinding.NodeIdMetadataKey] = "i=85"
            });

        var factory = new OpcUaFoundationRuntimeSessionFactory(options, _securityMaterialProvider);
        await using IOpcUaRuntimeSession runtimeSession = await factory
            .ConnectAsync([OpcUaRuntimeBinding.FromTag(probeTag)], cancellationToken)
            .ConfigureAwait(false);
        ISession session = runtimeSession is IOpcUaFoundationSessionAccessor accessor
            ? accessor.FoundationSession
            : throw new InvalidOperationException(
                "OPC UA Foundation node inspection requires a Foundation-backed runtime session.");

        NodeDraft[] drafts = nodes.Select(node => new NodeDraft(node)).ToArray();
        foreach (NodeDraft draft in drafts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                draft.ResolvedNodeId = NodeId.Parse(
                    OpcUaRuntimeProtocolSupport.ResolveSessionNodeId(
                        draft.RequestedIdentity,
                        namespaceUri => session.NamespaceUris.GetIndex(namespaceUri)));
                draft.NamespaceUri = session.NamespaceUris.GetString(draft.ResolvedNodeId.NamespaceIndex);
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException)
            {
                draft.ResolutionIssue = new DriverEngineeringIssue(
                    "OPCUA_RECONCILE_NODE_UNRESOLVED",
                    DriverEngineeringIssueSeverity.Warning,
                    "The OPC UA node identity could not be resolved against the active server namespace table.");
            }
        }

        foreach (NodeDraft[] batch in drafts
            .Where(draft => draft.ResolvedNodeId is not null)
            .Chunk(BatchSize))
        {
            try
            {
                await InspectBatchAsync(session, batch, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                foreach (NodeDraft draft in batch)
                {
                    draft.InspectionIssue = new DriverEngineeringIssue(
                        "OPCUA_RECONCILE_ATTRIBUTE_READ_FAILED",
                        DriverEngineeringIssueSeverity.Error,
                        "The OPC UA server failed one bounded node-inspection attribute batch.");
                }
            }
        }

        return drafts.Select(draft => draft.ToEvidence()).ToArray();
    }

    private static async Task InspectBatchAsync(
        ISession session,
        IReadOnlyList<NodeDraft> batch,
        CancellationToken cancellationToken)
    {
        const int AttributesPerNode = 5;
        var reads = new ReadValueIdCollection();
        foreach (NodeDraft draft in batch)
        {
            NodeId nodeId = draft.ResolvedNodeId!;
            reads.Add(new ReadValueId { NodeId = nodeId, AttributeId = Attributes.NodeClass });
            reads.Add(new ReadValueId { NodeId = nodeId, AttributeId = Attributes.UserAccessLevel });
            reads.Add(new ReadValueId { NodeId = nodeId, AttributeId = Attributes.AccessLevel });
            reads.Add(new ReadValueId { NodeId = nodeId, AttributeId = Attributes.DataType });
            reads.Add(new ReadValueId { NodeId = nodeId, AttributeId = Attributes.ValueRank });
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
            throw new InvalidOperationException("OPC UA node inspection returned an invalid result count.");
        }

        for (int index = 0; index < batch.Count; index++)
        {
            NodeDraft draft = batch[index];
            int offset = index * AttributesPerNode;
            DataValue nodeClass = response.Results[offset];
            DataValue userAccess = response.Results[offset + 1];
            DataValue access = response.Results[offset + 2];
            DataValue dataType = response.Results[offset + 3];
            DataValue valueRank = response.Results[offset + 4];

            if (!TryReadInt32(nodeClass, out int rawNodeClass))
            {
                draft.Exists = false;
                draft.InspectionIssue = new DriverEngineeringIssue(
                    "OPCUA_RECONCILE_NODE_MISSING",
                    DriverEngineeringIssueSeverity.Warning,
                    $"The OPC UA server did not return a readable NodeClass for the requested node (status '{nodeClass.StatusCode}').");
                continue;
            }

            draft.Exists = true;
            draft.NodeClass = MapNodeClass((NodeClass)rawNodeClass);

            byte? accessLevel = TryReadByte(userAccess) ?? TryReadByte(access);
            if (accessLevel.HasValue && draft.NodeClass == OpcUaBrowseNodeClass.Variable)
            {
                draft.IsReadable = (accessLevel.Value & 0x01) != 0;
                draft.IsWritable = (accessLevel.Value & 0x02) != 0;
            }

            if (TryReadValue<NodeId>(dataType, out NodeId? typeNodeId) && typeNodeId is not null)
            {
                draft.BuiltInDataType = MapBuiltInDataType(typeNodeId);
            }

            if (TryReadInt32(valueRank, out int rank))
            {
                draft.ValueRank = rank;
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

    private static OpcUaBuiltInDataType MapBuiltInDataType(NodeId dataType) =>
        dataType switch
        {
            _ when dataType == DataTypeIds.Boolean => OpcUaBuiltInDataType.Boolean,
            _ when dataType == DataTypeIds.SByte => OpcUaBuiltInDataType.SByte,
            _ when dataType == DataTypeIds.Byte => OpcUaBuiltInDataType.Byte,
            _ when dataType == DataTypeIds.Int16 => OpcUaBuiltInDataType.Int16,
            _ when dataType == DataTypeIds.UInt16 => OpcUaBuiltInDataType.UInt16,
            _ when dataType == DataTypeIds.Int32 => OpcUaBuiltInDataType.Int32,
            _ when dataType == DataTypeIds.UInt32 => OpcUaBuiltInDataType.UInt32,
            _ when dataType == DataTypeIds.Int64 => OpcUaBuiltInDataType.Int64,
            _ when dataType == DataTypeIds.UInt64 => OpcUaBuiltInDataType.UInt64,
            _ when dataType == DataTypeIds.Float => OpcUaBuiltInDataType.Float,
            _ when dataType == DataTypeIds.Double => OpcUaBuiltInDataType.Double,
            _ when dataType == DataTypeIds.String => OpcUaBuiltInDataType.String,
            _ when dataType == DataTypeIds.DateTime => OpcUaBuiltInDataType.DateTime,
            _ when dataType == DataTypeIds.Guid => OpcUaBuiltInDataType.Guid,
            _ when dataType == DataTypeIds.ByteString => OpcUaBuiltInDataType.ByteString,
            _ when dataType == DataTypeIds.NodeId => OpcUaBuiltInDataType.NodeId,
            _ when dataType == DataTypeIds.ExpandedNodeId => OpcUaBuiltInDataType.ExpandedNodeId,
            _ when dataType == DataTypeIds.QualifiedName => OpcUaBuiltInDataType.QualifiedName,
            _ when dataType == DataTypeIds.LocalizedText => OpcUaBuiltInDataType.LocalizedText,
            _ when dataType == DataTypeIds.XmlElement => OpcUaBuiltInDataType.XmlElement,
            _ when dataType == DataTypeIds.StatusCode => OpcUaBuiltInDataType.StatusCode,
            _ when dataType == DataTypeIds.Structure => OpcUaBuiltInDataType.Structure,
            _ => OpcUaBuiltInDataType.Variant
        };

    private sealed class NodeDraft(OpcUaNodeIdentity requestedIdentity)
    {
        public OpcUaNodeIdentity RequestedIdentity { get; } = requestedIdentity;
        public NodeId? ResolvedNodeId { get; set; }
        public string? NamespaceUri { get; set; }
        public bool Exists { get; set; }
        public OpcUaBrowseNodeClass NodeClass { get; set; } = OpcUaBrowseNodeClass.Other;
        public bool IsReadable { get; set; }
        public bool IsWritable { get; set; }
        public OpcUaBuiltInDataType? BuiltInDataType { get; set; }
        public int ValueRank { get; set; } = -1;
        public DriverEngineeringIssue? ResolutionIssue { get; set; }
        public DriverEngineeringIssue? InspectionIssue { get; set; }

        public OpcUaNodeInspectionEvidence ToEvidence()
        {
            var issues = new List<DriverEngineeringIssue>();
            if (ResolutionIssue is not null) issues.Add(ResolutionIssue);
            if (InspectionIssue is not null) issues.Add(InspectionIssue);

            return new OpcUaNodeInspectionEvidence(
                RequestedIdentity,
                Exists,
                ResolvedNodeId?.ToString(),
                NamespaceUri,
                NodeClass,
                IsReadable,
                IsWritable,
                BuiltInDataType,
                ValueRank,
                Metadata: null,
                Issues: issues);
        }
    }
}
