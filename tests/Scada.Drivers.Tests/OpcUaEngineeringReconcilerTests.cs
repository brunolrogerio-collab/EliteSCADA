using Scada.Drivers.Abstractions;
using Scada.Drivers.OpcUa;

namespace Scada.Drivers.Tests;

public sealed class OpcUaEngineeringReconcilerTests
{
    [Fact]
    public async Task NamespaceIndexReorder_IsAddressChangedWithoutIdentityChange()
    {
        var requested = new OpcUaNodeIdentity("ns=2;s=Temperature", "urn:plant");
        var observed = new OpcUaNodeInspectionEvidence(
            requested,
            Exists: true,
            ResolvedNodeId: "ns=5;s=Temperature",
            NamespaceUri: "urn:plant",
            NodeClass: OpcUaBrowseNodeClass.Variable,
            IsReadable: true,
            IsWritable: false,
            BuiltInDataType: OpcUaBuiltInDataType.Double,
            ValueRank: -1);
        var transport = new FakeInspectionTransport([observed]);
        var reconciler = new OpcUaEngineeringReconciler(transport);

        var results = await CollectAsync(reconciler.ReconcileAsync(new DriverReconcileRequest(
            Context(),
            [requested.PortableAddress])));

        var result = Assert.Single(results);
        Assert.Equal(DriverReconcileStatus.AddressChanged, result.Status);
        Assert.Equal(requested.StableIdentity, result.ResolvedIdentity);
        Assert.Equal(new OpcUaNodeIdentity("ns=5;s=Temperature", "urn:plant").PortableAddress, result.ResolvedPortableAddress);
        Assert.Equal(Scada.Core.Tags.TagDataType.Double, result.ObservedDataType);
        Assert.True(result.IsReadable);
        Assert.False(result.IsWritable);
    }

    [Fact]
    public async Task MissingNode_IsReportedMissingWithoutInventingReplacement()
    {
        var requested = new OpcUaNodeIdentity("ns=2;s=Missing", "urn:plant");
        var transport = new FakeInspectionTransport([
            new OpcUaNodeInspectionEvidence(requested, Exists: false)
        ]);
        var reconciler = new OpcUaEngineeringReconciler(transport);

        var result = Assert.Single(await CollectAsync(reconciler.ReconcileAsync(new DriverReconcileRequest(
            Context(),
            [requested.PortableAddress]))));

        Assert.Equal(DriverReconcileStatus.Missing, result.Status);
        Assert.Null(result.ResolvedPortableAddress);
    }

    [Fact]
    public async Task UnsupportedObservedType_IsExplicit()
    {
        var requested = new OpcUaNodeIdentity("ns=2;s=Counter", "urn:plant");
        var transport = new FakeInspectionTransport([
            new OpcUaNodeInspectionEvidence(
                requested,
                Exists: true,
                ResolvedNodeId: requested.NodeId,
                NamespaceUri: requested.NamespaceUri,
                NodeClass: OpcUaBrowseNodeClass.Variable,
                IsReadable: true,
                BuiltInDataType: OpcUaBuiltInDataType.UInt64,
                ValueRank: -1)
        ]);
        var reconciler = new OpcUaEngineeringReconciler(transport);

        var result = Assert.Single(await CollectAsync(reconciler.ReconcileAsync(new DriverReconcileRequest(
            Context(),
            [requested.PortableAddress]))));

        Assert.Equal(DriverReconcileStatus.Unsupported, result.Status);
        Assert.Null(result.ObservedDataType);
        Assert.Contains(result.Issues ?? [], issue => issue.Code == "OPCUA_RECONCILE_TYPE_UNSUPPORTED");
    }

    [Fact]
    public async Task DuplicateStableAddresses_AreInspectedOnceButReturnedInInputOrder()
    {
        var requested = new OpcUaNodeIdentity("ns=2;s=Value", "urn:plant");
        var transport = new FakeInspectionTransport([
            new OpcUaNodeInspectionEvidence(
                requested,
                Exists: true,
                ResolvedNodeId: requested.NodeId,
                NamespaceUri: requested.NamespaceUri,
                NodeClass: OpcUaBrowseNodeClass.Variable,
                IsReadable: true,
                BuiltInDataType: OpcUaBuiltInDataType.Int32,
                ValueRank: -1)
        ]);
        var reconciler = new OpcUaEngineeringReconciler(transport);

        var results = await CollectAsync(reconciler.ReconcileAsync(new DriverReconcileRequest(
            Context(),
            [requested.PortableAddress, requested.PortableAddress])));

        Assert.Equal(2, results.Count);
        Assert.Equal(1, transport.LastRequestedNodes.Count);
        Assert.All(results, result => Assert.Equal(DriverReconcileStatus.Unchanged, result.Status));
    }

    [Fact]
    public async Task InvalidPortableAddress_IsReturnedAsErrorWithoutTransportCall()
    {
        var transport = new FakeInspectionTransport([]);
        var reconciler = new OpcUaEngineeringReconciler(transport);

        var result = Assert.Single(await CollectAsync(reconciler.ReconcileAsync(new DriverReconcileRequest(
            Context(),
            ["not-a-portable-address"]))));

        Assert.Equal(DriverReconcileStatus.Error, result.Status);
        Assert.Empty(transport.LastRequestedNodes);
    }

    private static DriverEngineeringDataSourceContext Context() => new(
        "opc-1",
        "OPC 1",
        OpcUaDriverDescriptorProvider.DriverTypeId,
        new Dictionary<string, string>(),
        new Dictionary<string, string>());

    private static async Task<List<DriverReconcileResult>> CollectAsync(
        IAsyncEnumerable<DriverReconcileResult> source)
    {
        var result = new List<DriverReconcileResult>();
        await foreach (var item in source) result.Add(item);
        return result;
    }

    private sealed class FakeInspectionTransport(
        IReadOnlyCollection<OpcUaNodeInspectionEvidence> evidence) : IOpcUaNodeInspectionTransport
    {
        public IReadOnlyCollection<OpcUaNodeIdentity> LastRequestedNodes { get; private set; } = [];

        public ValueTask<IReadOnlyCollection<OpcUaNodeInspectionEvidence>> InspectAsync(
            DriverEngineeringDataSourceContext context,
            IReadOnlyCollection<OpcUaNodeIdentity> nodes,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequestedNodes = nodes.ToArray();
            return ValueTask.FromResult(evidence);
        }
    }
}
