using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.ImportExport;

namespace Scada.Drivers.Tests;

public sealed class TagDataSourceIdentityTests
{
    [Fact]
    public void Legacy_source_key_is_migrated_to_stable_data_source_identity_on_apply()
    {
        var sourceId = Guid.NewGuid();
        var (service, tags, _) = CreateService();
        var package = Package(
            new TagEngineeringDto(
                Guid.NewGuid(),
                "Pressure",
                "Area.Pressure",
                TagDataType.Double,
                Source: "plant.modbus",
                Address: "40001"),
            new DataSourceEngineeringDto(
                sourceId,
                "plant.modbus",
                "Plant Modbus",
                "modbus.tcp"));

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);
        Assert.True(preview.CanApply);

        var result = service.Apply(package, ImportMode.CreateAndUpdate);
        Assert.Empty(result.Issues);
        Assert.True(tags.TryGetByPath("Area.Pressure", out var tag));
        Assert.NotNull(tag);
        Assert.Equal(sourceId, tag!.DataSourceId);
        Assert.Equal("plant.modbus", tag.Source);

        var exported = Assert.Single(service.ExportPackage().Tags);
        Assert.Equal(sourceId, exported.DataSourceId);
        Assert.Equal("plant.modbus", exported.Source);
    }

    [Fact]
    public void Stable_identity_preserves_tag_reference_when_data_source_key_is_renamed()
    {
        var sourceId = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        var (service, tags, _) = CreateService();
        var initial = Package(
            new TagEngineeringDto(
                tagId,
                "Pressure",
                "Area.Pressure",
                TagDataType.Double,
                Source: "plant.modbus",
                Address: "40001"),
            new DataSourceEngineeringDto(
                sourceId,
                "plant.modbus",
                "Plant Modbus",
                "modbus.tcp"));

        Assert.Empty(service.Apply(initial, ImportMode.CreateAndUpdate).Issues);

        var renamed = Package(
            new TagEngineeringDto(
                tagId,
                "Pressure",
                "Area.Pressure",
                TagDataType.Double,
                Source: "plant.modbus",
                Address: "40001",
                DataSourceId: sourceId),
            new DataSourceEngineeringDto(
                sourceId,
                "plant.modbus.primary",
                "Plant Modbus",
                "modbus.tcp"));

        var preview = service.Preview(renamed, ImportMode.CreateAndUpdate);
        Assert.True(preview.CanApply);
        Assert.Contains(
            preview.Items.SelectMany(x => x.Issues),
            issue => issue.Code == "TAG_DATASOURCE_KEY_STALE" && !issue.IsError);

        Assert.Empty(service.Apply(renamed, ImportMode.CreateAndUpdate).Issues);
        Assert.True(tags.TryGet(tagId, out var tag));
        Assert.NotNull(tag);
        Assert.Equal(sourceId, tag!.DataSourceId);
        Assert.Equal("plant.modbus.primary", tag.Source);
    }

    [Fact]
    public void Missing_stable_identity_is_not_silently_remapped_by_legacy_key()
    {
        var configuredId = Guid.NewGuid();
        var deletedId = Guid.NewGuid();
        var (service, _, dataSources) = CreateService();
        dataSources.Upsert(new DataSourceEngineeringDto(
            configuredId,
            "plant.modbus",
            "Plant Modbus",
            "modbus.tcp"));

        var package = Package(new TagEngineeringDto(
            Guid.NewGuid(),
            "Pressure",
            "Area.Pressure",
            TagDataType.Double,
            Source: "plant.modbus",
            Address: "40001",
            DataSourceId: deletedId));

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);

        Assert.False(preview.CanApply);
        var issue = Assert.Single(
            preview.Items.SelectMany(x => x.Issues),
            candidate => candidate.Code == "TAG_DATASOURCE_ID_NOT_FOUND");
        Assert.True(issue.IsError);
        Assert.Contains(deletedId.ToString(), issue.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static (EngineeringExchangeService Service, InMemoryTagRegistry Tags, InMemoryDataSourceEngineeringRegistry DataSources) CreateService()
    {
        var tags = new InMemoryTagRegistry();
        var dataSources = new InMemoryDataSourceEngineeringRegistry();
        var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        return (new EngineeringExchangeService(tags, alarms, dataSources), tags, dataSources);
    }

    private static EngineeringPackage Package(
        TagEngineeringDto tag,
        DataSourceEngineeringDto? dataSource = null) =>
        new(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            [tag],
            Array.Empty<AlarmEngineeringDto>(),
            dataSource is null ? Array.Empty<DataSourceEngineeringDto>() : [dataSource]);
}
