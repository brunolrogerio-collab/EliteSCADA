using Scada.Drivers.OpcUa;

namespace Scada.Drivers.Tests;

public sealed class OpcUaImportPlannerTests
{
    [Fact]
    public void Build_DeduplicatesStableNodeIdentity()
    {
        var plan = OpcUaImportPlanner.Build(new[]
        {
            Create("ns=2;s=Motor.Speed"),
            Create("ns=9;s=Motor.Speed")
        });

        Assert.Single(plan.Candidates);
        Assert.Equal(1, plan.DuplicateCount);
    }

    [Fact]
    public void Build_EnforcesConfiguredLimit()
    {
        var plan = OpcUaImportPlanner.Build(new[]
        {
            Create("ns=2;i=1"),
            Create("ns=2;i=2"),
            Create("ns=2;i=3")
        }, maximumCandidates: 2);

        Assert.Equal(2, plan.ExaminedCount);
        Assert.Equal(2, plan.Candidates.Count);
        Assert.True(plan.IsTruncated);
    }

    private static OpcUaVariableImportEvidence Create(string nodeId) => new(
        NodeId: nodeId,
        NamespaceUri: "urn:elite:line-a",
        BrowseName: nodeId,
        DisplayName: nodeId,
        BuiltInDataType: OpcUaBuiltInDataType.Int32,
        ValueRank: OpcUaDataTypeMapper.ScalarValueRank,
        IsReadable: true,
        IsWritable: false);
}
