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
            .Concat(CommonVisualPropertyDefinitions.NumericInput)
            .Select(property => property.Key)
            .ToArray();

        Assert.Equal(
        [
            "x", "y", "width", "height", "zIndex",
            "rotation", "arcStartAngle", "arcEndAngle", "scaleX", "scaleY", "horizontalFlip", "verticalFlip",
            "visible", "opacity", "tooltip", "enabled",
            "fillStyle", "fillColor", "fillSecondaryColor", "gradientDirection", "backgroundColor",
            "strokeColor", "strokeWidth", "strokeStyle", "cornerRadius",
            "shadowEnabled", "shadowColor", "shadowOffsetX", "shadowOffsetY", "shadowBlur",
            "text", "textColor", "textColorEditing", "textColorGood", "textColorBad", "valueFormat",
            "fontFamily", "fontSize", "fontWeight", "fontStyle",
            "underline", "textWrap", "lineHeight", "textOverflow",
            "horizontalAlignment", "verticalAlignment",
            "assetRef", "imageFit", "imagePositionX", "imagePositionY", "imageZoom",
            "value", "minimum", "maximum", "step", "orientation", "interactionEnabled",
            "reverseDirection", "trackColor", "thumbColor",
            "showApplyButton", "decimalPlacesEnabled", "decimalPlaces"
        ], keys);

        Assert.DoesNotContain(VisualPropertyKeys.ImageResourceId, keys);
    }
}
