using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Views;

namespace Scada.Core.Tests;

public sealed class EngineeringSchemaCompatibilityTests
{
    [Theory]
    [InlineData(1, 2, 1, 1, 0, 0, 0, 0, 0, 0)]
    [InlineData(2, 2, 1, 0, 1, 0, 0, 0, 0, 0)]
    [InlineData(3, 4, 1, 0, 0, 1, 1, 1, 0, 0)]
    [InlineData(4, 6, 1, 0, 0, 1, 1, 1, 1, 1)]
    [InlineData(5, 1, 1, 0, 0, 0, 0, 0, 0, 0)]
    public void HistoricalSchema_CanBeAppliedAndReExportedAsCurrent(
        int sourceVersion,
        int expectedCreated,
        int expectedTags,
        int expectedAlarms,
        int expectedDataSources,
        int expectedTemplates,
        int expectedEquipment,
        int expectedDynamos,
        int expectedScreens,
        int expectedPopups)
    {
        var service = CreateService();
        var historical = service.ParseJson(Fixture(sourceVersion));

        Assert.Equal(sourceVersion, historical.SchemaVersion);
        Assert.NotNull(historical.DataSources);
        Assert.NotNull(historical.Templates);
        Assert.NotNull(historical.Equipment);
        Assert.NotNull(historical.Dynamos);
        Assert.NotNull(historical.Screens);
        Assert.NotNull(historical.Popups);

        var preview = service.Preview(historical, ImportMode.CreateAndUpdate);
        Assert.True(preview.CanApply);
        Assert.Equal(expectedCreated, preview.CreateCount);

        var result = service.Apply(historical, ImportMode.CreateAndUpdate);
        Assert.Empty(result.Issues);
        Assert.Equal(expectedCreated, result.Created);

        var migrated = service.ParseJson(service.ExportJson());
        Assert.Equal(EngineeringExchangeService.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.Equal(expectedTags, migrated.Tags.Count);
        Assert.Equal(expectedAlarms, migrated.Alarms.Count);
        Assert.Equal(expectedDataSources, migrated.DataSources!.Count);
        Assert.Equal(expectedTemplates, migrated.Templates!.Count);
        Assert.Equal(expectedEquipment, migrated.Equipment!.Count);
        Assert.Equal(expectedDynamos, migrated.Dynamos!.Count);
        Assert.Equal(expectedScreens, migrated.Screens!.Count);
        Assert.Equal(expectedPopups, migrated.Popups!.Count);

        if (sourceVersion == 5)
        {
            var tag = Assert.Single(migrated.Tags);
            Assert.Equal(new[] { "Operator" }, tag.AccessPolicy!.ReadRoles);
            Assert.Empty(tag.AccessPolicy.WriteRoles!);
            Assert.Equal(new[] { "Engineering" }, tag.AccessPolicy.ConfigureRoles);
        }
    }

    [Fact]
    public void Parser_RejectsEngineeringSchemaNewerThanRuntime()
    {
        var service = CreateService();
        var json = $$"""
        {
          "schema": "scada.engineering",
          "schemaVersion": {{EngineeringExchangeService.CurrentSchemaVersion + 1}},
          "exportedAt": "2026-08-26T00:00:00Z",
          "tags": [],
          "alarms": []
        }
        """;

        var exception = Assert.Throws<InvalidDataException>(() => service.ParseJson(json));

        Assert.Contains("newer than supported", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static EngineeringExchangeService CreateService()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        var alarms = new InMemoryAlarmEngine(bus);
        return new EngineeringExchangeService(
            tags,
            alarms,
            new InMemoryDataSourceEngineeringRegistry(),
            new InMemoryEngineeringAssetRegistry(),
            new InMemoryEngineeringViewRegistry());
    }

    private static string Fixture(int version) => version switch
    {
        1 => """
        {
          "schema": "scada.engineering",
          "schemaVersion": 1,
          "exportedAt": "2026-01-01T00:00:00Z",
          "tags": [
            {
              "name": "Pressure",
              "path": "Plant.P01.Pressure",
              "dataType": "double",
              "engineeringUnit": "bar",
              "readOnly": true
            }
          ],
          "alarms": [
            {
              "name": "High pressure",
              "tagPath": "Plant.P01.Pressure",
              "type": "high",
              "priority": "high",
              "setpoint": 9.5,
              "requiresAcknowledgement": true,
              "shelvingAllowed": true,
              "enabled": true
            }
          ]
        }
        """,
        2 => """
        {
          "schema": "scada.engineering",
          "schemaVersion": 2,
          "exportedAt": "2026-02-01T00:00:00Z",
          "tags": [
            {
              "name": "Pressure",
              "path": "Plant.P01.Pressure",
              "dataType": "double",
              "source": "plant.modbus01",
              "address": "40001",
              "engineeringUnit": "bar",
              "readOnly": true
            }
          ],
          "alarms": [],
          "dataSources": [
            {
              "key": "plant.modbus01",
              "name": "PLC principal",
              "driver": "modbus.tcp",
              "enabled": true,
              "settings": {
                "host": "10.0.0.10",
                "port": "502"
              },
              "secretReferences": {
                "credential": "secret://plant/modbus01"
              }
            }
          ]
        }
        """,
        3 => """
        {
          "schema": "scada.engineering",
          "schemaVersion": 3,
          "exportedAt": "2026-03-01T00:00:00Z",
          "tags": [
            {
              "name": "Running",
              "path": "Plant.P01.Running",
              "dataType": "boolean",
              "readOnly": true
            }
          ],
          "alarms": [],
          "dataSources": [],
          "templates": [
            {
              "key": "pump.standard",
              "name": "Standard Pump",
              "bindings": [
                {
                  "key": "running",
                  "kind": "tag",
                  "target": "{equipmentPath}.Running",
                  "direction": "read"
                }
              ]
            }
          ],
          "equipment": [
            {
              "path": "Plant.P01",
              "name": "Pump P01",
              "templateKey": "pump.standard",
              "bindings": [
                {
                  "key": "running",
                  "kind": "tag",
                  "target": "Plant.P01.Running",
                  "direction": "read"
                }
              ]
            }
          ],
          "dynamos": [
            {
              "key": "dynamo.pump.standard",
              "name": "Pump Dynamo",
              "templateKey": "pump.standard",
              "bindings": [
                {
                  "key": "running",
                  "kind": "tag",
                  "target": "{equipmentPath}.Running",
                  "direction": "read"
                }
              ]
            }
          ]
        }
        """,
        4 => """
        {
          "schema": "scada.engineering",
          "schemaVersion": 4,
          "exportedAt": "2026-04-01T00:00:00Z",
          "tags": [
            {
              "name": "Running",
              "path": "Plant.P01.Running",
              "dataType": "boolean",
              "readOnly": true
            }
          ],
          "alarms": [],
          "dataSources": [],
          "templates": [
            {
              "key": "pump.standard",
              "name": "Standard Pump"
            }
          ],
          "equipment": [
            {
              "path": "Plant.P01",
              "name": "Pump P01",
              "templateKey": "pump.standard"
            }
          ],
          "dynamos": [
            {
              "key": "dynamo.pump.standard",
              "name": "Pump Dynamo",
              "templateKey": "pump.standard"
            }
          ],
          "screens": [
            {
              "key": "plant.overview",
              "name": "Plant Overview",
              "route": "/plant",
              "elements": [
                {
                  "key": "pump01",
                  "type": "dynamo",
                  "dynamoKey": "dynamo.pump.standard",
                  "equipmentPath": "Plant.P01"
                }
              ]
            }
          ],
          "popups": [
            {
              "key": "popup.pump.standard",
              "name": "Pump Popup",
              "templateKey": "pump.standard",
              "elements": [
                {
                  "key": "running",
                  "type": "status",
                  "bindings": [
                    {
                      "key": "active",
                      "kind": "tag",
                      "target": "{equipmentPath}.Running",
                      "direction": "read"
                    }
                  ]
                }
              ]
            }
          ]
        }
        """,
        5 => """
        {
          "schema": "scada.engineering",
          "schemaVersion": 5,
          "exportedAt": "2026-05-01T00:00:00Z",
          "tags": [
            {
              "name": "Setpoint",
              "path": "Plant.P01.Setpoint",
              "dataType": "double",
              "engineeringUnit": "bar",
              "readOnly": false,
              "accessPolicy": {
                "readRoles": ["Operator"],
                "writeRoles": [],
                "configureRoles": ["Engineering"]
              }
            }
          ],
          "alarms": [],
          "dataSources": [],
          "templates": [],
          "equipment": [],
          "dynamos": [],
          "screens": [],
          "popups": []
        }
        """,
        _ => throw new ArgumentOutOfRangeException(nameof(version))
    };
}
