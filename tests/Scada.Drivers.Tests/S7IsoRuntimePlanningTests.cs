using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.Drivers.SiemensS7Iso;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class S7IsoRuntimePlanningTests
{
    [Fact]
    public void Planner_ProducesLibraryIndependentPlanFromCanonicalEngineering()
    {
        var dataSource = DataSource();
        var binding = new S7IsoTagBinding(
            S7IsoTagBinding.CurrentSchemaVersion,
            S7IsoArea.DataBlock,
            10,
            S7IsoValueType.Int16,
            DbNumber: 3,
            Writable: true);
        var tag = new TagEngineeringDto(
            Id: Guid.NewGuid(),
            Name: "Setpoint",
            Path: "Plant.P01.Setpoint",
            DataType: TagDataType.Int16,
            Source: dataSource.Key,
            Address: binding.ToPortableAddress(),
            ReadOnly: false);

        var result = new S7IsoRuntimePlanner().Plan(Package(new[] { tag }, new[] { dataSource }), dataSource);

        Assert.True(result.CanActivate);
        Assert.DoesNotContain(result.Issues, issue => issue.IsError);
        var plan = Assert.IsType<S7IsoRuntimePlan>(result.Plan);
        Assert.Equal(dataSource.Key, plan.DataSourceKey);
        Assert.Equal(dataSource.Name, plan.Name);
        Assert.Equal(S7IsoRuntimePlanner.DriverTypeKey, plan.DriverType);
        Assert.Equal("127.0.0.1", plan.Options.Host);
        Assert.Equal((ushort)480, plan.Options.RequestedPduSize);
        var point = Assert.Single(plan.Points);
        Assert.Equal(tag.Id, point.Tag.Id);
        Assert.Equal(S7IsoArea.DataBlock, point.Area);
        Assert.Equal((ushort)3, point.DbNumber);
        Assert.Equal(10, point.ByteOffset);
        Assert.Equal(S7IsoValueType.Int16, point.ValueType);
        Assert.True(point.Writable);
        Assert.Single(plan.Tags);
    }

    [Fact]
    public void Planner_RejectsInvalidBindingWithoutCreatingPartialRuntimePlan()
    {
        var dataSource = DataSource();
        var tag = new TagEngineeringDto(
            Id: Guid.NewGuid(),
            Name: "Invalid",
            Path: "Plant.Invalid",
            DataType: TagDataType.Int16,
            Source: dataSource.Key,
            Address: "s7iso:v1;area=DataBlock;db=1;byte=10;bit=0;type=Nope;string=0;writable=false;order=Normal");

        var result = new S7IsoRuntimePlanner().Plan(Package(new[] { tag }, new[] { dataSource }), dataSource);

        Assert.False(result.CanActivate);
        Assert.Null(result.Plan);
        var issue = Assert.Single(result.Issues, issue => issue.Code == "S7_TAG_BINDING_INVALID");
        Assert.True(issue.IsError);
        Assert.Equal(dataSource.Key, issue.DataSourceKey);
        Assert.Equal(tag.Path, issue.TagPath);
    }

    [Fact]
    public async Task Factory_CreatesSiemensDriverWithoutCentralRuntimeRegistration()
    {
        var dataSource = DataSource();
        var binding = new S7IsoTagBinding(
            S7IsoTagBinding.CurrentSchemaVersion,
            S7IsoArea.Merker,
            4,
            S7IsoValueType.Int32);
        var tag = new TagEngineeringDto(
            Id: Guid.NewGuid(),
            Name: "Counter",
            Path: "Plant.Counter",
            DataType: TagDataType.Int32,
            Source: dataSource.Key,
            Address: binding.ToPortableAddress());
        var planning = new S7IsoRuntimePlanner().Plan(Package(new[] { tag }, new[] { dataSource }), dataSource);
        var plan = Assert.IsType<S7IsoRuntimePlan>(planning.Plan);
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();

        await using var driver = new S7IsoRuntimeFactory().Create(plan, cache, registry);

        var s7 = Assert.IsType<S7IsoDriver>(driver);
        Assert.Equal(dataSource.Key, s7.DriverId);
        Assert.Equal(dataSource.Name, s7.Name);
        Assert.Equal(S7IsoRuntimePlanner.DriverTypeKey, new S7IsoRuntimeFactory().DriverType);
    }

    private static DataSourceEngineeringDto DataSource() => new(
        Id: Guid.NewGuid(),
        Key: "plc.s7.main",
        Name: "Main S7 PLC",
        Driver: S7IsoRuntimePlanner.DriverTypeKey,
        Settings: new Dictionary<string, string>
        {
            ["host"] = "127.0.0.1",
            ["cpuFamily"] = nameof(S7CpuFamily.S71500),
            ["connectionMode"] = nameof(S7IsoConnectionMode.RackSlot),
            ["rack"] = "0",
            ["slot"] = "1",
            ["connectionRole"] = nameof(S7IsoConnectionRole.Basic),
            ["requestedPduSize"] = "480"
        });

    private static EngineeringPackage Package(
        IReadOnlyCollection<TagEngineeringDto> tags,
        IReadOnlyCollection<DataSourceEngineeringDto> dataSources) => new(
            Schema: "scada.engineering",
            SchemaVersion: 5,
            ExportedAt: DateTimeOffset.UtcNow,
            Tags: tags,
            Alarms: Array.Empty<AlarmEngineeringDto>(),
            DataSources: dataSources);
}
