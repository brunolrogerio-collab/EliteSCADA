using Scada.Engineering.VisualScripting;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class VisualPropertyCatalogConvergenceTests
{
    [Fact]
    public void CommonCatalog_ExposesTheCurrentCanonicalCrossLanguagePropertyKeys()
    {
        var keys = CommonVisualPropertyDefinitions.Geometry
            .Concat(CommonVisualPropertyDefinitions.Transform)
            .Concat(CommonVisualPropertyDefinitions.Visibility)
            .Concat(CommonVisualPropertyDefinitions.Fill)
            .Concat(CommonVisualPropertyDefinitions.Stroke)
            .Concat(CommonVisualPropertyDefinitions.Effects)
            .Concat(CommonVisualPropertyDefinitions.Text)
            .Concat(CommonVisualPropertyDefinitions.Image)
            .Concat(CommonVisualPropertyDefinitions.Slider)
            .Select(property => property.Key)
            .ToArray();

        Assert.Equal(
        [
            "x", "y", "width", "height", "zIndex",
            "rotation", "scaleX", "scaleY", "horizontalFlip", "verticalFlip",
            "visible", "opacity", "tooltip", "enabled",
            "fillStyle", "fillColor", "fillSecondaryColor", "gradientDirection", "backgroundColor",
            "strokeColor", "strokeWidth", "strokeStyle", "cornerRadius",
            "shadowEnabled", "shadowColor", "shadowOffsetX", "shadowOffsetY", "shadowBlur",
            "text", "textColor", "fontFamily", "fontSize", "fontWeight", "fontStyle",
            "underline", "textWrap", "lineHeight", "textOverflow",
            "horizontalAlignment", "verticalAlignment",
            "assetRef", "imageFit", "imagePositionX", "imagePositionY",
            "value", "minimum", "maximum", "step", "orientation", "interactionEnabled",
            "reverseDirection", "trackColor", "thumbColor"
        ], keys);

        Assert.DoesNotContain(VisualPropertyKeys.ImageResourceId, keys);
    }
}
