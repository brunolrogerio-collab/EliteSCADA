using System.Text.Json;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Views;
using Scada.Engineering.VisualScripting;

namespace Scada.Core.Tests;

public sealed class VisualEngineeringSchemaV12TypedPropertiesTests
{
    [Fact]
    public void SchemaV11StringProperties_MigrateToTypedCurrentSchemaOnApplyAndExport()
    {
        var views = new InMemoryEngineeringViewRegistry();
        using var harness = new Harness(views);
        const string legacyJson = """
        {
          "schema": "scada.engineering",
          "schemaVersion": 11,
          "exportedAt": "2026-08-28T00:00:00Z",
          "tags": [],
          "alarms": [],
          "screens": [
            {
              "key": "plant.overview",
              "name": "Plant Overview",
              "elements": [
                {
                  "id": "95000000-0000-0000-0000-000000000001",
                  "key": "plantImage",
                  "type": "core.image",
                  "properties": {
                    "x": "12.5",
                    "visible": "false",
                    "zIndex": "2",
                    "assetRef": "asset:plant-logo",
                    "imageFit": "contain"
                  }
                }
              ]
            }
          ]
        }
        """;

        var package = harness.Exchange.ParseJson(legacyJson);
        var preview = harness.Exchange.Preview(package, ImportMode.CreateAndUpdate);
        Assert.True(preview.CanApply);

        var result = harness.Exchange.Apply(package, ImportMode.CreateAndUpdate);
        Assert.Empty(result.Issues);

        var stored = Assert.Single(Assert.Single(views.SnapshotScreens()).Elements!);
        Assert.Equal(JsonValueKind.Number, stored.Properties!["x"].ValueKind);
        Assert.Equal(12.5, stored.Properties["x"].GetDouble());
        Assert.Equal(JsonValueKind.False, stored.Properties["visible"].ValueKind);
        Assert.Equal(JsonValueKind.Number, stored.Properties["zIndex"].ValueKind);
        Assert.Equal(2, stored.Properties["zIndex"].GetInt32());
        Assert.Equal(JsonValueKind.Object, stored.Properties["assetRef"].ValueKind);
        Assert.Equal("asset:plant-logo", stored.Properties["assetRef"].GetProperty("assetId").GetString());
        Assert.Equal("contain", stored.Properties["imageFit"].GetString());

        var exported = harness.Exchange.ExportPackage();
        Assert.Equal(EngineeringExchangeService.CurrentSchemaVersion, exported.SchemaVersion);
        Assert.True(exported.SchemaVersion >= VisualEngineeringPropertyCodec.TypedSchemaVersion);
        Assert.Equal(12, VisualEngineeringPropertyCodec.TypedSchemaVersion);
    }

    [Fact]
    public void SchemaV12_RejectsLegacyStringEncodingForTypedBuiltinProperty()
    {
        var views = new InMemoryEngineeringViewRegistry();
        using var harness = new Harness(views);
        const string currentJson = """
        {
          "schema": "scada.engineering",
          "schemaVersion": 12,
          "exportedAt": "2026-08-28T00:00:00Z",
          "tags": [],
          "alarms": [],
          "screens": [
            {
              "key": "plant.overview",
              "name": "Plant Overview",
              "elements": [
                {
                  "id": "95000000-0000-0000-0000-000000000002",
                  "key": "rectangle",
                  "type": "core.rectangle",
                  "properties": { "x": "12.5" }
                }
              ]
            }
          ]
        }
        """;

        var package = harness.Exchange.ParseJson(currentJson);
        var preview = harness.Exchange.Preview(package, ImportMode.CreateAndUpdate);
        var issues = preview.Items.SelectMany(item => item.Issues).ToArray();

        Assert.False(preview.CanApply);
        Assert.Contains(issues, issue => issue.Code == "VISUAL_PROPERTY_INVALID" && issue.IsError);
    }

    [Fact]
    public void SchemaV12TypedEncoding_RemainsNativeWhenExportedAsCurrentSchema()
    {
        var views = new InMemoryEngineeringViewRegistry();
        using var harness = new Harness(views);
        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            [VisualPropertyKeys.X] = JsonSerializer.SerializeToElement(42.25),
            [VisualPropertyKeys.Visible] = JsonSerializer.SerializeToElement(true),
            [VisualPropertyKeys.AssetRef] = JsonSerializer.SerializeToElement(new { assetId = "asset:overview" }),
            [VisualPropertyKeys.ImageFit] = JsonSerializer.SerializeToElement("cover")
        };

        views.UpsertScreen(new ScreenEngineeringDto(
            null,
            "plant.overview",
            "Plant Overview",
            Elements:
            [
                new VisualElementEngineeringDto(
                    "image",
                    BuiltinVisualObjectSchemas.ImageType,
                    Properties: properties,
                    Id: Guid.Parse("95000000-0000-0000-0000-000000000003"))
            ]));

        using var document = JsonDocument.Parse(harness.Exchange.ExportJson(indented: false));
        var root = document.RootElement;
        var propertyBag = root.GetProperty("screens")[0]
            .GetProperty("elements")[0]
            .GetProperty("properties");

        Assert.Equal(EngineeringExchangeService.CurrentSchemaVersion, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(JsonValueKind.Number, propertyBag.GetProperty("x").ValueKind);
        Assert.Equal(JsonValueKind.True, propertyBag.GetProperty("visible").ValueKind);
        Assert.Equal(JsonValueKind.Object, propertyBag.GetProperty("assetRef").ValueKind);
        Assert.Equal("asset:overview", propertyBag.GetProperty("assetRef").GetProperty("assetId").GetString());
    }

    private sealed class Harness : IDisposable
    {
        private readonly InMemoryScadaEventBus _eventBus = new();

        public Harness(InMemoryEngineeringViewRegistry views)
        {
            var tags = new InMemoryTagRegistry();
            Alarms = new InMemoryAlarmEngine(_eventBus);
            Exchange = new EngineeringExchangeService(
                tags,
                Alarms,
                new InMemoryDataSourceEngineeringRegistry(),
                new InMemoryEngineeringAssetRegistry(),
                views);
        }

        public InMemoryAlarmEngine Alarms { get; }
        public EngineeringExchangeService Exchange { get; }

        public void Dispose() => Alarms.Dispose();
    }
}
