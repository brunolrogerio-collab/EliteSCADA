using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.ImportExport;
using Scada.Engineering.ProjectPackages;
using Scada.Engineering.Views;
using Scada.Engineering.VisualScripting;

namespace Scada.Core.Tests;

public sealed class VisualDynamicEngineeringTests
{
    [Fact]
    public void BuiltinSchemas_ExposeVisibleAndDeclareAnalogFillExplicitly()
    {
        foreach (var schema in BuiltinVisualObjectSchemas.All)
        {
            Assert.True(schema.Declares(VisualPropertyKeys.Visible));
            var visible = schema.GetRequired(VisualPropertyKeys.Visible);
            Assert.Equal(VisualPropertyValueKind.Boolean, visible.ValueKind);
            Assert.True(visible.EngineeringEditable);
            Assert.True(visible.RuntimeReadable);
            Assert.True(visible.RuntimeWritable);
            Assert.True(visible.SupportsBinding);
            Assert.True(Assert.IsType<VisualBooleanValue>(visible.DefaultValue).Value);
        }

        Assert.True(BuiltinVisualObjectSchemas.Rectangle.SupportsAnalogFill);
        Assert.True(BuiltinVisualObjectSchemas.Ellipse.SupportsAnalogFill);
        Assert.False(BuiltinVisualObjectSchemas.Group.SupportsAnalogFill);
        Assert.False(BuiltinVisualObjectSchemas.Line.SupportsAnalogFill);
        Assert.False(BuiltinVisualObjectSchemas.Polygon.SupportsAnalogFill);
        Assert.False(BuiltinVisualObjectSchemas.Text.SupportsAnalogFill);
        Assert.False(BuiltinVisualObjectSchemas.Image.SupportsAnalogFill);
        Assert.False(BuiltinVisualObjectSchemas.ValueDisplay.SupportsAnalogFill);
        Assert.False(BuiltinVisualObjectSchemas.Button.SupportsAnalogFill);
    }

