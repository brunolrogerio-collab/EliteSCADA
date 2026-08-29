using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.DataSources;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Views;
using Scada.Engineering.VisualScripting;

namespace Scada.Core.Tests;

public sealed class EngineeringVisualSchemaV11CompatibilityTests
{
    [Fact]
    public void SchemaV10VisualElementsWithoutIds_ParseAndMaterializeStableIds()
    {
        var views = new InMemoryEngineeringViewRegistry();
        var service = CreateService(views);
        const string legacyJson = """
        {
          "schema": "scada.engineering",
          "schemaVersion": 10,
          "exportedAt": "2026-08-28T00:00:00Z",
          "tags": [],
          "alarms": [],
          "screens": [
            {
              "key": "plant.overview",
              "name": "Plant Overview",
              "route": "/plant",
              "elements": [
                {
                  "key": "pump01",
                  "type": "group",
                  "children": [
                    { "key": "label", "type": "text" }
                  ]
                }
              ]
            }
          ]
        }
        """;

        var legacy = service.ParseJson(legacyJson);
        var parsedRoot = Assert.Single(Assert.Single(legacy.Screens!).Elements!);
        Assert.Null(parsedRoot.Id);

        var result = service.Apply(legacy, Scada.Engineering.Contracts.ImportMode.CreateAndUpdate);
        Assert.Empty(result.Issues);

        var storedScreen = Assert.Single(views.SnapshotScreens());
        var storedRoot = Assert.Single(storedScreen.Elements!);
        var storedChild = Assert.Single(storedRoot.Children!);
        Assert.NotEqual(Guid.Empty, storedRoot.Id);
        Assert.NotEqual(Guid.Empty, storedChild.Id);

        var exported = service.ExportPackage();
        Assert.Equal(EngineeringExchangeService.CurrentSchemaVersion, exported.SchemaVersion);
        Assert.True(exported.SchemaVersion >= VisualEngineeringPropertyCodec.TypedSchemaVersion);
        Assert.Equal(storedRoot.Id, Assert.Single(Assert.Single(exported.Screens!).Elements!).Id);
    }

    [Fact]
    public void SchemaV11IdentityRoundTrip_ReExportsAsCurrentSchemaAndPreservesExplicitIds()
    {
        var views = new InMemoryEngineeringViewRegistry();
        var service = CreateService(views);
        var objectId = Guid.NewGuid();

        views.UpsertScreen(new Scada.Engineering.Contracts.ScreenEngineeringDto(
            null,
            "plant.overview",
            "Plant Overview",
            Elements:
            [
                new Scada.Engineering.Contracts.VisualElementEngineeringDto(
                    "pump01",
                    "rectangle",
                    Id: objectId)
            ]));

        var roundTrip = service.ParseJson(service.ExportJson());
        var element = Assert.Single(Assert.Single(roundTrip.Screens!).Elements!);

        Assert.Equal(EngineeringExchangeService.CurrentSchemaVersion, roundTrip.SchemaVersion);
        Assert.True(roundTrip.SchemaVersion >= VisualEngineeringPropertyCodec.TypedSchemaVersion);
        Assert.Equal(objectId, element.Id);
    }

    private static EngineeringExchangeService CreateService(InMemoryEngineeringViewRegistry views)
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        var alarms = new InMemoryAlarmEngine(bus);
        return new EngineeringExchangeService(
            tags,
            alarms,
            new InMemoryDataSourceEngineeringRegistry(),
            new InMemoryEngineeringAssetRegistry(),
            views);
    }
}
