using System.Text.Json;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Views;
using Scada.Engineering.VisualScripting;

namespace Scada.Drivers.Tests;

public sealed class VisualControls790EngineeringExchangeTests
{
    [Fact]
    public void CheckBoxAndComboBoxSettings_RoundTripThroughEngineeringExportAndImport()
    {
        var sourceTags = new InMemoryTagRegistry();
        var booleanTag = TagDefinition.Create("Pump enabled", "Plant.Pump01.Enabled", TagDataType.Boolean);
        var modeTag = TagDefinition.Create("Pump mode", "Plant.Pump01.Mode", TagDataType.Enum);
        sourceTags.Register(booleanTag);
        sourceTags.Register(modeTag);

        var sourceAlarmsBus = new InMemoryScadaEventBus();
        using var sourceAlarms = new InMemoryAlarmEngine(sourceAlarmsBus);
        var sourceAssets = new InMemoryEngineeringAssetRegistry();
        var sourceViews = new InMemoryEngineeringViewRegistry();

        sourceAssets.UpsertTemplate(new EquipmentTemplateEngineeringDto(
            null,
            "motor.faceplate",
            "Motor faceplate",
            Elements: [CreateComboBox(modeTag.Id)]));

        sourceViews.UpsertScreen(new ScreenEngineeringDto(
            null,
            "plant.overview",
            "Plant overview",
            "/plant",
            Elements:
            [
                new VisualElementEngineeringDto(
                    "available",
                    BuiltinVisualObjectSchemas.CheckBoxType,
                    Bindings:
                    [
                        new EngineeringBindingDto(
                            VisualPropertyKeys.Checked,
                            EngineeringBindingKind.Expression,
                            "Pump01.IsAvailable",
                            "read")
                    ],
                    Properties: new()
                    {
                        [VisualPropertyKeys.Text] = Json("Pump available")
                    })
            ]));

        sourceViews.UpsertPopup(new PopupEngineeringDto(
            null,
            "popup.motor.faceplate",
            "Motor faceplate",
            "motor.faceplate",
            Elements:
            [
                new VisualElementEngineeringDto(
                    "enabled",
                    BuiltinVisualObjectSchemas.CheckBoxType,
                    Bindings:
                    [
                        new EngineeringBindingDto(
                            VisualPropertyKeys.Checked,
                            EngineeringBindingKind.Tag,
                            "Plant.Pump01.Enabled",
                            "readWrite",
                            TagReference: new TagValueReference(booleanTag.Id))
                    ],
                    Properties: new()
                    {
                        [VisualPropertyKeys.Text] = Json("Enabled"),
                        [VisualPropertyKeys.InteractionEnabled] = Json(true)
                    }),
                CreateComboBox(modeTag.Id)
            ]));

        var source = new EngineeringExchangeService(
            sourceTags,
            sourceAlarms,
            new InMemoryDataSourceEngineeringRegistry(),
            sourceAssets,
            sourceViews);
        var payload = source.ExportJson();

        var targetTags = new InMemoryTagRegistry();
        var targetAlarmsBus = new InMemoryScadaEventBus();
        using var targetAlarms = new InMemoryAlarmEngine(targetAlarmsBus);
        var targetAssets = new InMemoryEngineeringAssetRegistry();
        var targetViews = new InMemoryEngineeringViewRegistry();
        var target = new EngineeringExchangeService(
            targetTags,
            targetAlarms,
            new InMemoryDataSourceEngineeringRegistry(),
            targetAssets,
            targetViews);

        var package = target.ParseJson(payload);
        var preview = target.Preview(package, ImportMode.CreateOnly);
        Assert.True(preview.CanApply, string.Join(Environment.NewLine,
            preview.Items.SelectMany(item => item.Issues).Select(issue => issue.Message)));

        var applied = target.Apply(package, ImportMode.CreateOnly);
        Assert.DoesNotContain(applied.Issues, issue => issue.IsError);

        var restored = target.ParseJson(target.ExportJson());
        var restoredScreen = Assert.Single(restored.Screens!);
        var restoredScreenCheckBox = Assert.Single(restoredScreen.Elements!);
        var restoredPopup = Assert.Single(restored.Popups!);
        var restoredTemplate = Assert.Single(restored.Templates!);
        var restoredControls = restoredPopup.Elements!.ToDictionary(element => element.Key);

        Assert.Equal("Pump01.IsAvailable", Assert.Single(restoredScreenCheckBox.Bindings!).Target);
        Assert.Equal(EngineeringBindingKind.Expression, restoredScreenCheckBox.Bindings!.Single().Kind);
        Assert.Equal("Plant.Pump01.Enabled", restoredControls["enabled"].Bindings!.Single().Target);
        Assert.Equal(booleanTag.Id, restoredControls["enabled"].Bindings!.Single().TagReference!.TagId);
        Assert.True(restoredControls["enabled"].Properties![VisualPropertyKeys.InteractionEnabled].GetBoolean());
        AssertComboBox(restoredControls["pumpMode"]);
        AssertComboBox(Assert.Single(restoredTemplate.Elements!));
    }

    private static VisualElementEngineeringDto CreateComboBox(Guid modeTagId) => new(
        "pumpMode",
        BuiltinVisualObjectSchemas.ComboBoxType,
        Bindings:
        [
            new EngineeringBindingDto(
                VisualPropertyKeys.Text,
                EngineeringBindingKind.Tag,
                "Plant.Pump01.Mode",
                "readWrite",
                TagReference: new TagValueReference(modeTagId))
        ],
        Properties: new()
        {
            [VisualPropertyKeys.InteractionEnabled] = Json(true),
            [BuiltinVisualObjectSchemas.ComboBoxOptionsProperty] = Json(new[]
            {
                new { value = "AUTO", label = "Automatic" },
                new { value = "MANUAL", label = "Manual" }
            })
        });

    private static void AssertComboBox(VisualElementEngineeringDto element)
    {
        Assert.Equal(BuiltinVisualObjectSchemas.ComboBoxType, element.Type);
        Assert.Equal(EngineeringBindingKind.Tag, Assert.Single(element.Bindings!).Kind);
        Assert.Equal("Plant.Pump01.Mode", element.Bindings!.Single().Target);
        Assert.Equal(JsonValueKind.Array, element.Properties![BuiltinVisualObjectSchemas.ComboBoxOptionsProperty].ValueKind);
        Assert.Equal("AUTO", element.Properties[BuiltinVisualObjectSchemas.ComboBoxOptionsProperty][0].GetProperty("value").GetString());
        Assert.Equal("Automatic", element.Properties[BuiltinVisualObjectSchemas.ComboBoxOptionsProperty][0].GetProperty("label").GetString());
        Assert.Equal("MANUAL", element.Properties[BuiltinVisualObjectSchemas.ComboBoxOptionsProperty][1].GetProperty("value").GetString());
    }

    private static JsonElement Json<T>(T value) => JsonSerializer.SerializeToElement(value);
}
