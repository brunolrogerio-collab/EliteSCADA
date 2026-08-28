using Scada.Engineering.VisualScripting;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class VisualPropertyCatalogConvergenceTests
{
    [Fact]
    public void CommonCatalog_ExposesTheWave07CrossLanguagePropertyKeys()
    {
        var keys = CommonVisualPropertyDefinitions.Geometry
            .Concat(CommonVisualPropertyDefinitions.Transform)
            .Concat(CommonVisualPropertyDefinitions.Visibility)
            .Concat(CommonVisualPropertyDefinitions.Fill)
            .Concat(CommonVisualPropertyDefinitions.Stroke)
            .Concat(CommonVisualPropertyDefinitions.Text)
            .Concat(CommonVisualPropertyDefinitions.Image)
            .Select(property => property.Key)
            .ToArray();

        Assert.Equal(
        [
            "x", "y", "width", "height", "zIndex",
            "rotation", "scaleX", "scaleY",
            "visible", "opacity",
            "fillColor", "backgroundColor",
            "strokeColor", "strokeWidth", "strokeStyle", "cornerRadius",
            "text", "textColor", "fontFamily", "fontSize", "fontWeight", "fontStyle",
            "horizontalAlignment", "verticalAlignment",
            "assetRef", "imageFit", "imagePositionX", "imagePositionY"
        ], keys);

        Assert.DoesNotContain(VisualPropertyKeys.ImageResourceId, keys);
    }
}
