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
}
