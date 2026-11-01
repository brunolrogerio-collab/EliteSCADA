using System.Text.Json;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.VisualScripting;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class VisualControl790ValidationTests
{
    [Fact]
    public void GenericCheckBox_RequiresWritableStableBooleanBindingOnlyWhenInteractionIsEnabled()
    {
        var passive = Element(BuiltinVisualObjectSchemas.CheckBoxType, new()
        {
            [VisualPropertyKeys.Checked] = Json("true")
        });
        Assert.DoesNotContain(Validate(passive), issue => issue.IsError);

        var calculatedBoolean = passive with
        {
            Bindings = [new EngineeringBindingDto(
                VisualPropertyKeys.Checked,
                EngineeringBindingKind.Expression,
                "Pump01.IsAvailable",
                "read")]
        };
        Assert.DoesNotContain(Validate(calculatedBoolean), issue => issue.IsError);

        var missingBinding = Element(BuiltinVisualObjectSchemas.CheckBoxType, new()
        {
            [VisualPropertyKeys.InteractionEnabled] = Json("true")
        });
        Assert.Contains(Validate(missingBinding), issue => issue.Code == "VISUAL_CHECKBOX_BOOLEAN_TAG_REQUIRED");

        var interactiveExpression = missingBinding with
        {
            Bindings = [new EngineeringBindingDto(
                VisualPropertyKeys.Checked,
                EngineeringBindingKind.Expression,
                "Pump01.IsAvailable",
                "readWrite")]
        };
        Assert.Contains(Validate(interactiveExpression), issue => issue.Code == "VISUAL_CHECKBOX_BOOLEAN_TAG_REQUIRED");

        var validBinding = missingBinding with
        {
            Bindings = [new EngineeringBindingDto(
                VisualPropertyKeys.Checked,
                EngineeringBindingKind.Tag,
                "Plant.Pump01.Enabled",
                "readWrite",
                TagReference: new TagValueReference(Guid.NewGuid()))]
        };
        Assert.DoesNotContain(Validate(validBinding), issue => issue.IsError);
    }

    [Fact]
    public void CheckBoxAlarmSuppression_ValidatesTypedTargetAndRejectsConflictWithGenericBinding()
    {
        var alarmId = Guid.Parse("c76c7e0b-3235-4a8b-936d-0e23a3f8b62e");
        var config = JsonSerializer.SerializeToElement(new
        {
            mode = "alarmSuppression",
            alarmDefinitionId = alarmId,
            reasonCode = "maintenance",
            duration = "4h"
        });
        var element = Element(BuiltinVisualObjectSchemas.CheckBoxType, new()
        {
            [BuiltinVisualObjectSchemas.CheckBoxConfigProperty] = config
        });
        Assert.DoesNotContain(Validate(element), issue => issue.IsError);

        var conflicting = element with
        {
            Bindings = [new EngineeringBindingDto(
                VisualPropertyKeys.Checked,
                EngineeringBindingKind.Tag,
                "Plant.Pump01.Enabled",
                "readWrite",
                TagReference: new TagValueReference(Guid.NewGuid()))]
        };
        Assert.Contains(Validate(conflicting), issue => issue.Code == "VISUAL_CHECKBOX_MODE_CONFLICT");

        var malformed = Element(BuiltinVisualObjectSchemas.CheckBoxType, new()
        {
            [BuiltinVisualObjectSchemas.CheckBoxConfigProperty] = Json("{\"mode\":7}")
        });
        Assert.Contains(Validate(malformed), issue => issue.Code == "VISUAL_CHECKBOX_SUPPRESSION_CONFIG_INVALID");
    }

    [Fact]
    public void InteractiveComboBox_RequiresOptionsAndRejectsDuplicateValues()
    {
        var noOptions = Element(BuiltinVisualObjectSchemas.ComboBoxType, new()
        {
            [VisualPropertyKeys.InteractionEnabled] = Json("true")
        });
        Assert.Contains(Validate(noOptions), issue => issue.Code == "VISUAL_COMBOBOX_OPTIONS_REQUIRED");

        var emptyOptions = Element(BuiltinVisualObjectSchemas.ComboBoxType, new()
        {
            [BuiltinVisualObjectSchemas.ComboBoxOptionsProperty] = Json("[]")
        });
        Assert.Contains(Validate(emptyOptions), issue => issue.Code == "VISUAL_COMBOBOX_OPTIONS_INVALID");

        var duplicateOptions = Element(BuiltinVisualObjectSchemas.ComboBoxType, new()
        {
            [BuiltinVisualObjectSchemas.ComboBoxOptionsProperty] = Json("""
            [
              {"value":"AUTO","label":"Automático"},
              {"value":"AUTO","label":"Modo auto"}
            ]
            """),
            [VisualPropertyKeys.InteractionEnabled] = Json("true")
        });
        Assert.Contains(Validate(duplicateOptions), issue => issue.Code == "VISUAL_COMBOBOX_OPTION_INVALID");
    }

    [Fact]
    public void StructuralControlSettings_SurviveCurrentSchemaMigration()
    {
        var options = Json("""
        [
          {"value":"AUTO","label":"Automático"},
          {"value":"MANUAL","label":"Manual"}
        ]
        """);
        var element = Element(BuiltinVisualObjectSchemas.ComboBoxType, new()
        {
            [BuiltinVisualObjectSchemas.ComboBoxOptionsProperty] = options
        });

        Assert.DoesNotContain(Validate(element), issue => issue.IsError);
        var normalized = VisualEngineeringPropertyMigration.NormalizeCurrentElements([element])!.Single();
        var migrated = normalized.Properties![BuiltinVisualObjectSchemas.ComboBoxOptionsProperty];
        Assert.Equal(JsonValueKind.Array, migrated.ValueKind);
        Assert.Equal("AUTO", migrated[0].GetProperty("value").GetString());
        Assert.Equal("Manual", migrated[1].GetProperty("label").GetString());
    }

    private static IReadOnlyCollection<ImportIssue> Validate(VisualElementEngineeringDto element) =>
        BuiltinVisualEngineeringValidation.Validate(
            element,
            ImportEntityKind.Screen,
            "controls",
            EngineeringExchangeService.CurrentSchemaVersion);

    private static VisualElementEngineeringDto Element(
        string type,
        Dictionary<string, JsonElement> properties) => new("control", type, Properties: properties);

    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
