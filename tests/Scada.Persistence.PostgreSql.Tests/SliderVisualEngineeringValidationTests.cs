using System.Text.Json;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.VisualScripting;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class SliderVisualEngineeringValidationTests
{
    [Fact]
    public void PassiveSlider_AcceptsDisplayOnlyValueBinding()
    {
        var element = Slider(
            properties: new Dictionary<string, JsonElement>
            {
                [VisualPropertyKeys.Minimum] = JsonSerializer.SerializeToElement(0d),
                [VisualPropertyKeys.Maximum] = JsonSerializer.SerializeToElement(100d),
                [VisualPropertyKeys.Step] = JsonSerializer.SerializeToElement(0.5d)
            },
            binding: new EngineeringBindingDto(
                VisualPropertyKeys.Value,
                EngineeringBindingKind.Tag,
                "Plant.Setpoint",
                "read",
                TagReference: new TagValueReference(Guid.NewGuid())));

        var issues = BuiltinVisualEngineeringValidation.Validate(
            element,
            ImportEntityKind.Screen,
            "overview",
            EngineeringExchangeService.CurrentSchemaVersion);

        Assert.Empty(issues.Where(issue => issue.IsError));
    }

    [Fact]
    public void InteractiveSlider_RequiresStableWritableTagBinding()
    {
        var element = Slider(
            properties: new Dictionary<string, JsonElement>
            {
                [VisualPropertyKeys.InteractionEnabled] = JsonSerializer.SerializeToElement(true)
            },
            binding: new EngineeringBindingDto(
                VisualPropertyKeys.Value,
                EngineeringBindingKind.Tag,
                "Plant.Setpoint",
                "read"));

        var issues = BuiltinVisualEngineeringValidation.Validate(
            element,
            ImportEntityKind.Screen,
            "overview",
            EngineeringExchangeService.CurrentSchemaVersion);

        Assert.Contains(issues, issue => issue.Code == "VISUAL_SLIDER_WRITABLE_TAG_REQUIRED");
    }

    [Fact]
    public void Slider_RejectsInvalidCrossPropertyRange()
    {
        var element = Slider(new Dictionary<string, JsonElement>
        {
            [VisualPropertyKeys.Minimum] = JsonSerializer.SerializeToElement(10d),
            [VisualPropertyKeys.Maximum] = JsonSerializer.SerializeToElement(10d)
        });

        var issues = BuiltinVisualEngineeringValidation.Validate(
            element,
            ImportEntityKind.Screen,
            "overview",
            EngineeringExchangeService.CurrentSchemaVersion);

        Assert.Contains(issues, issue => issue.Code == "VISUAL_SLIDER_RANGE_INVALID");
    }

    private static VisualElementEngineeringDto Slider(
        Dictionary<string, JsonElement>? properties = null,
        EngineeringBindingDto? binding = null) =>
        new(
            "setpoint",
            BuiltinVisualObjectSchemas.SliderType,
            Bindings: binding is null ? null : [binding],
            Properties: properties);
}
