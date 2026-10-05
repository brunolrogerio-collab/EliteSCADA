using Scada.Api.VisualAssets;
using Xunit;

namespace Scada.Drivers.Tests;

public sealed class StaticArtworkCatalogTests
{
    [Fact]
    public void OriginalFactoryCatalogIsEmbeddedCategorizedAndNotFalselyApproved()
    {
        var catalog = new StaticArtworkCatalog();
        Assert.True(catalog.Entries.Count > 1200);
        Assert.True(catalog.Entries.Select(entry => entry.Category).Distinct().Count() > 20);
        Assert.All(catalog.Entries, entry => Assert.Equal("draft", entry.Status));
        Assert.Null(catalog.Content("../../secret"));
    }

    [Fact]
    public void EveryShippedFactoryPreviewPassesCanonicalStaticSvgSanitization()
    {
        var catalog = new StaticArtworkCatalog();
        foreach (var entry in catalog.Entries) {
            var content = catalog.Content(entry.Id);
            Assert.NotNull(content);
            Assert.NotEmpty(content);
        }
    }

    [Fact]
    public void ProjectCopyPreservesCategoryProvenanceAndCanonicalPaintMetadata()
    {
        var catalog = new StaticArtworkCatalog();
        var entry = catalog.Entries.First(item => item.Category == "industrial/rotating/motors");
        var copy = catalog.ProjectCopy(entry.Id)!.Value;
        Assert.Equal($"factory.{entry.Id}", copy.Asset.Key);
        Assert.Equal(entry.Category, copy.Asset.Metadata!["categoryPath"]);
        Assert.Equal(entry.Status, copy.Asset.Metadata["artworkReviewStatus"]);
        Assert.Equal(entry.Id, copy.Asset.Metadata["factoryArtworkId"]);
        Assert.False(copy.Asset.Metadata.ContainsKey("builtinLibrary"));
        var registry = new Scada.Engineering.VisualAssets.InMemoryVisualAssetEngineeringRegistry();
        var context = new Scada.Engineering.VisualAssets.EngineeringImportContext(
            new Dictionary<string, Scada.Engineering.VisualAssets.VisualAssetPayload> { [copy.Payload.Sha256] = copy.Payload });
        Assert.DoesNotContain(Scada.Engineering.VisualAssets.VisualAssetEngineeringValidator.Validate(copy.Asset, registry, context), issue => issue.IsError);
        Assert.Null(catalog.ProjectCopy("../../secret"));
    }
}
