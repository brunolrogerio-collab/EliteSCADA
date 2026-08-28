using System.Text.Json;
using Scada.Engineering.Contracts;
using Scada.Engineering.Validation;
using Scada.Engineering.Views;

namespace Scada.Core.Tests;

public sealed class VisualElementEngineeringIdentityTests
{
    [Fact]
    public void Registry_AssignsAndPreservesIdsForLegacyVisualElements()
    {
        var registry = new InMemoryEngineeringViewRegistry();

        registry.UpsertScreen(new ScreenEngineeringDto(
            null,
            "plant.overview",
            "Plant Overview",
            Elements:
            [
                new VisualElementEngineeringDto(
                    "pump01",
                    "group",
                    Children:
                    [
                        new VisualElementEngineeringDto("label", "text")
                    ])
            ]));

        var first = Assert.Single(registry.SnapshotScreens());
        var firstRoot = Assert.Single(first.Elements!);
        var firstChild = Assert.Single(firstRoot.Children!);
        Assert.NotNull(first.Id);
        Assert.NotEqual(Guid.Empty, firstRoot.Id);
        Assert.NotEqual(Guid.Empty, firstChild.Id);
        Assert.NotEqual(firstRoot.Id, firstChild.Id);

        // Simulates a schema-v10 update that still does not know element IDs.
        registry.UpsertScreen(new ScreenEngineeringDto(
            null,
            "plant.overview",
            "Plant Overview Updated",
            Elements:
            [
                new VisualElementEngineeringDto(
                    "pump01",
                    "group",
                    Children:
                    [
                        new VisualElementEngineeringDto(
                            "label",
                            "text",
                            Properties: new() { ["text"] = JsonSerializer.SerializeToElement("P-01") })
                    ])
            ]));

        var second = Assert.Single(registry.SnapshotScreens());
        var secondRoot = Assert.Single(second.Elements!);
        var secondChild = Assert.Single(secondRoot.Children!);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(firstRoot.Id, secondRoot.Id);
        Assert.Equal(firstChild.Id, secondChild.Id);
        Assert.Equal("P-01", secondChild.Properties!["text"].GetString());
    }

    [Fact]
    public void Registry_PreservesSuppliedObjectIdAcrossDeveloperKeyRename()
    {
        var registry = new InMemoryEngineeringViewRegistry();
        var screenId = Guid.NewGuid();
        var objectId = Guid.NewGuid();

        registry.UpsertScreen(new ScreenEngineeringDto(
            screenId,
            "plant.overview",
            "Plant Overview",
            Elements: [new VisualElementEngineeringDto("oldKey", "text", Id: objectId)]));

        registry.UpsertScreen(new ScreenEngineeringDto(
            screenId,
            "plant.overview",
            "Plant Overview",
            Elements: [new VisualElementEngineeringDto("newKey", "text", Id: objectId)]));

        var element = Assert.Single(Assert.Single(registry.SnapshotScreens()).Elements!);
        Assert.Equal(objectId, element.Id);
        Assert.Equal("newKey", element.Key);
    }

    [Fact]
    public void Validator_RejectsDuplicateStableIdsAcrossNestedTree()
    {
        var duplicated = Guid.NewGuid();
        var screen = new ScreenEngineeringDto(
            null,
            "plant.overview",
            "Plant Overview",
            Elements:
            [
                new VisualElementEngineeringDto(
                    "group01",
                    "group",
                    Children: [new VisualElementEngineeringDto("label", "text", Id: duplicated)],
                    Id: duplicated)
            ]);

        var issues = EngineeringValidator.ValidateScreen(screen);

        Assert.Contains(issues, issue => issue.Code == "VISUAL_ELEMENT_ID_DUPLICATE");
    }

    [Fact]
    public void Validator_AllowsMissingIdsForLegacyInputButRejectsEmptyGuid()
    {
        var legacy = EngineeringValidator.ValidateScreen(new ScreenEngineeringDto(
            null,
            "legacy.screen",
            "Legacy Screen",
            Elements: [new VisualElementEngineeringDto("label", "text")]));
        Assert.DoesNotContain(legacy, issue => issue.Code.StartsWith("VISUAL_ELEMENT_ID_", StringComparison.Ordinal));

        var empty = EngineeringValidator.ValidateScreen(new ScreenEngineeringDto(
            null,
            "invalid.screen",
            "Invalid Screen",
            Elements: [new VisualElementEngineeringDto("label", "text", Id: Guid.Empty)]));
        Assert.Contains(empty, issue => issue.Code == "VISUAL_ELEMENT_ID_EMPTY");
    }
}