    [Fact]
    public void JsonPreviewApplyExportAndProjectPackage_RoundTripDynamicVisualEngineering()
    {
        var tags = new InMemoryTagRegistry();
        var word = TagDefinition.Create("Status Word", "Plant.Word_status", TagDataType.Int16);
        var fault = TagDefinition.Create("Pump Fault", "Plant.falha_bomba1", TagDataType.Boolean);
        var level = TagDefinition.Create("Level", "Plant.nivel1", TagDataType.Double);
        tags.Register(word);
        tags.Register(fault);
        tags.Register(level);

        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var views = new InMemoryEngineeringViewRegistry();
        var service = new EngineeringExchangeService(
            tags,
            alarms,
            new InMemoryDataSourceEngineeringRegistry(),
            new InMemoryEngineeringAssetRegistry(),
            views);

        var screen = new ScreenEngineeringDto(
            null,
            "plant.overview",
            "Plant Overview",
            "/plant",
            Elements:
            [
                new VisualElementEngineeringDto(
                    "tank",
                    BuiltinVisualObjectSchemas.RectangleType,
                    PropertyExpressions:
                    [
                        new VisualPropertyExpressionEngineeringDto(
                            VisualPropertyKeys.Opacity,
                            new VisualExpressionEngineeringDto(
                                "clamp(level / 100, 0, 1)",
                                VisualExpressionValueType.Number,
                                [Dependency("level", level, VisualExpressionValueType.Number)]))
                    ],
                    BooleanConditions:
                    [
                        new VisualBooleanConditionEngineeringDto(
                            VisualPropertyKeys.Visible,
                            VisualBooleanConditionKind.NumericInterval,
                            Direct(level, VisualExpressionValueType.Number),
                            Minimum: 20,
                            MinimumInclusive: true,
                            Maximum: 80,
                            MaximumInclusive: true,
                            IntervalMode: VisualNumericIntervalMode.Inside)
                    ],
                    AnalogFill: new VisualAnalogFillEngineeringDto(
                        ExpressionSource(
                            "level * 1",
                            VisualExpressionValueType.Number,
                            Dependency("level", level, VisualExpressionValueType.Number)),
                        0,
                        100,
                        "#00AAFF",
                        Clamp: true,
                        InvertScale: false,
                        Direction: VisualAnalogFillDirection.BottomToTop)),
                new VisualElementEngineeringDto(
                    "alarm",
                    BuiltinVisualObjectSchemas.EllipseType,
                    PropertyExpressions:
                    [
                        new VisualPropertyExpressionEngineeringDto(
                            VisualPropertyKeys.Visible,
                            new VisualExpressionEngineeringDto(
                                "Word_status_03 or falha_bomba1",
                                VisualExpressionValueType.Boolean,
                                [
                                    new VisualExpressionDependencyEngineeringDto(
                                        "Word_status_03",
                                        VisualExpressionDependencyKind.Tag,
                                        VisualExpressionValueType.Boolean,
                                        new TagValueReference(
                                            word.Id,
                                            new TagValueSelector(TagValueSelectorKind.Bit, 3)),
                                        "Plant.Word_status.03"),
                                    Dependency("falha_bomba1", fault, VisualExpressionValueType.Boolean)
                                ]))
                    ])
            ]);

        var package = EmptyPackage() with { Screens = [screen] };
        var json = System.Text.Json.JsonSerializer.Serialize(package, JsonOptions());
        var parsed = service.ParseJson(json);
        var preview = service.Preview(parsed, ImportMode.CreateAndUpdate);
        var result = service.Apply(parsed, ImportMode.CreateAndUpdate);

        Assert.True(preview.CanApply);
        Assert.Empty(result.Issues);
        Assert.Equal(1, result.Created);

        var exported = Assert.Single(service.ExportPackage().Screens!);
        var tank = exported.Elements!.Single(element => element.Key == "tank");
        var alarmElement = exported.Elements!.Single(element => element.Key == "alarm");
        Assert.Equal("clamp(level / 100, 0, 1)", Assert.Single(tank.PropertyExpressions!).Expression.Text);
        Assert.Equal(20, Assert.Single(tank.BooleanConditions!).Minimum);
        Assert.Equal("#00AAFF", tank.AnalogFill!.FillColor);
        Assert.Equal(VisualAnalogFillDirection.BottomToTop, tank.AnalogFill.Direction);
        var bitDependency = Assert.Single(Assert.Single(alarmElement.PropertyExpressions!).Expression.Dependencies!, dependency =>
            dependency.TagReference.Selector is not null);
        Assert.Equal(word.Id, bitDependency.TagReference.TagId);
        Assert.Equal(3, bitDependency.TagReference.Selector!.Index);

        var packageService = new ProjectPackageService(service);
        var inspection = packageService.Inspect(packageService.Export("visual-dynamic", "Visual Dynamic"));
        var restoredTank = Assert.Single(inspection.Engineering.Screens!).Elements!.Single(element => element.Key == "tank");
        Assert.Equal(VisualDynamicEngineeringVersions.Current, restoredTank.AnalogFill!.Version);
        Assert.Equal("level * 1", restoredTank.AnalogFill.Source.Expression!.Text);
    }

