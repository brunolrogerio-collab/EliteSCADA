using System.Text.Json;
using Scada.Engineering.Contracts;
using Scada.Engineering.Validation;
using Scada.Engineering.VisualScripting;

namespace Scada.Core.Tests;

public sealed class VisualPolygonEngineeringTests
{
    [Fact]
    public void BuiltinRegistry_DeclaresPolygonWithoutTreatingPointsAsScalarProperty()
    {
        var schema = BuiltinVisualObjectSchemas.GetRequired(BuiltinVisualObjectSchemas.PolygonType);

        Assert.Equal("core.polygon", schema.ObjectTypeKey);
        Assert.True(schema.Declares(VisualPropertyKeys.FillColor));
        Assert.True(schema.Declares(VisualPropertyKeys.StrokeColor));
        Assert.False(schema.Declares("points"));

        var element = Polygon("[[0,0],[1,0],[0,1]]");
        var issues = BuiltinVisualEngineeringValidation.Validate(
            element,
            ImportEntityKind.Screen,
            "screen",
            schemaVersion: 13);

        Assert.DoesNotContain(issues, issue => issue.IsError);
    }

    [Fact]
    public void ScreenValidator_AcceptsValidClosedPolygonGeometryAndRejectsDegenerateGeometry()
    {
        var valid = new ScreenEngineeringDto(
            Guid.NewGuid(),
            "screen",
            "Screen",
            "/screen",
            [Polygon("[[0,0],[100,0],[100,80],[20,90]]")]);

        var validIssues = EngineeringValidator.ValidateScreen(valid);
        Assert.DoesNotContain(validIssues, issue => issue.Code.StartsWith("VISUAL_POLYGON_", StringComparison.Ordinal));

        var degenerate = valid with
        {
            Elements = [Polygon("[[0,0],[10,10],[20,20]]")]
        };
        var invalidIssues = EngineeringValidator.ValidateScreen(degenerate);
        Assert.Contains(invalidIssues, issue => issue.Code == "VISUAL_POLYGON_AREA_INVALID" && issue.IsError);
    }

    [Fact]
    public void ScreenValidator_RejectsPolygonPointsOnOtherBuiltinsAndPolygonWithTooFewVertices()
    {
        var nonPolygon = new VisualElementEngineeringDto(
            "rectangle",
            "core.rectangle",
            Properties: new Dictionary<string, JsonElement>
            {
                ["points"] = Points("[[0,0],[1,0],[0,1]]")
            },
            Id: Guid.NewGuid());

        var tooSmall = Polygon("[[0,0],[1,0]]");
        var screen = new ScreenEngineeringDto(Guid.NewGuid(), "screen", "Screen", "/screen", [nonPolygon, tooSmall]);
        var issues = EngineeringValidator.ValidateScreen(screen);

        Assert.Contains(issues, issue => issue.Code == "VISUAL_POLYGON_POINTS_UNEXPECTED" && issue.IsError);
        Assert.Contains(issues, issue => issue.Code == "VISUAL_POLYGON_POINTS_MINIMUM" && issue.IsError);
    }

    private static VisualElementEngineeringDto Polygon(string compactPairs)
    {
        using var source = JsonDocument.Parse(compactPairs);
        var points = source.RootElement.EnumerateArray()
            .Select(pair => new
            {
                x = pair[0].GetDouble(),
                y = pair[1].GetDouble()
            })
            .ToArray();
        var element = JsonSerializer.SerializeToElement(points);
        return new VisualElementEngineeringDto(
            "polygon",
            "core.polygon",
            Properties: new Dictionary<string, JsonElement>
            {
                ["x"] = JsonSerializer.SerializeToElement(0d),
                ["y"] = JsonSerializer.SerializeToElement(0d),
                ["width"] = JsonSerializer.SerializeToElement(100d),
                ["height"] = JsonSerializer.SerializeToElement(100d),
                ["fillColor"] = JsonSerializer.SerializeToElement("#336699"),
                ["points"] = element
            },
            Id: Guid.NewGuid());
    }

    private static JsonElement Points(string compactPairs)
    {
        using var source = JsonDocument.Parse(compactPairs);
        var points = source.RootElement.EnumerateArray()
            .Select(pair => new { x = pair[0].GetDouble(), y = pair[1].GetDouble() })
            .ToArray();
        return JsonSerializer.SerializeToElement(points);
    }
}
