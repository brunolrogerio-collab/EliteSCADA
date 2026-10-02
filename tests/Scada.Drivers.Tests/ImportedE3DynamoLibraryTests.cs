using Scada.Api.Runtime;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class ImportedE3DynamoLibraryTests
{
    [Fact]
    public void ImportedCandidates_ArePortableNormalizedAndHaveUniqueStableIdentities()
    {
        var definitions = ImportedE3DynamoLibrary.Create();

        Assert.Equal(27, definitions.Count);
        Assert.Equal(definitions.Count, definitions.Select(definition => definition.Key).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(definitions.Count, definitions.Select(definition => definition.Id).Distinct().Count());
        Assert.All(definitions, definition =>
        {
            Assert.Equal("true", definition.Metadata!["importedDynamoLibrary"]);
            Assert.Equal("elipse-e3-import", definition.Metadata["assetOrigin"]);
            Assert.Equal(ImportedE3DynamoLibrary.Version, definition.Properties!["libraryVersion"]);
            Assert.NotEmpty(definition.Elements!);
            var allNodes = FlattenIncludingGroups(definition.Elements!).ToArray();
            Assert.Equal(allNodes.Length, allNodes.Select(element => element.Id).Distinct().Count());
            var leaves = Flatten(definition.Elements!).OrderBy(element => element.Properties!["zIndex"].GetDouble()).ToArray();
            Assert.All(leaves.Select((element, index) => (element, index)), item =>
            {
                Assert.Contains(item.element.Type, new[] { "core.rectangle", "core.ellipse", "core.line", "core.arc", "core.bezier", "core.polygon", "core.text" });
                Assert.NotNull(item.element.Id);
                Assert.Equal((double)item.index, item.element.Properties!["zIndex"].GetDouble());
            });
            Assert.DoesNotContain(definition.Metadata.Values, value => value.Contains(":\\", StringComparison.Ordinal));
        });
        Assert.Contains(definitions, definition => FlattenIncludingGroups(definition.Elements!).Any(element =>
            element.Type == "core.group" && element.Children?.Count > 0));
    }

    [Fact]
    public void ImportedCurvedPolygon_IsAvailableAsNativeBezierGeometryAndLineColorsUseBorderColor()
    {
        var reservoir = Assert.Single(ImportedE3DynamoLibrary.Create(), definition => definition.Key == "e3.process.water-reservoir");
        var curve = Assert.Single(Flatten(reservoir.Elements!), element => element.Type == "core.bezier");
        Assert.Equal("M 0 20 C 20 0 80 0 100 20 L 100 100 L 0 100 Z", curve.Properties!["bezierPath"].GetString());

        var floculator = Assert.Single(ImportedE3DynamoLibrary.Create(), definition => definition.Key == "e3.process.flocculator");
        var importedLine = Assert.Single(Flatten(floculator.Elements!), element => element.Key == "e3-048");
        Assert.Equal("#1E1E1E", importedLine.Properties!["strokeColor"].GetString());
        Assert.Equal(0.5, importedLine.Properties["strokeWidth"].GetDouble());

        var pump = Assert.Single(ImportedE3DynamoLibrary.Create(), definition => definition.Key == "e3.process.pump-3");
        Assert.Contains(Flatten(pump.Elements!), element =>
            element.Type == "core.polygon" &&
            element.Properties!.TryGetValue("points", out var points) && points.GetArrayLength() >= 3 &&
            element.Properties.TryGetValue("fillStyle", out var fillStyle) && fillStyle.GetString() == "solid" &&
            element.Properties.TryGetValue("fillColor", out var fillColor) && fillColor.GetString() == "#B4B4B4" &&
            element.Properties.TryGetValue("polygonFillRule", out var fillRule) && fillRule.GetString() == "evenodd");
    }

    [Fact]
    public void ImportedMotorVariants_PreserveElipseLayerColorsAndExposeArcGeometry()
    {
        var motor = Assert.Single(ImportedE3DynamoLibrary.Create(), definition => definition.Key == "e3.process.motor-1");

        Assert.Contains(motor.Parameters!, parameter => parameter.Key == "motorRunning" && parameter.Kind == DynamoParameterKind.TagReference);
        Assert.Contains(motor.Parameters!, parameter => parameter.Key == "motorFault" && parameter.Kind == DynamoParameterKind.TagReference);

        var motorElements = Flatten(motor.Elements!).ToArray();
        var stopped = motorElements.Where(element => element.Bindings?.Any(binding => binding.Metadata?.GetValueOrDefault("dynamoParameter") == "motorRunning") == false &&
            element.Properties!.TryGetValue("fillColor", out var fill) && fill.ValueKind == System.Text.Json.JsonValueKind.String).ToArray();
        Assert.NotEmpty(stopped);
        Assert.Contains(motorElements, element =>
            element.Type == "core.arc" &&
            element.Properties!.ContainsKey("arcStartAngle") && element.Properties.ContainsKey("arcEndAngle"));
        Assert.Contains(motorElements, element =>
            element.Properties!.TryGetValue("fillColor", out var fill) && fill.GetString() == "#DD1B00" &&
            element.Properties.TryGetValue("fillStyle", out var style) && style.GetString() == "gradient" &&
            element.Properties.TryGetValue("visible", out var visible) && visible.ValueKind == System.Text.Json.JsonValueKind.False &&
            element.Bindings?.Any(binding => binding.Metadata?.GetValueOrDefault("dynamoParameter") == "motorRunning") == true);
        Assert.Contains(motorElements, element =>
            element.Properties!.TryGetValue("fillColor", out var fill) && fill.GetString() == "#C0C000" &&
            element.Properties.TryGetValue("fillStyle", out var style) && style.GetString() == "gradient" &&
            element.Bindings?.Any(binding => binding.Metadata?.GetValueOrDefault("dynamoParameter") == "motorFault") == true);
    }

    private static IEnumerable<VisualElementEngineeringDto> Flatten(IEnumerable<VisualElementEngineeringDto> elements) =>
        elements.SelectMany(element => element.Type == "core.group"
            ? Flatten(element.Children ?? Array.Empty<VisualElementEngineeringDto>())
            : new[] { element });

    private static IEnumerable<VisualElementEngineeringDto> FlattenIncludingGroups(IEnumerable<VisualElementEngineeringDto> elements) =>
        elements.SelectMany(element => new[] { element }.Concat(
            FlattenIncludingGroups(element.Children ?? Array.Empty<VisualElementEngineeringDto>())));
}
