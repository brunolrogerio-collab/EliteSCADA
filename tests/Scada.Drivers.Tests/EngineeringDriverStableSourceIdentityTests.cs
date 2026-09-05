using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class EngineeringDriverStableSourceIdentityTests
{
    [Fact]
    public void Modbus_compilation_follows_stable_Source_id_after_key_rename()
    {
        var sourceId = Guid.NewGuid();
        var dataSource = new DataSourceEngineeringDto(
            Id: sourceId,
            Key: "plc.renamed",
            Name: "Renamed PLC",
            Driver: EngineeringDriverCompiler.ModbusTcpDriverKey,
            Settings: new Dictionary<string, string> { ["host"] = "127.0.0.1" });
        var tag = new TagEngineeringDto(
            Id: Guid.NewGuid(),
            Name: "Pressure",
            Path: "Plant.Pressure",
            DataType: TagDataType.Int16,
            Source: "plc.old-key",
            Address: "holding:0",
            DataSourceId: sourceId);

        var result = new EngineeringDriverCompiler().Compile(Package([tag], [dataSource]));

        Assert.True(result.CanActivate);
        var plan = Assert.Single(result.ModbusTcpPlans);
        var point = Assert.Single(plan.Points);
        Assert.Equal(dataSource.Key, plan.DataSourceKey);
        Assert.Equal(sourceId, point.Tag.DataSourceId);
        Assert.Equal(tag.Path, point.Tag.Path);
    }

    [Fact]
    public void Orphaned_stable_Source_id_never_rebinds_to_same_legacy_key()
    {
        var replacement = new DataSourceEngineeringDto(
            Id: Guid.NewGuid(),
            Key: "plc.old-key",
            Name: "Replacement PLC",
            Driver: EngineeringDriverCompiler.ModbusTcpDriverKey,
            Settings: new Dictionary<string, string> { ["host"] = "127.0.0.1" });
        var tag = new TagEngineeringDto(
            Id: Guid.NewGuid(),
            Name: "Pressure",
            Path: "Plant.Pressure",
            DataType: TagDataType.Int16,
            Source: replacement.Key,
            Address: "holding:0",
            DataSourceId: Guid.NewGuid());

        var result = new EngineeringDriverCompiler().Compile(Package([tag], [replacement]));

        Assert.True(result.CanActivate);
        var plan = Assert.Single(result.ModbusTcpPlans);
        Assert.Empty(plan.Points);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "MODBUS_DATASOURCE_NO_TAGS" && !issue.IsError);
    }

    [Fact]
    public void Legacy_key_only_TAG_still_compiles_for_backward_compatibility()
    {
        var dataSource = new DataSourceEngineeringDto(
            Id: Guid.NewGuid(),
            Key: "plc.legacy",
            Name: "Legacy PLC",
            Driver: EngineeringDriverCompiler.ModbusTcpDriverKey,
            Settings: new Dictionary<string, string> { ["host"] = "127.0.0.1" });
        var tag = new TagEngineeringDto(
            Id: Guid.NewGuid(),
            Name: "Pressure",
            Path: "Plant.Pressure",
            DataType: TagDataType.Int16,
            Source: dataSource.Key,
            Address: "holding:0");

        var result = new EngineeringDriverCompiler().Compile(Package([tag], [dataSource]));

        Assert.True(result.CanActivate);
        Assert.Single(Assert.Single(result.ModbusTcpPlans).Points);
    }

    private static EngineeringPackage Package(
        IReadOnlyCollection<TagEngineeringDto> tags,
        IReadOnlyCollection<DataSourceEngineeringDto> dataSources) => new(
            Schema: "scada.engineering",
            SchemaVersion: 15,
            ExportedAt: DateTimeOffset.UtcNow,
            Tags: tags,
            Alarms: Array.Empty<AlarmEngineeringDto>(),
            DataSources: dataSources);
}