    [Fact]
    public void Preview_RejectsConflictsTypeErrorsBadReferencesAndUnsupportedFill()
    {
        var tags = new InMemoryTagRegistry();
        var word = TagDefinition.Create("Word", "Plant.Word", TagDataType.Int16);
        tags.Register(word);
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var service = new EngineeringExchangeService(
            tags,
            alarms,
            new InMemoryDataSourceEngineeringRegistry(),
            new InMemoryEngineeringAssetRegistry(),
            new InMemoryEngineeringViewRegistry());

        var missing = Guid.NewGuid();
        var screen = new ScreenEngineeringDto(
            null,
            "invalid.dynamic",
            "Invalid Dynamic",
            Elements:
            [
                new VisualElementEngineeringDto(
                    "conflict",
                    BuiltinVisualObjectSchemas.RectangleType,
                    Bindings: [new EngineeringBindingDto(VisualPropertyKeys.Visible, EngineeringBindingKind.Tag, "Plant.Legacy")],
                    PropertyExpressions:
                    [
                        new VisualPropertyExpressionEngineeringDto(
                            VisualPropertyKeys.Visible,
                            new VisualExpressionEngineeringDto(
                                "1",
                                VisualExpressionValueType.Number,
                                [new VisualExpressionDependencyEngineeringDto(
                                    "missing",
                                    VisualExpressionDependencyKind.Tag,
                                    VisualExpressionValueType.Number,
                                    new TagValueReference(missing))]))
                    ],
                    BooleanConditions:
                    [
                        new VisualBooleanConditionEngineeringDto(
                            VisualPropertyKeys.Visible,
                            VisualBooleanConditionKind.NumericInterval,
                            Direct(word, VisualExpressionValueType.Number))
                    ]),
                new VisualElementEngineeringDto(
                    "bad-bit",
                    BuiltinVisualObjectSchemas.EllipseType,
                    PropertyExpressions:
                    [
                        new VisualPropertyExpressionEngineeringDto(
                            VisualPropertyKeys.Visible,
                            new VisualExpressionEngineeringDto(
                                "word17",
                                VisualExpressionValueType.Boolean,
                                [new VisualExpressionDependencyEngineeringDto(
                                    "word17",
                                    VisualExpressionDependencyKind.Tag,
                                    VisualExpressionValueType.Boolean,
                                    new TagValueReference(word.Id, new TagValueSelector(TagValueSelectorKind.Bit, 17))) ]))
                    ]),
                new VisualElementEngineeringDto(
                    "image-fill",
                    BuiltinVisualObjectSchemas.ImageType,
                    AnalogFill: new VisualAnalogFillEngineeringDto(
                        Direct(word, VisualExpressionValueType.Number),
                        0,
                        100,
                        "red"))
            ]);

        var preview = service.Preview(EmptyPackage() with { Screens = [screen] }, ImportMode.CreateAndUpdate);
        var codes = preview.Items.SelectMany(item => item.Issues).Select(issue => issue.Code).ToArray();

        Assert.False(preview.CanApply);
        Assert.Contains("VISUAL_DYNAMIC_PROPERTY_SOURCE_CONFLICT", codes);
        Assert.Contains("VISUAL_DYNAMIC_PROPERTY_TYPE_MISMATCH", codes);
        Assert.Contains("VISUAL_DYNAMIC_REFERENCE_NOT_FOUND", codes);
        Assert.Contains("VISUAL_BOOLEAN_CONDITION_BOUND_REQUIRED", codes);
        Assert.Contains("VISUAL_DYNAMIC_REFERENCE_SELECTOR_INVALID", codes);
        Assert.Contains("VISUAL_ANALOG_FILL_NOT_SUPPORTED", codes);
        Assert.Contains("VISUAL_ANALOG_FILL_COLOR_INVALID", codes);
    }

    [Fact]
    public void SchemaV13WithoutDynamicFields_ReopensWithoutManufacturingConfiguration()
    {
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var views = new InMemoryEngineeringViewRegistry();
        var service = new EngineeringExchangeService(
            new InMemoryTagRegistry(),
            alarms,
            new InMemoryDataSourceEngineeringRegistry(),
            new InMemoryEngineeringAssetRegistry(),
            views);
        const string json = """
        {
          "schema": "scada.engineering",
          "schemaVersion": 13,
          "exportedAt": "2026-08-29T00:00:00Z",
          "tags": [],
          "alarms": [],
          "screens": [
            {
              "key": "legacy.screen",
              "name": "Legacy Screen",
              "elements": [
                { "key": "box", "type": "core.rectangle" }
              ]
            }
          ]
        }
        """;

        var package = service.ParseJson(json);
        var element = Assert.Single(Assert.Single(package.Screens!).Elements!);
        var preview = service.Preview(package, ImportMode.CreateAndUpdate);
        var result = service.Apply(package, ImportMode.CreateAndUpdate);

        Assert.Null(element.PropertyExpressions);
        Assert.Null(element.BooleanConditions);
        Assert.Null(element.AnalogFill);
        Assert.True(preview.CanApply);
        Assert.Empty(result.Issues);

        var reopened = Assert.Single(Assert.Single(service.ExportPackage().Screens!).Elements!);
        Assert.Null(reopened.PropertyExpressions);
        Assert.Null(reopened.BooleanConditions);
        Assert.Null(reopened.AnalogFill);
    }

    private static VisualExpressionDependencyEngineeringDto Dependency(
        string symbol,
        TagDefinition tag,
        VisualExpressionValueType valueType) =>
        new(symbol, VisualExpressionDependencyKind.Tag, valueType, new TagValueReference(tag.Id), tag.Path);

    private static VisualValueSourceEngineeringDto Direct(
        TagDefinition tag,
        VisualExpressionValueType valueType) =>
        new(VisualValueSourceKind.Tag, valueType, tag.Path, new TagValueReference(tag.Id));

    private static VisualValueSourceEngineeringDto ExpressionSource(
        string text,
        VisualExpressionValueType valueType,
        params VisualExpressionDependencyEngineeringDto[] dependencies) =>
        new(
            VisualValueSourceKind.Expression,
            valueType,
            Expression: new VisualExpressionEngineeringDto(text, valueType, dependencies));

    private static EngineeringPackage EmptyPackage() =>
        new(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>());

    private static System.Text.Json.JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase) }
    };
}
