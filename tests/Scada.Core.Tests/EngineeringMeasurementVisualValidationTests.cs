using System.Text.Json;
using Scada.Engineering.Contracts;
using Scada.Engineering.VisualScripting;

namespace Scada.Core.Tests;

public sealed class EngineeringMeasurementVisualValidationTests
{
    [Theory]
    [InlineData(BuiltinVisualObjectSchemas.MeasurementDialType)]
    [InlineData(BuiltinVisualObjectSchemas.MeasurementRingType)]
    [InlineData(BuiltinVisualObjectSchemas.MeasurementBarType)]
    [InlineData(BuiltinVisualObjectSchemas.MeasurementThermometerType)]
    [InlineData(BuiltinVisualObjectSchemas.MeasurementBandsType)]
    [InlineData(BuiltinVisualObjectSchemas.MeasurementTargetType)]
    public void MeasurementDisplays_ValidateAndRoundTripThroughTheCanonicalScreenContract(string type)
    {
        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            [VisualPropertyKeys.Value] = Json("42.5"),
            [VisualPropertyKeys.MeasurementUnit] = Json("\"bar\""),
            [VisualPropertyKeys.Minimum] = Json("0"),
            [VisualPropertyKeys.Maximum] = Json("100"),
            [VisualPropertyKeys.MeasurementTheme] = Json("\"high-contrast\""),
            [VisualPropertyKeys.MeasurementQualityVisible] = Json("true"),
            [VisualPropertyKeys.MeasurementFreshnessVisible] = Json("true"),
            [VisualPropertyKeys.MeasurementStaleAfterSeconds] = Json("30")
        };
        if (type == BuiltinVisualObjectSchemas.MeasurementTargetType)
            properties[VisualPropertyKeys.MeasurementTarget] = Json("65");
        if (type == BuiltinVisualObjectSchemas.MeasurementBandsType)
        {
            properties[VisualPropertyKeys.MeasurementBandOneEnd] = Json("30");
            properties[VisualPropertyKeys.MeasurementBandTwoEnd] = Json("70");
        }

        var element = new VisualElementEngineeringDto("measurement", type, Properties: properties);
        var issues = BuiltinVisualEngineeringValidation.Validate(element, ImportEntityKind.Screen, "demo.overview", int.MaxValue);
        Assert.DoesNotContain(issues, issue => issue.IsError);

        var normalized = VisualEngineeringPropertyMigration.NormalizeCurrentElements([element])!.Single();
        Assert.Equal("bar", normalized.Properties![VisualPropertyKeys.MeasurementUnit].GetString());
        Assert.Equal("high-contrast", normalized.Properties[VisualPropertyKeys.MeasurementTheme].GetString());
    }

    [Fact]
    public void MeasurementDisplay_RejectsInvalidScaleTargetAndBandOrder()
    {
        var target = new VisualElementEngineeringDto(
            "target",
            BuiltinVisualObjectSchemas.MeasurementTargetType,
            Properties: new Dictionary<string, JsonElement>
            {
                [VisualPropertyKeys.Minimum] = Json("10"),
                [VisualPropertyKeys.Maximum] = Json("20"),
                [VisualPropertyKeys.MeasurementTarget] = Json("25")
            });
        var targetIssues = BuiltinVisualEngineeringValidation.Validate(target, ImportEntityKind.Screen, "demo.overview", int.MaxValue);
        Assert.Contains(targetIssues, issue => issue.Code == "VISUAL_MEASUREMENT_TARGET_OUT_OF_RANGE" && issue.IsError);

        var bands = new VisualElementEngineeringDto(
            "bands",
            BuiltinVisualObjectSchemas.MeasurementBandsType,
            Properties: new Dictionary<string, JsonElement>
            {
                [VisualPropertyKeys.Minimum] = Json("0"),
                [VisualPropertyKeys.Maximum] = Json("100"),
                [VisualPropertyKeys.MeasurementBandOneEnd] = Json("70"),
                [VisualPropertyKeys.MeasurementBandTwoEnd] = Json("30")
            });
        var bandIssues = BuiltinVisualEngineeringValidation.Validate(bands, ImportEntityKind.Screen, "demo.overview", int.MaxValue);
        Assert.Contains(bandIssues, issue => issue.Code == "VISUAL_MEASUREMENT_BANDS_INVALID" && issue.IsError);

        var invalidScale = new VisualElementEngineeringDto(
            "invalid",
            BuiltinVisualObjectSchemas.MeasurementDialType,
            Properties: new Dictionary<string, JsonElement>
            {
                [VisualPropertyKeys.Minimum] = Json("20"),
                [VisualPropertyKeys.Maximum] = Json("20")
            });
        var rangeIssues = BuiltinVisualEngineeringValidation.Validate(invalidScale, ImportEntityKind.Screen, "demo.overview", int.MaxValue);
        Assert.Contains(rangeIssues, issue => issue.Code == "VISUAL_MEASUREMENT_RANGE_INVALID" && issue.IsError);
    }

    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
