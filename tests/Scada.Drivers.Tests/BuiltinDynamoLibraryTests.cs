using Scada.Api.Runtime;

namespace Scada.Drivers.Tests;

public sealed class BuiltinDynamoLibraryTests
{
    [Fact]
    public void Library_ProvidesRepresentativeInsertableDefinitionsAcrossIndustrialFamilies()
    {
        var definitions = BuiltinDynamoLibrary.Create();

        Assert.Equal(30, definitions.Count);
        Assert.Equal(definitions.Count, definitions.Select(definition => definition.Id).Distinct().Count());
        Assert.Equal(definitions.Count, definitions.Select(definition => definition.Key).Distinct(StringComparer.Ordinal).Count());

        var categories = definitions
            .GroupBy(definition => definition.Properties!["category"], StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        Assert.Equal(6, categories["pump"]);
        Assert.Equal(6, categories["motor"]);
        Assert.Equal(6, categories["valve"]);
        Assert.Equal(6, categories["tank"]);
        Assert.Equal(3, categories["compressor"]);
        Assert.Equal(3, categories["instrument"]);
        Assert.All(definitions, definition =>
        {
            Assert.NotEmpty(definition.Elements!);
            Assert.Equal("true", definition.Metadata!["builtinLibrary"]);
            Assert.Equal("original-elitescada-vector", definition.Metadata!["assetOrigin"]);
            Assert.Equal("1.2.0", definition.Properties!["libraryVersion"]);
            Assert.True(double.Parse(definition.Properties!["defaultWidth"], System.Globalization.CultureInfo.InvariantCulture) > 0);
            Assert.True(double.Parse(definition.Properties!["defaultHeight"], System.Globalization.CultureInfo.InvariantCulture) > 0);
            Assert.Contains(definition.Properties!["visualStyle"], new[] { "detailed-2d", "dimensional-front", "high-performance" });
            Assert.Equal(definition.Properties!["visualStyle"], definition.Metadata!["visualStyle"]);
            Assert.Equal("front-orthographic", definition.Metadata!["view"]);
        });

        var styleCounts = definitions
            .GroupBy(definition => definition.Properties!["visualStyle"], StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        Assert.Equal(10, styleCounts["detailed-2d"]);
        Assert.Equal(10, styleCounts["dimensional-front"]);
        Assert.Equal(10, styleCounts["high-performance"]);

        var dimensional = definitions.Where(definition => definition.Properties!["visualStyle"] == "dimensional-front").ToArray();
        Assert.All(dimensional, definition =>
            Assert.Contains(definition.Elements!, element =>
                element.Properties is not null &&
                element.Properties.TryGetValue("fillStyle", out var fillStyle) &&
                string.Equals(fillStyle.GetString(), "gradient", StringComparison.Ordinal)));

        var highPerformance = definitions.Where(definition => definition.Properties!["visualStyle"] == "high-performance").ToArray();
        Assert.All(highPerformance, definition =>
            Assert.DoesNotContain(definition.Elements!, element =>
                element.Properties is not null &&
                element.Properties.TryGetValue("fillStyle", out var fillStyle) &&
                string.Equals(fillStyle.GetString(), "gradient", StringComparison.Ordinal)));
    }

    [Fact]
    public void Workspace_SeedsTheBuiltInLibraryAndKeepsEquipmentBindingsParameterized()
    {
        using var workspace = new EngineeringWorkspace();

        var definitions = workspace.Assets.SnapshotDynamos();
        Assert.Equal(30, definitions.Count);
        var targets = definitions
            .SelectMany(definition => definition.Elements ?? [])
            .SelectMany(element => element.Bindings ?? [])
            .Select(binding => binding.Target)
            .ToArray();

        Assert.NotEmpty(targets);
        Assert.All(targets, target => Assert.StartsWith("{equipmentPath}.", target));
    }
}
