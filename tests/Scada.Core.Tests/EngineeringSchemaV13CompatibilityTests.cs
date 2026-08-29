using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.ImportExport;

namespace Scada.Core.Tests;

public sealed class EngineeringSchemaV13CompatibilityTests
{
    [Fact]
    public void SchemaV12WithoutVisualAssets_ParsesAsEmptyAssetCollectionAndReExportsAsV13()
    {
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var exchange = new EngineeringExchangeService(new InMemoryTagRegistry(), alarms);
        const string schemaV12 = """
        {
          "schema": "scada.engineering",
          "schemaVersion": 12,
          "exportedAt": "2026-08-28T00:00:00Z",
          "tags": [],
          "alarms": [],
          "screens": []
        }
        """;

        var parsed = exchange.ParseJson(schemaV12);

        Assert.Equal(12, parsed.SchemaVersion);
        Assert.NotNull(parsed.VisualAssets);
        Assert.Empty(parsed.VisualAssets!);

        var exported = exchange.ExportPackage();
        Assert.Equal(13, EngineeringExchangeService.CurrentSchemaVersion);
        Assert.Equal(EngineeringExchangeService.CurrentSchemaVersion, exported.SchemaVersion);
        Assert.NotNull(exported.VisualAssets);
        Assert.Empty(exported.VisualAssets!);
    }
}
