using Scada.Engineering.VisualScripting;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class BuiltinVisualObjectSchemasTests
{
    [Fact]
    public void BuiltinTypes_AreStableUniqueAndMatchTheGraphicalEditorFoundationSet()
    {
        Assert.Equal(
        [
            "core.group",
            "core.rectangle",
            "core.ellipse",
            "core.line",
            "core.arc",
            "core.bezier",
            "core.polygon",
            "core.text",
            "core.image",
            "core.videoPlayer",
            "core.pdfViewer",
            "core.svgSymbol",
            "core.valueDisplay",
            "core.trend",
            "core.alarmBrowser",
            "core.eventBrowser",
            "core.button",
            "core.slider",
            "core.numericInput"
        ], BuiltinVisualObjectSchemas.All.Select(schema => schema.ObjectTypeKey).ToArray());
    }

    [Fact]
    public void BuiltinSchemas_UseTheSharedPropertyContractRatherThanPrivateObjectProperties()
    {
        Assert.True(BuiltinVisualObjectSchemas.Rectangle.Declares(VisualPropertyKeys.FillColor));
        Assert.True(BuiltinVisualObjectSchemas.Rectangle.Declares(VisualPropertyKeys.StrokeStyle));
        Assert.False(BuiltinVisualObjectSchemas.Rectangle.Declares(VisualPropertyKeys.AssetRef));
        Assert.True(BuiltinVisualObjectSchemas.Bezier.Declares(VisualPropertyKeys.BezierPath));
        Assert.True(BuiltinVisualObjectSchemas.SupportsAnalogFill(BuiltinVisualObjectSchemas.BezierType));

        Assert.True(BuiltinVisualObjectSchemas.Image.Declares(VisualPropertyKeys.AssetRef));
        Assert.True(BuiltinVisualObjectSchemas.Image.Declares(VisualPropertyKeys.ImageFit));
        Assert.True(BuiltinVisualObjectSchemas.Image.Declares(VisualPropertyKeys.ImagePositionX));
        Assert.True(BuiltinVisualObjectSchemas.Image.Declares(VisualPropertyKeys.ImageZoom));
        Assert.False(BuiltinVisualObjectSchemas.Image.Declares(VisualPropertyKeys.Text));
        Assert.True(BuiltinVisualObjectSchemas.VideoPlayer.Declares(VisualPropertyKeys.MediaSourceId));
        Assert.True(BuiltinVisualObjectSchemas.PdfViewer.Declares(VisualPropertyKeys.PdfZoom));

        Assert.True(BuiltinVisualObjectSchemas.Text.Declares(VisualPropertyKeys.FontFamily));
        Assert.True(BuiltinVisualObjectSchemas.Text.Declares(VisualPropertyKeys.HorizontalAlignment));

        Assert.True(BuiltinVisualObjectSchemas.Trend.Declares(VisualPropertyKeys.BackgroundColor));
        Assert.True(BuiltinVisualObjectSchemas.Trend.Declares(VisualPropertyKeys.StrokeColor));
        Assert.False(BuiltinVisualObjectSchemas.Trend.Declares(BuiltinVisualObjectSchemas.TrendPensProperty));

        Assert.True(BuiltinVisualObjectSchemas.Button.Declares(VisualPropertyKeys.BackgroundColor));
        Assert.True(BuiltinVisualObjectSchemas.Button.Declares(VisualPropertyKeys.CornerRadius));
        Assert.True(BuiltinVisualObjectSchemas.Button.Declares(VisualPropertyKeys.Text));

        Assert.True(BuiltinVisualObjectSchemas.Slider.Declares(VisualPropertyKeys.Value));
        Assert.True(BuiltinVisualObjectSchemas.Slider.Declares(VisualPropertyKeys.Minimum));
        Assert.True(BuiltinVisualObjectSchemas.Slider.Declares(VisualPropertyKeys.Maximum));
        Assert.True(BuiltinVisualObjectSchemas.Slider.Declares(VisualPropertyKeys.Step));
        Assert.True(BuiltinVisualObjectSchemas.Slider.Declares(VisualPropertyKeys.InteractionEnabled));

        Assert.True(BuiltinVisualObjectSchemas.NumericInput.Declares(VisualPropertyKeys.ShowApplyButton));
        Assert.False(BuiltinVisualObjectSchemas.Slider.Declares(VisualPropertyKeys.ShowApplyButton));
    }

    [Fact]
    public void UnknownBuiltinType_FailsClosed()
    {
        Assert.Throws<KeyNotFoundException>(() => BuiltinVisualObjectSchemas.GetRequired("core.mystery"));
    }
}
