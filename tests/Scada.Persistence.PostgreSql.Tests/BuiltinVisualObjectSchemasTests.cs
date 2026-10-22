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
            "core.model3d",
            "core.videoPlayer",
            "core.pdfViewer",
            "core.reportLauncher",
            "core.svgSymbol",
            "core.valueDisplay",
            "core.trend",
            "core.alarmBrowser",
            "core.eventBrowser",
            "core.button",
            "core.slider",
            "core.numericInput",
            "core.measurementDial",
            "core.measurementRing",
            "core.measurementBar",
            "core.measurementThermometer",
            "core.measurementBands",
            "core.measurementTarget",
            "core.intelligencePerformanceV1B",
            "core.intelligenceProfileCoverageV1"
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
        Assert.True(BuiltinVisualObjectSchemas.Model3d.Declares(VisualPropertyKeys.AssetRef));
        Assert.True(BuiltinVisualObjectSchemas.Model3d.Declares(VisualPropertyKeys.Visible));
        Assert.True(BuiltinVisualObjectSchemas.Image.Declares(VisualPropertyKeys.ImageFit));
        Assert.True(BuiltinVisualObjectSchemas.Image.Declares(VisualPropertyKeys.ImagePositionX));
        Assert.True(BuiltinVisualObjectSchemas.Image.Declares(VisualPropertyKeys.ImageZoom));
        Assert.False(BuiltinVisualObjectSchemas.Image.Declares(VisualPropertyKeys.Text));
        Assert.True(BuiltinVisualObjectSchemas.VideoPlayer.Declares(VisualPropertyKeys.MediaSourceId));
        Assert.True(BuiltinVisualObjectSchemas.PdfViewer.Declares(VisualPropertyKeys.PdfZoom));
        Assert.False(BuiltinVisualObjectSchemas.VideoPlayer.Declares(VisualPropertyKeys.ReportKey));
        Assert.False(BuiltinVisualObjectSchemas.PdfViewer.Declares(VisualPropertyKeys.ReportKey));
        Assert.True(BuiltinVisualObjectSchemas.ReportLauncher.Declares(VisualPropertyKeys.ReportKey));
        Assert.True(BuiltinVisualObjectSchemas.ReportLauncher.Declares(VisualPropertyKeys.Text));

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

        foreach (var schema in new[]
        {
            BuiltinVisualObjectSchemas.MeasurementDial,
            BuiltinVisualObjectSchemas.MeasurementRing,
            BuiltinVisualObjectSchemas.MeasurementBar,
            BuiltinVisualObjectSchemas.MeasurementThermometer,
            BuiltinVisualObjectSchemas.MeasurementBands,
            BuiltinVisualObjectSchemas.MeasurementTarget
        })
        {
            Assert.True(schema.Declares(VisualPropertyKeys.Value));
            Assert.True(schema.Declares(VisualPropertyKeys.MeasurementUnit));
            Assert.True(schema.Declares(VisualPropertyKeys.Minimum));
            Assert.True(schema.Declares(VisualPropertyKeys.Maximum));
            Assert.True(schema.Declares(VisualPropertyKeys.MeasurementQualityVisible));
            Assert.True(schema.Declares(VisualPropertyKeys.MeasurementFreshnessVisible));
            Assert.True(schema.Declares(VisualPropertyKeys.MeasurementTheme));
            Assert.False(schema.Declares(VisualPropertyKeys.InteractionEnabled));
        }
        Assert.True(BuiltinVisualObjectSchemas.MeasurementTarget.Declares(VisualPropertyKeys.MeasurementTarget));
        Assert.True(BuiltinVisualObjectSchemas.MeasurementBands.Declares(VisualPropertyKeys.MeasurementBandOneEnd));
        Assert.True(BuiltinVisualObjectSchemas.MeasurementBands.Declares(VisualPropertyKeys.MeasurementBandTwoEnd));

        var intelligence = BuiltinVisualObjectSchemas.IntelligencePerformanceV1B;
        Assert.True(intelligence.Declares(VisualPropertyKeys.IntelligenceScopeId));
        Assert.True(intelligence.Declares(VisualPropertyKeys.IntelligenceAssignmentId));
        Assert.True(intelligence.Declares(VisualPropertyKeys.IntelligenceProfileId));
        Assert.True(intelligence.Declares(VisualPropertyKeys.IntelligenceRuleId));
        Assert.True(intelligence.Declares(VisualPropertyKeys.IntelligenceRuleVersion));
        Assert.True(intelligence.Declares(VisualPropertyKeys.IntelligenceSignalIds));
        Assert.All(intelligence.Properties.Values.Where(property => property.Key.StartsWith("intelligence", StringComparison.Ordinal)), property =>
        {
            Assert.False(property.EngineeringEditable);
            Assert.False(property.RuntimeWritable);
            Assert.False(property.SupportsBinding);
        });

        var coverage = BuiltinVisualObjectSchemas.IntelligenceProfileCoverageV1;
        Assert.True(coverage.Declares(VisualPropertyKeys.IntelligenceScopeId));
        Assert.True(coverage.Declares(VisualPropertyKeys.IntelligenceProfileId));
        Assert.False(coverage.Declares(VisualPropertyKeys.IntelligenceAssignmentId));
        Assert.All(coverage.Properties.Values.Where(property => property.Key.StartsWith("intelligence", StringComparison.Ordinal)), property =>
        {
            Assert.False(property.EngineeringEditable);
            Assert.False(property.RuntimeWritable);
            Assert.False(property.SupportsBinding);
        });
    }

    [Fact]
    public void UnknownBuiltinType_FailsClosed()
    {
        Assert.Throws<KeyNotFoundException>(() => BuiltinVisualObjectSchemas.GetRequired("core.mystery"));
    }
}
