using Scada.Core.Abstractions;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.ImportExport;

namespace Scada.Core.Tests;

public sealed class EngineeringExchangeTests
{
    [Fact]
    public void ExportAndParseJson_RoundTripsTagAndAlarm()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var tag = TagDefinition.Create("Pressure", "Plant.P01.Pressure", TagDataType.Double, "modbus.tcp", "bar");
        tags.Register(tag);
        alarms.Register(AlarmDefinition.Create("High pressure", tag.Id, AlarmType.High, AlarmPriority.High, setpoint: 9.5, area: "Plant"));
        var service = new EngineeringExchangeService(tags, alarms);

        var json = service.ExportJson();
        var package = service.ParseJson(json);

        Assert.Single(package.Tags);
        Assert.Single(package.Alarms);
        Assert.Equal(EngineeringExchangeService.CurrentSchemaVersion, package.SchemaVersion);
        Assert.Equal("Plant.P01.Pressure", package.Tags.Single().Path);
        Assert.Equal("High pressure", package.Alarms.Single().Name);
    }

    [Fact]
    public void Preview_DetectsDuplicateTagPaths()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var service = new EngineeringExchangeService(tags, alarms);
        var package = new EngineeringPackage(EngineeringExchangeService.CurrentSchema, 1, DateTimeOffset.UtcNow,
            new[]
            {
                new TagEngineeringDto(null, "A", "Plant.Duplicate", TagDataType.Double),
                new TagEngineeringDto(null, "B", "Plant.Duplicate", TagDataType.Double)
            }, Array.Empty<AlarmEngineeringDto>());

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);

        Assert.False(preview.CanApply);
        Assert.Equal(2, preview.ErrorCount);
    }

    [Fact]
    public void Apply_CreateAndUpdate_PreservesStableIdWhenPathMatches()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var existing = TagDefinition.Create("Old", "Plant.P01.Current", TagDataType.Double);
        tags.Register(existing);
        var service = new EngineeringExchangeService(tags, alarms);
        var package = new EngineeringPackage(EngineeringExchangeService.CurrentSchema, 1, DateTimeOffset.UtcNow,
            new[] { new TagEngineeringDto(null, "Current", "Plant.P01.Current", TagDataType.Double, EngineeringUnit: "A") },
            Array.Empty<AlarmEngineeringDto>());

        var result = service.Apply(package, ImportMode.CreateAndUpdate);

        Assert.Equal(1, result.Updated);
        Assert.True(tags.TryGetByPath("Plant.P01.Current", out var updated));
        Assert.Equal(existing.Id, updated!.Id);
        Assert.Equal("Current", updated.Name);
    }

    [Fact]
    public void CurrentSchema_RoundTripsDataSourceAndSecretReference()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var dataSources = new InMemoryDataSourceEngineeringRegistry();
        dataSources.Upsert(new DataSourceEngineeringDto(
            null,
            "plant.modbus01",
            "PLC principal",
            "modbus.tcp",
            Settings: new() { ["host"] = "10.10.0.10", ["port"] = "502" },
            SecretReferences: new() { ["credential"] = "secret://plant/modbus01" }));
        var service = new EngineeringExchangeService(tags, alarms, dataSources);

        var package = service.ParseJson(service.ExportJson());

        var dataSource = Assert.Single(package.DataSources!);
        Assert.Equal("plant.modbus01", dataSource.Key);
        Assert.Equal("10.10.0.10", dataSource.Settings!["host"]);
        Assert.Equal("secret://plant/modbus01", dataSource.SecretReferences!["credential"]);
    }

    [Fact]
    public void DataSourceCsv_RoundTripsFlexibleSettingsAndSecretReferences()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var dataSources = new InMemoryDataSourceEngineeringRegistry();
        dataSources.Upsert(new DataSourceEngineeringDto(
            null,
            "plant.modbus01",
            "PLC principal",
            "modbus.tcp",
            Settings: new() { ["host"] = "10.10.0.10", ["port"] = "502", ["unitId"] = "1" },
            SecretReferences: new() { ["credential"] = "vault://plant/modbus01" },
            Metadata: new() { ["area"] = "EAB5" }));
        var service = new EngineeringExchangeService(tags, alarms, dataSources);

        var parsed = service.ParseDataSourcesCsv(service.ExportDataSourcesCsv());

        var dataSource = Assert.Single(parsed.DataSources!);
        Assert.Equal("plant.modbus01", dataSource.Key);
        Assert.Equal("502", dataSource.Settings!["port"]);
        Assert.Equal("vault://plant/modbus01", dataSource.SecretReferences!["credential"]);
        Assert.Equal("EAB5", dataSource.Metadata!["area"]);
    }

    [Fact]
    public void Preview_RejectsPlaintextSecretInDataSourceSettings()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var dataSources = new InMemoryDataSourceEngineeringRegistry();
        var service = new EngineeringExchangeService(tags, alarms, dataSources);
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            new[]
            {
                new DataSourceEngineeringDto(null, "plant.bad", "Bad source", "modbus.tcp", Settings: new() { ["password"] = "123456" })
            });

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);

        Assert.False(preview.CanApply);
        Assert.Contains(preview.Items.SelectMany(x => x.Issues), x => x.Code == "DATASOURCE_PLAINTEXT_SECRET");
    }

    [Fact]
    public void Preview_RejectsTagReferencingUnknownDataSource()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var service = new EngineeringExchangeService(tags, alarms, new InMemoryDataSourceEngineeringRegistry());
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            new[] { new TagEngineeringDto(null, "Pressure", "Plant.P01.Pressure", TagDataType.Double, Source: "plant.missing") },
            Array.Empty<AlarmEngineeringDto>(),
            Array.Empty<DataSourceEngineeringDto>());

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);

        Assert.False(preview.CanApply);
        Assert.Contains(preview.Items.SelectMany(x => x.Issues), x => x.Code == "TAG_DATASOURCE_NOT_FOUND");
    }

    [Fact]
    public void Preview_AcceptsTagWhenDataSourceIsInSamePackage()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var service = new EngineeringExchangeService(tags, alarms, new InMemoryDataSourceEngineeringRegistry());
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            new[] { new TagEngineeringDto(null, "Pressure", "Plant.P01.Pressure", TagDataType.Double, Source: "plant.modbus01") },
            Array.Empty<AlarmEngineeringDto>(),
            new[] { new DataSourceEngineeringDto(null, "plant.modbus01", "PLC principal", "modbus.tcp", Settings: new() { ["host"] = "10.10.0.10" }) });

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);

        Assert.True(preview.CanApply);
        Assert.Equal(2, preview.CreateCount);
    }

    [Fact]
    public void SchemaV3_RoundTripsTemplateEquipmentAndDynamo()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var dataSources = new InMemoryDataSourceEngineeringRegistry();
        var assets = new InMemoryEngineeringAssetRegistry();

        assets.UpsertTemplate(new EquipmentTemplateEngineeringDto(
            null,
            "pump.standard",
            "Standard Pump",
            Bindings: new[] { new EngineeringBindingDto("running", EngineeringBindingKind.Tag, "{equipmentPath}.Running", "read") },
            Properties: new() { ["category"] = "pump" },
            Context: new() { ["domain"] = "pumping" }));
        assets.UpsertEquipment(new EquipmentEngineeringDto(
            null,
            "Plant.P01",
            "Pump P01",
            "pump.standard",
            Bindings: new[] { new EngineeringBindingDto("running", EngineeringBindingKind.Tag, "Plant.P01.Running", "read") },
            Properties: new() { ["displayLabel"] = "P01" },
            Context: new() { ["area"] = "Plant" }));
        assets.UpsertDynamo(new DynamoEngineeringDto(
            null,
            "dynamo.pump.standard",
            "Standard Pump Dynamo",
            "pump.standard",
            Bindings: new[] { new EngineeringBindingDto("running", EngineeringBindingKind.Tag, "{equipmentPath}.Running", "read") },
            Properties: new() { ["symbol"] = "pump" }));

        var service = new EngineeringExchangeService(tags, alarms, dataSources, assets);
        var package = service.ParseJson(service.ExportJson());

        var template = Assert.Single(package.Templates!);
        var equipment = Assert.Single(package.Equipment!);
        var dynamo = Assert.Single(package.Dynamos!);
        Assert.Equal("pump.standard", template.Key);
        Assert.Equal("P01", equipment.Properties!["displayLabel"]);
        Assert.Equal("Plant", equipment.Context!["area"]);
        Assert.Equal("dynamo.pump.standard", dynamo.Key);
        Assert.Equal("{equipmentPath}.Running", dynamo.Bindings!.Single().Target);
    }

    [Fact]
    public void Preview_RejectsEquipmentWhenTemplateDoesNotExist()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var service = new EngineeringExchangeService(tags, alarms, new InMemoryDataSourceEngineeringRegistry(), new InMemoryEngineeringAssetRegistry());
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            Equipment: new[] { new EquipmentEngineeringDto(null, "Plant.P01", "Pump P01", "pump.missing") });

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);

        Assert.False(preview.CanApply);
        Assert.Contains(preview.Items.SelectMany(x => x.Issues), x => x.Code == "EQUIPMENT_TEMPLATE_NOT_FOUND");
    }

    [Fact]
    public void Preview_RejectsEquipmentBindingWhenTagDoesNotExist()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var service = new EngineeringExchangeService(tags, alarms, new InMemoryDataSourceEngineeringRegistry(), new InMemoryEngineeringAssetRegistry());
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            Templates: new[] { new EquipmentTemplateEngineeringDto(null, "pump.standard", "Standard Pump") },
            Equipment: new[]
            {
                new EquipmentEngineeringDto(
                    null,
                    "Plant.P01",
                    "Pump P01",
                    "pump.standard",
                    Bindings: new[] { new EngineeringBindingDto("running", EngineeringBindingKind.Tag, "Plant.P01.Running", "read") })
            });

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);

        Assert.False(preview.CanApply);
        Assert.Contains(preview.Items.SelectMany(x => x.Issues), x => x.Code == "BINDING_TAG_NOT_FOUND");
    }

    [Fact]
    public void Preview_AcceptsEquipmentWhenTemplateAndTagAreInSamePackage()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var service = new EngineeringExchangeService(tags, alarms, new InMemoryDataSourceEngineeringRegistry(), new InMemoryEngineeringAssetRegistry());
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            new[] { new TagEngineeringDto(null, "Running", "Plant.P01.Running", TagDataType.Boolean) },
            Array.Empty<AlarmEngineeringDto>(),
            Templates: new[] { new EquipmentTemplateEngineeringDto(null, "pump.standard", "Standard Pump") },
            Equipment: new[]
            {
                new EquipmentEngineeringDto(
                    null,
                    "Plant.P01",
                    "Pump P01",
                    "pump.standard",
                    Bindings: new[] { new EngineeringBindingDto("running", EngineeringBindingKind.Tag, "Plant.P01.Running", "read") })
            });

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);

        Assert.True(preview.CanApply);
        Assert.Equal(3, preview.CreateCount);
    }

    [Fact]
    public void ParseJson_AcceptsSchemaV1WithoutDataSourcesOrAssets()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var service = new EngineeringExchangeService(tags, alarms);
        const string json = """
        {
          "schema": "scada.engineering",
          "schemaVersion": 1,
          "exportedAt": "2026-08-25T00:00:00Z",
          "tags": [],
          "alarms": []
        }
        """;

        var package = service.ParseJson(json);

        Assert.NotNull(package.DataSources);
        Assert.Empty(package.DataSources!);
        Assert.Empty(package.Templates!);
        Assert.Empty(package.Equipment!);
        Assert.Empty(package.Dynamos!);
    }
}
