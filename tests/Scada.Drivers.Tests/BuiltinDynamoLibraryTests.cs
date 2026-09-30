using Scada.Api.Runtime;

namespace Scada.Drivers.Tests;

public sealed class BuiltinDynamoLibraryTests
{
    [Fact]
    public void Library_ProvidesRepresentativeInsertableDefinitionsAcrossIndustrialFamilies()
    {
        var definitions = BuiltinDynamoLibrary.Create();

        Assert.Equal(10, definitions.Count);
        Assert.Equal(definitions.Count, definitions.Select(definition => definition.Id).Distinct().Count());
        Assert.Equal(definitions.Count, definitions.Select(definition => definition.Key).Distinct(StringComparer.Ordinal).Count());

        var categories = definitions
            .GroupBy(definition => definition.Properties!["category"], StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        Assert.Equal(2, categories["pump"]);
        Assert.Equal(2, categories["motor"]);
        Assert.Equal(2, categories["valve"]);
        Assert.Equal(2, categories["tank"]);
        Assert.Equal(1, categories["compressor"]);
        Assert.Equal(1, categories["instrument"]);
        Assert.All(definitions, definition =>
        {
            Assert.NotEmpty(definition.Elements!);
            Assert.Equal("true", definition.Metadata!["builtinLibrary"]);
            Assert.Equal("original-elitescada-vector", definition.Metadata!["assetOrigin"]);
            Assert.Equal("1.1.0", definition.Properties!["libraryVersion"]);
            Assert.True(double.Parse(definition.Properties!["defaultWidth"], System.Globalization.CultureInfo.InvariantCulture) > 0);
            Assert.True(double.Parse(definition.Properties!["defaultHeight"], System.Globalization.CultureInfo.InvariantCulture) > 0);
        });
    }

    [Fact]
    public void Workspace_SeedsTheBuiltInLibraryAndKeepsEquipmentBindingsParameterized()
    {
        using var workspace = new EngineeringWorkspace();

        var definitions = workspace.Assets.SnapshotDynamos();
        Assert.Equal(10, definitions.Count);
        var targets = definitions
            .SelectMany(definition => definition.Elements ?? [])
            .SelectMany(element => element.Bindings ?? [])
            .Select(binding => binding.Target)
            .ToArray();

        Assert.NotEmpty(targets);
        Assert.All(targets, target => Assert.StartsWith("{equipmentPath}.", target));
    }
}
