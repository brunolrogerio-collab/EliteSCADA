using Scada.Api.Runtime;
using Scada.Engineering.Contracts;
using Scada.Engineering.Validation;
using Scada.Engineering.Views;

namespace Scada.Drivers.Tests;

public sealed class BuiltinDynamoLibraryTests
{
    [Fact]
    public void ReplacementCatalogV1_MeetsInitialFamilyCountsAndUsesStableCanonicalGeometry()
    {
        var definitions = BuiltinDynamoCatalogV1.Create();

        Assert.Equal(26, definitions.Count);
        Assert.Equal(26, definitions.Select(definition => definition.Key).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(26, definitions.Select(definition => definition.Id).Distinct().Count());
        Assert.Equal(4, definitions.Count(definition => definition.Metadata!["familyKey"] == "indicator.lamp"));
        Assert.Equal(4, definitions.Count(definition => definition.Metadata!["familyKey"] == "operator.button"));
        Assert.Equal(6, definitions.Count(definition => definition.Metadata!["familyKey"] == "equipment.motor"));
        Assert.Equal(6, definitions.Count(definition => definition.Metadata!["familyKey"] == "equipment.valve"));
        Assert.Equal(6, definitions.Count(definition => definition.Metadata!["familyKey"] == "equipment.electrical"));

        Assert.All(definitions, definition =>
        {
            Assert.Equal("active", definition.Metadata!["catalogStatus"]);
            Assert.Equal(BuiltinDynamoCatalogV1.Version, definition.Metadata!["libraryVersion"]);
            Assert.NotEmpty(definition.Elements!);
            Assert.All(definition.Elements!, element => Assert.NotNull(element.Id));
            var issues = VisualCompositionEngineeringValidation.ValidateDynamo(definition);
            Assert.DoesNotContain(issues, issue => issue.IsError);
        });
    }

    [Fact]
    public void ReplacementCatalogV1_PublishesSafeOriginalSvgAssetsAndReferencesThemCanonically()
    {
        var definitions = BuiltinDynamoCatalogV1.Create();
        var assets = BuiltinDynamoCatalogV1.CreateArtworkAssets();

        Assert.Equal(definitions.Count, assets.Count);
        foreach (var definition in definitions)
        {
            var assetKey = $"builtin.dynamo.v1.{definition.Key.Replace('.', '-')}";
            var (asset, payload) = Assert.Single(assets, candidate => candidate.Asset.Key == assetKey);
            if (definition.Metadata!["familyKey"] == "equipment.electrical")
            {
                Assert.DoesNotContain(definition.Elements!, element => element.Type == "core.svgSymbol");
                Assert.Contains(definition.Elements!, element => element.Key.EndsWith("-moving", StringComparison.Ordinal));
            }
            else
            {
                var artwork = Assert.Single(definition.Elements!, element => element.Type == "core.svgSymbol");
                Assert.Equal(asset.Id!.Value.ToString("D"), artwork.Properties!["assetRef"].GetProperty("assetId").GetString());
            }
            Assert.Equal("image/svg+xml", payload.MediaType);
            Assert.Equal(payload.Sha256, asset.Sha256);
            Assert.NotEmpty(payload.Content);
            Assert.DoesNotContain("<script", System.Text.Encoding.UTF8.GetString(payload.Content), StringComparison.OrdinalIgnoreCase);
            var registry = new Scada.Engineering.VisualAssets.InMemoryVisualAssetEngineeringRegistry();
            registry.UpsertAsset(asset);
            registry.PutPayload(payload);
            Assert.DoesNotContain(Scada.Engineering.VisualAssets.VisualAssetEngineeringValidator.Validate(asset, registry), issue => issue.IsError);
        }
    }

    [Fact]
    public void ReplacementElectricalContacts_AnimateBladeColorAndRotationFromMappedTag()
    {
        var contact = BuiltinDynamoCatalogV1.Create().Single(definition => definition.Key == "electrical.contact-tri-horizontal");
        Assert.Equal("0=open;1=closed", contact.Metadata!["stateProfile"]);
        Assert.Contains(contact.Parameters!, parameter => parameter.Key == "state" && parameter.Kind == DynamoParameterKind.TagReference);
        Assert.Contains(contact.Parameters!, parameter => parameter.Key == "openColor");
        Assert.Contains(contact.Parameters!, parameter => parameter.Key == "closedColor");

        var tagId = Guid.NewGuid();
        var instance = new VisualElementEngineeringDto("contacts", "dynamo", DynamoKey: contact.Key, Id: Guid.NewGuid(),
            DynamoParameters: [new("state", DynamoParameterKind.TagReference, TagReference: new(tagId))]);
        var projected = DynamoRuntimeComposer.Compose(instance, contact);
        var blades = projected.Elements.Where(element => element.Key.EndsWith("-moving", StringComparison.Ordinal)).ToArray();

        Assert.Equal(3, blades.Length);
        Assert.All(blades, blade =>
        {
            Assert.Equal(tagId, Assert.Single(blade.PropertyMaps!, map => map.PropertyKey == "fillColor").Source.TagReference!.TagId);
            var rotation = Assert.Single(blade.PropertyMaps!, map => map.PropertyKey == "rotation");
            Assert.Equal(2, rotation.Rules.Count);
            Assert.Equal(VisualExpressionValueType.Number, rotation.Source.ValueType);
            Assert.Equal(tagId, rotation.Source.TagReference!.TagId);
            Assert.Null(rotation.Source.Target);
        });

        var fixedInstance = instance with
        {
            DynamoParameters =
            [
                new("state", DynamoParameterKind.TagReference, TagReference: new(tagId)),
                new("animationEnabled", DynamoParameterKind.Boolean, System.Text.Json.JsonSerializer.SerializeToElement(false)),
                new("fixedState", DynamoParameterKind.Number, System.Text.Json.JsonSerializer.SerializeToElement(1))
            ]
        };
        var fixedProjection = DynamoRuntimeComposer.Compose(fixedInstance, contact);
        Assert.All(fixedProjection.Elements.Where(element => element.Key.EndsWith("-moving", StringComparison.Ordinal)),
            blade => Assert.Equal(0, blade.Properties!["rotation"].GetDouble()));
        Assert.All(fixedProjection.Elements.Where(element => element.Key.EndsWith("-moving", StringComparison.Ordinal)),
            blade => Assert.Null(blade.PropertyMaps));
    }

    [Fact]
    public void ReplacementSignalLamp_ProjectsTypedNumericExpressionStateSource()
    {
        var lamp = BuiltinDynamoCatalogV1.Create().Single(definition => definition.Key == "indicator.lamp.round");
        var sourceTagId = Guid.NewGuid();
        var stateSource = new VisualValueSourceEngineeringDto(
            VisualValueSourceKind.Expression,
            VisualExpressionValueType.Number,
            Expression: new VisualExpressionEngineeringDto(
                "statusWord",
                VisualExpressionValueType.Number,
                [new("statusWord", VisualExpressionDependencyKind.Tag, VisualExpressionValueType.Number,
                    new Scada.Core.Tags.TagValueReference(sourceTagId))]));
        var instance = new VisualElementEngineeringDto("lamp-1", "dynamo", DynamoKey: lamp.Key, Id: Guid.NewGuid(),
            DynamoParameters: [new("state", DynamoParameterKind.ValueSource, ValueSource: stateSource)]);

        var projected = DynamoRuntimeComposer.Compose(instance, lamp);
        var stateMap = Assert.Single(projected.Elements.SelectMany(element => element.PropertyMaps ?? []));

        Assert.Equal(VisualValueSourceKind.Expression, stateMap.Source.Kind);
        Assert.Equal("statusWord", stateMap.Source.Expression!.Text);
        Assert.Equal(sourceTagId, Assert.Single(stateMap.Source.Expression.Dependencies!).TagReference.TagId);
        Assert.Empty(VisualCompositionEngineeringValidation.ValidateDynamo(lamp));

        var wrongTypeInstance = instance with
        {
            DynamoParameters =
            [new("state", DynamoParameterKind.ValueSource, ValueSource: new VisualValueSourceEngineeringDto(
                VisualValueSourceKind.Tag, VisualExpressionValueType.Boolean,
                TagReference: new Scada.Core.Tags.TagValueReference(Guid.NewGuid())))]
        };
        Assert.Throws<InvalidOperationException>(() => DynamoRuntimeComposer.Compose(wrongTypeInstance, lamp));
    }

    [Fact]
    public void ReplacementSignalLamp_ProjectsLegacyDirectTagStateAsTypedSourceWithoutChangingStoredPayload()
    {
        var lamp = BuiltinDynamoCatalogV1.Create().Single(definition => definition.Key == "indicator.lamp.round");
        var tagId = Guid.NewGuid();
        var legacyParameter = new DynamoParameterValueEngineeringDto(
            "state", DynamoParameterKind.TagReference,
            TagReference: new Scada.Core.Tags.TagValueReference(tagId));
        var instance = new VisualElementEngineeringDto("lamp-legacy", "dynamo", DynamoKey: lamp.Key,
            Id: Guid.NewGuid(), DynamoParameters: [legacyParameter]);

        var projected = DynamoRuntimeComposer.Compose(instance, lamp);
        var stateMap = Assert.Single(projected.Elements.SelectMany(element => element.PropertyMaps ?? []));

        Assert.Equal(DynamoParameterKind.TagReference, instance.DynamoParameters!.Single().Kind);
        Assert.Equal(VisualValueSourceKind.Tag, stateMap.Source.Kind);
        Assert.Equal(tagId, stateMap.Source.TagReference!.TagId);
        Assert.Equal(VisualExpressionValueType.Number, stateMap.Source.ValueType);
        Assert.Equal(DynamoParameterKind.ValueSource, projected.Parameters["state"].Kind);
    }

    [Fact]
    public void ReplacementCatalogV1_EquipmentStatesAreNumericTagMapsAndKeepLegacyLibrarySeparate()
    {
        var replacement = BuiltinDynamoCatalogV1.Create();
        var legacy = BuiltinDynamoLibrary.Create();

        Assert.Equal(72, legacy.Count);
        Assert.All(legacy, definition => Assert.Equal("legacy", definition.Metadata!["catalogStatus"]));
        foreach (var definition in replacement.Where(definition => definition.Metadata!["familyKey"] is "equipment.motor" or "equipment.valve"))
        {
            var stateMap = Assert.Single(definition.Elements!.SelectMany(element => element.PropertyMaps ?? []),
                map => map.PropertyKey == "svg.slot.state.fill");
            Assert.Equal(5, stateMap.Rules.Count);
            Assert.Equal(VisualValueSourceKind.Tag, stateMap.Source.Kind);
            Assert.Equal(VisualExpressionValueType.Number, stateMap.Source.ValueType);
            Assert.Equal("{equipmentPath}.State", stateMap.Source.Target);
            Assert.Equal("not-displayed", definition.Metadata!["analogProcessValues"]);
            Assert.Equal(5, definition.Elements!.Count(element => element.Metadata?.ContainsKey("dynamoStateLabelIndex") == true));
            Assert.Contains(definition.Elements!, element => element.Actions?.Any(action => action.Kind == VisualNavigationActionKind.ExecuteCommand && action.CommandParameterKey == "command") == true);
        }
    }

    [Fact]
    public void ReplacementEquipment_ProjectsStateTagToLabelsAndSupportsFixedStateText()
    {
        var motor = BuiltinDynamoCatalogV1.Create().Single(definition => definition.Key == "motor.tefc");
        var tagId = Guid.NewGuid();
        var instance = new VisualElementEngineeringDto(
            "motor-1", "dynamo", DynamoKey: motor.Key, Id: Guid.NewGuid(),
            DynamoParameters:
            [
                new("state", DynamoParameterKind.TagReference, TagReference: new(tagId)),
                new("animationEnabled", DynamoParameterKind.Boolean, System.Text.Json.JsonSerializer.SerializeToElement(false)),
                new("fixedState", DynamoParameterKind.Number, System.Text.Json.JsonSerializer.SerializeToElement(3)),
                new("labelPosition", DynamoParameterKind.String, System.Text.Json.JsonSerializer.SerializeToElement("right")),
                new("communicationBadText", DynamoParameterKind.String, System.Text.Json.JsonSerializer.SerializeToElement("TAG SEM COMUNICAÇÃO")),
                new("command", DynamoParameterKind.Command, CommandId: Guid.NewGuid())
            ]);

        var projected = DynamoRuntimeComposer.Compose(instance, motor);
        var labels = projected.Elements.Where(element => element.Metadata?.ContainsKey("dynamoStateLabelIndex") == true).ToArray();

        Assert.Equal(5, labels.Length);
        Assert.Equal("TAG SEM COMUNICAÇÃO", labels[3].Properties!["text"].GetString());
        Assert.True(labels[3].Properties!["visible"].GetBoolean());
        Assert.Equal(96, labels[3].Properties!["x"].GetDouble());
        Assert.Equal(36, labels[3].Properties!["width"].GetDouble());
        Assert.All(labels.Where((_, index) => index != 3), label => Assert.False(label.Properties!["visible"].GetBoolean()));
        Assert.All(labels, label => Assert.Null(label.BooleanConditions));

        var animatedInstance = instance with
        {
            DynamoParameters = [
                new("state", DynamoParameterKind.TagReference, TagReference: new(tagId)),
                new("command", DynamoParameterKind.Command, CommandId: Guid.NewGuid())
            ]
        };
        var animated = DynamoRuntimeComposer.Compose(animatedInstance, motor);
        Assert.All(animated.Elements.Where(element => element.Metadata?.ContainsKey("dynamoStateLabelIndex") == true), label =>
        {
            var condition = Assert.Single(label.BooleanConditions!);
            Assert.Equal("visible", condition.PropertyKey);
            Assert.Equal(tagId, condition.Source.TagReference!.TagId);
            Assert.Null(condition.Source.Target);
        });
        Assert.All(animated.Elements.SelectMany(element => element.PropertyMaps ?? []), map =>
        {
            Assert.Equal(tagId, map.Source.TagReference!.TagId);
            Assert.Null(map.Source.Target);
        });
    }

    [Fact]
    public void ReplacementButtonActions_ProjectTagAndScalarParametersThroughCanonicalRuntimeComposition()
    {
        var definitions = BuiltinDynamoCatalogV1.Create();
        var analogButton = definitions.Single(definition => definition.Key == "operator.button.flush");
        var targetTagId = Guid.NewGuid();
        var instance = new Scada.Engineering.Contracts.VisualElementEngineeringDto(
            "setpoint-button", "dynamo", DynamoKey: analogButton.Key, Id: Guid.NewGuid(),
            DynamoParameters:
            [
                new("targetTag", DynamoParameterKind.TagReference, TagReference: new(targetTagId)),
                new("analogValue", DynamoParameterKind.Number, System.Text.Json.JsonSerializer.SerializeToElement(24.5))
            ]);

        var projected = DynamoRuntimeComposer.Compose(instance, analogButton);
        var action = Assert.Single(projected.Elements.SelectMany(element => element.Actions ?? []));

        Assert.Equal(VisualNavigationActionKind.SetTagValue, action.Kind);
        Assert.Equal(targetTagId.ToString("D"), action.TargetKey);
        Assert.Equal(24.5, action.Parameters!["value"].GetDouble());
        Assert.Contains(projected.Elements, element => element.Type == "core.svgSymbol" &&
            element.Metadata?.GetValueOrDefault("dynamoInteraction") == "momentary-button");

        var toggleButton = definitions.Single(definition => definition.Key == "operator.button.guarded");
        var toggleInstance = instance with
        {
            DynamoKey = toggleButton.Key,
            DynamoParameters = [new("targetTag", DynamoParameterKind.TagReference, TagReference: new(targetTagId))]
        };
        var toggle = Assert.Single(DynamoRuntimeComposer.Compose(toggleInstance, toggleButton).Elements.SelectMany(element => element.Actions ?? []));
        Assert.Equal(VisualNavigationActionKind.ToggleTagBoolean, toggle.Kind);
        Assert.Equal(targetTagId.ToString("D"), toggle.TargetKey);
    }

    [Fact]
    public void ReplacementEquipmentCanPinAConfiguredStateWithoutRuntimeAnimation()
    {
        var motor = BuiltinDynamoCatalogV1.Create().Single(definition => definition.Key == "motor.tefc");
        var instance = new Scada.Engineering.Contracts.VisualElementEngineeringDto(
            "motor-1", "dynamo", DynamoKey: motor.Key, Id: Guid.NewGuid(),
            DynamoParameters:
            [
                new("animationEnabled", DynamoParameterKind.Boolean, System.Text.Json.JsonSerializer.SerializeToElement(false)),
                new("fixedState", DynamoParameterKind.Number, System.Text.Json.JsonSerializer.SerializeToElement(2)),
                new("command", DynamoParameterKind.Command, CommandId: Guid.NewGuid())
            ]);

        var projected = DynamoRuntimeComposer.Compose(instance, motor);
        var body = projected.Elements.Single(element => element.Key == "artwork");

        Assert.Null(body.PropertyMaps);
        Assert.Equal("#DC2626", body.Properties!["svgPaintOverrides"].GetProperty("slots").GetProperty("state").GetProperty("fill").GetString());
    }

    [Fact]
    public void Library_ProvidesRepresentativeInsertableDefinitionsAcrossIndustrialFamilies()
    {
        var definitions = BuiltinDynamoLibrary.Create();

        Assert.Equal(72, definitions.Count);
        Assert.Equal(definitions.Count, definitions.Select(definition => definition.Id).Distinct().Count());
        Assert.Equal(definitions.Count, definitions.Select(definition => definition.Key).Distinct(StringComparer.Ordinal).Count());

        var categories = definitions
            .GroupBy(definition => definition.Properties!["category"], StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        Assert.Equal(6, categories["pump"]);
        Assert.Equal(6, categories["motor"]);
        Assert.Equal(15, categories["valve"]);
        Assert.Equal(6, categories["tank"]);
        Assert.Equal(9, categories["compressor"]);
        Assert.Equal(9, categories["process"]);
        Assert.Equal(15, categories["substation"]);
        Assert.Equal(3, categories["electrical"]);
        Assert.Equal(3, categories["instrument"]);
        Assert.All(definitions, definition =>
        {
            Assert.NotEmpty(definition.Elements!);
            Assert.Equal("true", definition.Metadata!["builtinLibrary"]);
            Assert.Equal("original-elitescada-vector", definition.Metadata!["assetOrigin"]);
            Assert.Equal(BuiltinDynamoLibrary.Version, definition.Properties!["libraryVersion"]);
            Assert.True(double.Parse(definition.Properties!["defaultWidth"], System.Globalization.CultureInfo.InvariantCulture) > 0);
            Assert.True(double.Parse(definition.Properties!["defaultHeight"], System.Globalization.CultureInfo.InvariantCulture) > 0);
            Assert.Contains(definition.Properties!["visualStyle"], new[] { "detailed-2d", "dimensional-front", "high-performance" });
            Assert.Equal(definition.Properties!["visualStyle"], definition.Metadata!["visualStyle"]);
            Assert.Equal("front-orthographic", definition.Metadata!["view"]);
            Assert.Equal("pid-inspired-native-vector-v1", definition.Properties["visualReferenceProfile"]);
            Assert.Equal("pid-inspired-native-vector-v1", definition.Metadata!["visualReferenceProfile"]);
            Assert.Equal("original-editable-geometry; third-party SVGs are not embedded", definition.Metadata!["visualReferencePolicy"]);
        });

        var styleCounts = definitions
            .GroupBy(definition => definition.Properties!["visualStyle"], StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        Assert.Equal(24, styleCounts["detailed-2d"]);
        Assert.Equal(24, styleCounts["dimensional-front"]);
        Assert.Equal(24, styleCounts["high-performance"]);

        var dimensional = definitions.Where(definition => definition.Properties!["visualStyle"] == "dimensional-front").ToArray();
        Assert.All(dimensional, definition =>
            Assert.Contains(definition.Elements!, element =>
                element.Properties is not null &&
                element.Properties.TryGetValue("fillStyle", out var fillStyle) &&
                string.Equals(fillStyle.GetString(), "gradient", StringComparison.Ordinal)));

        var highPerformance = definitions.Where(definition => definition.Properties!["visualStyle"] == "high-performance").ToArray();
        Assert.All(highPerformance, definition =>
            Assert.DoesNotContain(definition.Elements!, element =>
                element.Properties is not null &&
                element.Properties.TryGetValue("fillStyle", out var fillStyle) &&
                string.Equals(fillStyle.GetString(), "gradient", StringComparison.Ordinal)));
    }

    [Fact]
    public void Workspace_SeedsTheBuiltInLibraryAndKeepsEquipmentBindingsParameterized()
    {
        using var workspace = new EngineeringWorkspace();

        var definitions = workspace.Assets.SnapshotDynamos();
        Assert.Equal(BuiltinDynamoLibrary.Create().Count + BuiltinDynamoCatalogV1.Create().Count, definitions.Count);
        Assert.DoesNotContain(definitions, definition =>
            definition.Metadata?.GetValueOrDefault("assetOrigin") == "elipse-e3-import");
        var targets = definitions
            .SelectMany(definition => definition.Elements ?? [])
            .SelectMany(element => element.Bindings ?? [])
            .Select(binding => binding.Target)
            .ToArray();

        Assert.NotEmpty(targets);
        Assert.All(targets, target => Assert.StartsWith("{equipmentPath}.", target));
    }

    [Fact]
    public void CentrifugalBlowerStyles_KeepTheSameInterfaceAndRenderAnInspectableRotorAssembly()
    {
        var variants = BuiltinDynamoLibrary.Create()
            .Where(definition => definition.Metadata!["familyKey"] == "process.blower.centrifugal")
            .ToArray();

        Assert.Equal(3, variants.Length);
        foreach (var variant in variants)
        {
            var elementKeys = variant.Elements!.Select(element => element.Key).ToHashSet(StringComparer.Ordinal);
            Assert.Contains("inlet-flange", elementKeys);
            Assert.Contains("outlet-flange", elementKeys);
            Assert.Contains(elementKeys, key => key is "casing" or "volute-case");
            Assert.Contains("impeller-recess", elementKeys);
            Assert.Contains("hub-cap", elementKeys);
            Assert.Contains("outlet-flow-arrow", elementKeys);
            Assert.Equal(6, elementKeys.Count(key => key.StartsWith("impeller-blade-", StringComparison.Ordinal)));
            Assert.Contains(variant.Parameters!, parameter => parameter.Key == "equipmentPath");
            Assert.Contains(variant.Elements!, element => element.Bindings?.Any(binding => binding.Target == "{equipmentPath}.Running") == true);
            Assert.Contains(variant.Elements!, element => element.Bindings?.Any(binding => binding.Target == "{equipmentPath}.Fault") == true);
        }
    }

    [Fact]
    public void AllOtherBuiltinDynamoFamilies_ReceiveVersionedFamilySpecificVisualDetails()
    {
        var definitions = BuiltinDynamoLibrary.Create()
            .Where(definition => definition.Properties!["visualStyle"] != "high-performance")
            .Where(definition => definition.Metadata!["familyKey"] is
                "dynamo.pump.standard" or "process.pump.submersible" or "process.motor.standard" or
                "process.motor.vfd" or "process.valve.onoff" or "process.valve.control" or
                "process.tank.vertical" or "process.tank.horizontal" or "process.instrument.indicator")
            .ToArray();

        Assert.Equal(18, definitions.Length);
        Assert.All(definitions, definition =>
        {
            Assert.Equal(BuiltinDynamoLibrary.Version, definition.Properties!["libraryVersion"]);
            Assert.Contains(definition.Elements!, element => element.Key.StartsWith("detail-", StringComparison.Ordinal));
            Assert.Equal(definition.Elements!.Count, definition.Elements.Select(element => element.Key).Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(definition.Elements.Count, definition.Elements.Select(element => element.Id).Distinct().Count());
        });

        var keys = definitions.SelectMany(definition => definition.Elements!).Select(element => element.Key).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("detail-casing-bolt-1", keys);
        Assert.Contains("detail-upper-cooling-slot-1", keys);
        Assert.Contains("detail-cooling-rib-left-1", keys);
        Assert.Contains("detail-drive-status-1", keys);
        Assert.Contains("detail-flange-bolt-31-49", keys);
        Assert.Contains("detail-shell-weld-50", keys);
        Assert.Contains("detail-shell-seam-65", keys);
        Assert.Contains("detail-scale-tick--160", keys);
    }

    [Fact]
    public void StateAwareDynamos_ExposeTagAndEditableColorsWithHighPerformanceDefaults()
    {
        var definitions = BuiltinDynamoLibrary.Create();
        var highPerformanceMotor = definitions.Single(definition =>
            definition.Key == "process.motor.standard.high-performance");
        var stateColorElement = Assert.Single(highPerformanceMotor.Elements!, element =>
            element.Metadata?.ContainsKey("dynamoStateColorProfile") == true);
        var stateMap = Assert.Single(stateColorElement.PropertyMaps!);

        Assert.Contains(highPerformanceMotor.Parameters!, parameter => parameter.Key == "state" && parameter.Kind == Scada.Engineering.Contracts.DynamoParameterKind.TagReference);
        Assert.Equal("#8FBF98", stateMap.Rules.ElementAt(0).Value.GetString());
        Assert.Equal("#D98282", stateMap.Rules.ElementAt(1).Value.GetString());
        Assert.Equal("#D8B95F", stateMap.Rules.ElementAt(2).Value.GetString());
        Assert.Contains(highPerformanceMotor.Parameters!, parameter => parameter.Key == "stoppedColor" && parameter.DefaultValue?.GetString() == "#8FBF98");
        Assert.Contains(highPerformanceMotor.Parameters!, parameter => parameter.Key == "runningColor" && parameter.DefaultValue?.GetString() == "#D98282");
        Assert.Contains(highPerformanceMotor.Parameters!, parameter => parameter.Key == "faultColor" && parameter.DefaultValue?.GetString() == "#D8B95F");

        var families = definitions.Select(definition => definition.Metadata!["familyKey"]).Distinct().ToArray();
        Assert.Contains("electrical.transformer.power", families);
        Assert.Contains("electrical.breaker", families);
        Assert.Contains("electrical.disconnector", families);
        Assert.Contains("electrical.earthing-switch", families);
        Assert.Contains("process.exchanger.shell-tube", families);

        var mapped = definitions.Where(definition => definition.Metadata!["stateTagProfile"] != "none").ToArray();
        Assert.NotEmpty(mapped);
        Assert.All(mapped, definition =>
        {
            Assert.Contains(definition.Parameters!, parameter => parameter.Key == "state" && parameter.Kind == Scada.Engineering.Contracts.DynamoParameterKind.TagReference);
            Assert.Contains(definition.Parameters!, parameter => parameter.Key == "stoppedColor");
            Assert.Contains(definition.Parameters!, parameter => parameter.Key == "runningColor");
            Assert.Contains(definition.Parameters!, parameter => parameter.Key == "faultColor");
            Assert.Contains(definition.Elements!, element => element.PropertyMaps?.Any(map => map.PropertyKey == "fillColor") == true);
        });
    }

    [Fact]
    public void EveryBuiltinDynamo_UsesItsVersionedArtworkFinishWithoutFlatteningSemanticColors()
    {
        var definitions = BuiltinDynamoLibrary.Create();

        Assert.Equal(72, definitions.Count);
        Assert.All(definitions, definition =>
        {
            var style = definition.Properties!["visualStyle"];
            var expectedFinish = style switch
            {
                "detailed-2d" => "industrial-steel-2d-v4",
                "dimensional-front" => "soft-machined-steel-v4",
                "high-performance" => "high-performance-neutral-v4",
                _ => throw new Xunit.Sdk.XunitException($"Unexpected visual style '{style}'.")
            };

            Assert.Equal(BuiltinDynamoLibrary.Version, definition.Properties["libraryVersion"]);
            Assert.Equal(expectedFinish, definition.Properties["visualFinish"]);
            Assert.Equal(expectedFinish, definition.Metadata!["visualFinish"]);

            foreach (var element in definition.Elements!)
            {
                if (element.Properties is null) continue;
                if (element.Properties.TryGetValue("strokeWidth", out var strokeWidth) &&
                    strokeWidth.ValueKind == System.Text.Json.JsonValueKind.Number &&
                    strokeWidth.GetDouble() > 0)
                {
                    Assert.InRange(strokeWidth.GetDouble(), 0.75, 2.5);
                    Assert.Equal(0, strokeWidth.GetDouble() * 2 % 1, precision: 6);
                }
            }
        });

        var detailedBlower = definitions.Single(definition =>
            definition.Key == "process.blower.centrifugal");
        Assert.Contains(detailedBlower.Elements!, element =>
            element.Key == "outlet-flow-arrow" &&
            element.Properties!["fillColor"].GetString() == "#13799D");

        var highPerformanceMotor = definitions.Single(definition =>
            definition.Key == "process.motor.standard.high-performance");
        Assert.Contains(highPerformanceMotor.Elements!, element =>
            element.Metadata?.ContainsKey("dynamoStateColorProfile") == true);

        var dimensionalDynamos = definitions.Where(definition =>
            definition.Properties!["visualStyle"] == "dimensional-front").ToArray();
        Assert.Equal(24, dimensionalDynamos.Length);
        Assert.All(dimensionalDynamos, definition =>
            Assert.Contains(definition.Elements!, element =>
                element.Properties is not null &&
                element.Properties.TryGetValue("shadowEnabled", out var shadowEnabled) &&
                shadowEnabled.ValueKind == System.Text.Json.JsonValueKind.True));

        Assert.All(definitions.Where(definition =>
            definition.Properties!["visualStyle"] != "dimensional-front"), definition =>
            Assert.DoesNotContain(definition.Elements!, element =>
                element.Properties is not null &&
                element.Properties.TryGetValue("shadowEnabled", out var shadowEnabled) &&
                shadowEnabled.ValueKind == System.Text.Json.JsonValueKind.True));
    }

    [Fact]
    public void EveryBuiltinDynamo_PassesParameterizedStateSourceValidation()
    {
        var definitions = BuiltinDynamoLibrary.Create();

        Assert.Equal(72, definitions.Count);
        foreach (var definition in definitions)
        {
            var issues = VisualCompositionEngineeringValidation.ValidateDynamo(definition);
            Assert.DoesNotContain(issues, issue =>
                issue.Code is "VISUAL_VALUE_SOURCE_REFERENCE_REQUIRED" or "DYNAMO_STATE_COLOR_PARAMETER_NOT_FOUND");
        }
    }

    [Fact]
    public void EveryBuiltinFamily_HasThreeStylesAndFamilySpecificFinishDetails()
    {
        var definitions = BuiltinDynamoLibrary.Create();
        var families = definitions.GroupBy(definition => definition.Metadata!["familyKey"]).ToArray();

        Assert.Equal(24, families.Length);
        Assert.All(families, family =>
        {
            Assert.Equal(3, family.Count());
            Assert.Equal(3, family.Select(definition => definition.Properties!["visualStyle"]).Distinct().Count());
            Assert.All(family, definition =>
            {
                var hasFamilyFinish = definition.Elements!.Any(element =>
                    element.Key.StartsWith("detail-", StringComparison.Ordinal));
                if (family.Key == "process.blower.centrifugal")
                    hasFamilyFinish |= definition.Elements!.Any(element => element.Key.StartsWith("impeller-blade-", StringComparison.Ordinal));
                if (definition.Properties!["visualStyle"] == "high-performance")
                    Assert.False(definition.Elements!.Any(element => element.Key.StartsWith("detail-", StringComparison.Ordinal)),
                        $"High-performance family '{family.Key}' should stay visually sparse.");
                else
                    Assert.True(hasFamilyFinish, $"Dynamo family '{family.Key}' is missing family-specific artwork details.");
            });
        });

        foreach (var transformer in definitions.Where(definition => definition.Metadata!["familyKey"] == "electrical.transformer.power"))
        {
            var elements = transformer.Elements!.ToDictionary(element => element.Key, StringComparer.Ordinal);
            var tank = elements["tank"].Properties!;
            var leftRadiators = Enumerable.Range(1, 5).Select(index => elements[$"radiator-{index}"].Properties!["x"].GetDouble()).ToArray();
            var rightRadiators = Enumerable.Range(1, 5).Select(index => elements[$"radiator-r-{index}"].Properties!["x"].GetDouble()).ToArray();
            Assert.Equal(75, tank["x"].GetDouble() + tank["width"].GetDouble() / 2d);
            // Artwork normalization scales the original 4-unit radiator width proportionally.
            var radiatorWidth = elements["radiator-1"].Properties!["width"].GetDouble();
            Assert.InRange(Math.Abs((150 - leftRadiators[0] - radiatorWidth) - rightRadiators[^1]), 0, 0.25);
        }
    }

    [Fact]
    public void BuiltinArtwork_StaysInsideItsCanvasAndValveAssembliesShareOneVerticalAxis()
    {
        var definitions = BuiltinDynamoLibrary.Create();
        foreach (var definition in definitions)
        {
            var canvasWidth = double.Parse(definition.Properties!["defaultWidth"], System.Globalization.CultureInfo.InvariantCulture);
            var canvasHeight = double.Parse(definition.Properties["defaultHeight"], System.Globalization.CultureInfo.InvariantCulture);
            foreach (var element in definition.Elements!)
            {
                if (element.Properties is null ||
                    !element.Properties.TryGetValue("x", out var x) || !x.TryGetDouble(out var left) ||
                    !element.Properties.TryGetValue("y", out var y) || !y.TryGetDouble(out var top) ||
                    !element.Properties.TryGetValue("width", out var width) || !width.TryGetDouble(out var elementWidth) ||
                    !element.Properties.TryGetValue("height", out var height) || !height.TryGetDouble(out var elementHeight))
                    continue;

                Assert.True(left >= -1 && top >= -1 && left + elementWidth <= canvasWidth + 1 && top + elementHeight <= canvasHeight + 1,
                    $"Element '{element.Key}' in '{definition.Key}' exceeds its {canvasWidth}×{canvasHeight} canvas.");
            }
        }

        var valves = definitions.Where(definition => definition.Metadata!["familyKey"] is
            "process.valve.onoff" or "process.valve.control" or "process.valve.butterfly" or "process.valve.ball" or "process.valve.gate");
        foreach (var valve in valves)
        {
            var elements = valve.Elements!.ToDictionary(element => element.Key, StringComparer.Ordinal);
            var axisElements = valve.Metadata!["familyKey"] switch
            {
                "process.valve.gate" => new[] { "stem", "handwheel", "handwheel-hub" },
                "process.valve.ball" => new[] { "stem", "ball", "bore" },
                "process.valve.butterfly" => new[] { "shaft", "disc", "disc-edge" },
                _ => new[] { "stem", "actuator" }
            };
            var centers = axisElements.Select(key =>
            {
                Assert.True(elements.TryGetValue(key, out var element), $"Missing '{key}' in '{valve.Key}'.");
                return element!.Properties!["x"].GetDouble() + element.Properties["width"].GetDouble() / 2d;
            }).ToArray();

            Assert.InRange(centers.Max() - centers.Min(), 0, 1.5);
        }
    }

    [Fact]
    public void BuiltinArtwork_RotatedPartsAndOutlinesStayInsideTheirLogicalCanvas()
    {
        var problems = new List<string>();
        foreach (var definition in BuiltinDynamoLibrary.Create())
        {
            var canvasWidth = double.Parse(definition.Properties!["defaultWidth"], System.Globalization.CultureInfo.InvariantCulture);
            var canvasHeight = double.Parse(definition.Properties["defaultHeight"], System.Globalization.CultureInfo.InvariantCulture);
            foreach (var element in definition.Elements!)
            {
                var properties = element.Properties;
                if (properties is null || !properties.TryGetValue("x", out var x) || !x.TryGetDouble(out var left) ||
                    !properties.TryGetValue("y", out var y) || !y.TryGetDouble(out var top) ||
                    !properties.TryGetValue("width", out var width) || !width.TryGetDouble(out var elementWidth) ||
                    !properties.TryGetValue("height", out var height) || !height.TryGetDouble(out var elementHeight)) continue;
                var rotation = properties.TryGetValue("rotation", out var rotationValue) && rotationValue.TryGetDouble(out var degrees) ? degrees : 0;
                var radians = rotation * Math.PI / 180;
                var boundsWidth = Math.Abs(elementWidth * Math.Cos(radians)) + Math.Abs(elementHeight * Math.Sin(radians));
                var boundsHeight = Math.Abs(elementWidth * Math.Sin(radians)) + Math.Abs(elementHeight * Math.Cos(radians));
                var centerX = left + elementWidth / 2;
                var centerY = top + elementHeight / 2;
                var rotatedLeft = centerX - boundsWidth / 2;
                var rotatedTop = centerY - boundsHeight / 2;
                if (rotatedLeft < -2 || rotatedTop < -2 || rotatedLeft + boundsWidth > canvasWidth + 2 || rotatedTop + boundsHeight > canvasHeight + 2)
                    problems.Add($"{definition.Key}/{element.Key}: rotated bounds {rotatedLeft:0.##},{rotatedTop:0.##} {boundsWidth:0.##}×{boundsHeight:0.##} outside {canvasWidth}×{canvasHeight}");
            }
        }

        Assert.Empty(problems);
    }
}
