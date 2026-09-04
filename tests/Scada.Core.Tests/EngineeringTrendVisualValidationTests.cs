using System.Text.Json;
using Scada.Engineering.Contracts;
using Scada.Engineering.VisualScripting;

namespace Scada.Core.Tests;

public sealed class EngineeringTrendVisualValidationTests
{
    [Fact]
    public void BuiltinSchema_RegistersTrendAndItsCanonicalScalarContract()
    {
        var schema = BuiltinVisualObjectSchemas.GetRequired(BuiltinVisualObjectSchemas.TrendType);

        Assert.Equal("core.trend", schema.ObjectTypeKey);
        Assert.True(schema.Declares("trendMode"));
        Assert.True(schema.Declares("trendWindowSeconds"));
        Assert.True(schema.Declares("trendRefreshSeconds"));
        Assert.True(schema.Declares("trendLegendVisible"));
        Assert.True(schema.Declares("trendGridVisible"));
        Assert.True(schema.Declares("trendAxesVisible"));
        Assert.True(schema.Declares("trendQualityVisible"));
        Assert.False(schema.Declares(BuiltinVisualObjectSchemas.TrendPensProperty));
    }

    [Fact]
    public void Validation_AcceptsNativeTrendPensAndRejectsInvalidScalarValues()
    {
        var valid = new VisualElementEngineeringDto(
            "trend",
            BuiltinVisualObjectSchemas.TrendType,
            Properties: new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["x"] = Json("10"),
                ["y"] = Json("20"),
                ["width"] = Json("420"),
                ["height"] = Json("180"),
                ["trendMode"] = Json("\"history\""),
                ["trendWindowSeconds"] = Json("3600"),
                ["trendRefreshSeconds"] = Json("5"),
                ["trendLegendVisible"] = Json("true"),
                ["trendGridVisible"] = Json("true"),
                ["trendAxesVisible"] = Json("true"),
                ["trendQualityVisible"] = Json("true"),
                [BuiltinVisualObjectSchemas.TrendPensProperty] = Json("""
                [
                  {
                    "id": "pressure",
                    "tagId": "00000000-0000-0000-0000-00000000c150",
                    "tagPath": "Demo.Discharge.Pressure",
                    "label": "Pressure",
                    "visible": true,
                    "unit": "bar",
                    "color": "#38BDF8",
                    "lineWidth": 2,
                    "lineStyle": "solid",
                    "axis": "left",
                    "scale": { "mode": "auto" }
                  }
                ]
                """)
            });

        var validIssues = BuiltinVisualEngineeringValidation.Validate(
            valid,
            ImportEntityKind.Screen,
            "demo.overview",
            schemaVersion: int.MaxValue);

        Assert.DoesNotContain(validIssues, issue => issue.IsError);

        var invalidProperties = new Dictionary<string, JsonElement>(valid.Properties!, StringComparer.Ordinal)
        {
            ["trendWindowSeconds"] = Json("59")
        };
        var invalid = valid with { Properties = invalidProperties };

        var invalidIssues = BuiltinVisualEngineeringValidation.Validate(
            invalid,
            ImportEntityKind.Screen,
            "demo.overview",
            schemaVersion: int.MaxValue);

        Assert.Contains(invalidIssues, issue => issue.Code == "VISUAL_PROPERTY_INVALID" && issue.IsError);
    }

    [Fact]
    public void Validation_RemainsFailClosedForUnknownCoreTypes()
    {
        var unknown = new VisualElementEngineeringDto(
            "future",
            "core.futureTrend",
            Properties: new Dictionary<string, JsonElement>());

        var issues = BuiltinVisualEngineeringValidation.Validate(
            unknown,
            ImportEntityKind.Screen,
            "demo.overview",
            schemaVersion: int.MaxValue);

        Assert.Contains(issues, issue => issue.Code == "VISUAL_BUILTIN_TYPE_UNKNOWN" && issue.IsError);
    }

    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
