using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Commands;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.Gateways;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Security;
using Scada.Engineering.Views;

namespace Scada.Core.Tests;

public sealed class HomeCommonS1EngineeringTests
{
    [Fact]
    public void SchemaV22_RoundTripsLocationPhysicalBindingAndCapabilities()
    {
        var dataSourceId = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var equipmentId = Guid.NewGuid();

        var tags = new InMemoryTagRegistry();
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var dataSources = new InMemoryDataSourceEngineeringRegistry();
        var assets = new InMemoryEngineeringAssetRegistry();
        var commands = new InMemoryCommandEngineeringRegistry();
        var service = new EngineeringExchangeService(
            tags,
            alarms,
            dataSources,
            assets,
            new InMemoryEngineeringViewRegistry(),
            new InMemorySecurityPolicyEngineeringRegistry(),
            commands,
            new InMemoryGatewayEngineeringRegistry());

        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            [new TagEngineeringDto(tagId, "State", "Home.Relay.State", TagDataType.Boolean, DataSourceId: dataSourceId)],
            [],
            DataSources: [new DataSourceEngineeringDto(dataSourceId, "relay-1", "Relay 1", "test.driver")],
            Equipment:
            [
                new EquipmentEngineeringDto(
                    equipmentId,
                    "Home.Relay",
                    "Relay",
                    LocationId: locationId,
                    SourceBindings: [new EquipmentSourceBindingEngineeringDto(dataSourceId, "opaque-device-01", "primary")],
                    Capabilities:
                    [
                        new EquipmentCapabilityEngineeringDto(
                            "switch",
                            EquipmentCapabilityKinds.OnOff,
                            [
                                new CapabilityRoleBindingEngineeringDto("state", TagId: tagId),
                                new CapabilityRoleBindingEngineeringDto("set", CommandId: commandId)
                            ])
                    ])
            ],
            Commands: [new CommandEngineeringDto(commandId, "relay.set", "Set relay", Scada.Core.Commands.CommandKind.WriteTagValue, "true", TargetTagId: tagId)],
            Locations: [new LocationEngineeringDto(locationId, "Kitchen", Kind: "AreaRoom")]);

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);
        Assert.True(preview.CanApply);

        var result = service.Apply(package, ImportMode.CreateAndUpdate);
        Assert.DoesNotContain(result.Issues, issue => issue.IsError);

        var exported = service.ParseJson(service.ExportJson());
        Assert.Equal(22, exported.SchemaVersion);
        Assert.Equal(locationId, Assert.Single(exported.Locations!).Id);
        var equipment = Assert.Single(exported.Equipment!);
        Assert.Equal(locationId, equipment.LocationId);
        var source = Assert.Single(equipment.SourceBindings!);
        Assert.Equal(dataSourceId, source.DataSourceId);
        Assert.Equal("opaque-device-01", source.StableDeviceIdentity);
        var capability = Assert.Single(equipment.Capabilities!);
        Assert.Equal(EquipmentCapabilityKinds.OnOff, capability.Kind);
        Assert.Equal(2, capability.Bindings!.Count);
    }

    [Fact]
    public void Preview_RejectsInvalidLocationParentAndMissingCanonicalCapabilityTarget()
    {
        var locationId = Guid.NewGuid();
        var missingTagId = Guid.NewGuid();
        var equipmentId = Guid.NewGuid();

        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var service = new EngineeringExchangeService(
            new InMemoryTagRegistry(),
            alarms,
            new InMemoryDataSourceEngineeringRegistry(),
            new InMemoryEngineeringAssetRegistry());

        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            [],
            [],
            Equipment:
            [
                new EquipmentEngineeringDto(
                    equipmentId,
                    "Home.Sensor",
                    "Sensor",
                    LocationId: locationId,
                    Capabilities:
                    [
                        new EquipmentCapabilityEngineeringDto(
                            "temperature",
                            EquipmentCapabilityKinds.Temperature,
                            [new CapabilityRoleBindingEngineeringDto("value", TagId: missingTagId)])
                    ])
            ],
            Locations: [new LocationEngineeringDto(locationId, "Room", ParentLocationId: locationId)]);

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);
        var codes = preview.Items.SelectMany(x => x.Issues).Select(x => x.Code).ToHashSet();

        Assert.False(preview.CanApply);
        Assert.Contains("LOCATION_PARENT_SELF", codes);
        Assert.Contains("LOCATION_PARENT_CYCLE", codes);
        Assert.Contains("EQUIPMENT_CAPABILITY_TAG_NOT_FOUND", codes);
    }

    [Fact]
    public void LocationDelete_IsRejectedWhileReferencedOrParented()
    {
        var assets = new InMemoryEngineeringAssetRegistry();
        var siteId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        assets.UpsertLocation(new LocationEngineeringDto(siteId, "Site", Kind: "Site"));
        assets.UpsertLocation(new LocationEngineeringDto(roomId, "Room", siteId, "AreaRoom"));
        assets.UpsertEquipment(new EquipmentEngineeringDto(Guid.NewGuid(), "Home.Device", "Device", LocationId: roomId));

        Assert.False(assets.RemoveLocation(siteId));
        Assert.False(assets.RemoveLocation(roomId));
        Assert.NotNull(assets.FindLocation(siteId));
        Assert.NotNull(assets.FindLocation(roomId));
    }

    [Fact]
    public void RecreatedDataSourceWithSameFriendlyKey_DoesNotCaptureOldEquipmentBinding()
    {
        var oldId = Guid.NewGuid();
        var newId = Guid.NewGuid();
        var registry = new InMemoryDataSourceEngineeringRegistry();
        registry.Upsert(new DataSourceEngineeringDto(oldId, "device", "Device", "test.driver"));
        Assert.True(registry.Remove(oldId));
        registry.Upsert(new DataSourceEngineeringDto(newId, "device", "Device", "test.driver"));

        var equipment = new EquipmentEngineeringDto(
            Guid.NewGuid(),
            "Home.Device",
            "Device",
            SourceBindings: [new EquipmentSourceBindingEngineeringDto(oldId, "physical-identity")]);

        Assert.NotEqual(newId, Assert.Single(equipment.SourceBindings!).DataSourceId);
        Assert.Null(registry.Find(oldId));
        Assert.Equal(newId, registry.FindByKey("device")!.Id);
    }

    [Fact]
    public void LegacySchemaWithoutHomeFields_RemainsReadable()
    {
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var service = new EngineeringExchangeService(new InMemoryTagRegistry(), alarms);
        const string json = """
        {
          "schema": "scada.engineering",
          "schemaVersion": 21,
          "exportedAt": "2026-10-01T00:00:00Z",
          "tags": [],
          "alarms": [],
          "equipment": [
            { "id": "42000000-0000-0000-0000-000000000001", "path": "Plant.P01", "name": "Pump P01" }
          ]
        }
        """;

        var package = service.ParseJson(json);

        Assert.NotNull(package.Locations);
        Assert.Empty(package.Locations!);
        var equipment = Assert.Single(package.Equipment!);
        Assert.Null(equipment.LocationId);
        Assert.Null(equipment.SourceBindings);
        Assert.Null(equipment.Capabilities);
    }
}
