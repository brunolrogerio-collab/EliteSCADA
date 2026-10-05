using Scada.Api.Runtime;
using Scada.Engineering.Contracts;
using Scada.Engineering.Validation;
using Scada.Engineering.Views;

namespace Scada.Drivers.Tests;

public sealed class BuiltinDynamoLibraryTests
{
    [Fact]
    public void AllReplacementDefinitionsComposeWithoutOptionalCommandTargets()
    {
        foreach (var definition in BuiltinDynamoCatalogV1.Create())
        {
            var instance = new VisualElementEngineeringDto("static-preview", "dynamo",
                DynamoKey: definition.Key, Id: Guid.NewGuid(), DynamoParameters: [
                    new("animationEnabled", DynamoParameterKind.Boolean, System.Text.Json.JsonSerializer.SerializeToElement(false)),
                    new("fixedState", DynamoParameterKind.Number, System.Text.Json.JsonSerializer.SerializeToElement(1))
                ]);
            var composition = DynamoRuntimeComposer.Compose(instance, definition);
            Assert.NotEmpty(composition.Elements);
            Assert.Empty(composition.Elements.SelectMany(element => element.Actions ?? []));
        }
    }

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
                if (definition.Metadata!["familyKey"] == "indicator.lamp")
                {
                    Assert.Equal("bezelColor", artwork.Metadata!["dynamoOutlineColorParameter"]);
                    Assert.Equal("bezel3d", artwork.Metadata["dynamo3dEffectParameter"]);
                    var svg = System.Text.Encoding.UTF8.GetString(payload.Content);
                    Assert.Contains("data-elitescada-slot=\"bezel\"", svg, StringComparison.Ordinal);
                    Assert.Contains("data-elitescada-slot=\"state\"", svg, StringComparison.Ordinal);
                }
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
        Assert.Contains(contact.Parameters!, parameter => parameter.Key == "state" && parameter.Kind == DynamoParameterKind.ValueSource &&
            parameter.ValueSourceType == VisualExpressionValueType.Number);
        Assert.Contains(contact.Parameters!, parameter => parameter.Key == "openColor");
        Assert.Contains(contact.Parameters!, parameter => parameter.Key == "closedColor");
        Assert.Contains(contact.Parameters!, parameter => parameter.Key == "invertState" && parameter.Kind == DynamoParameterKind.Boolean);

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

        var inverted = DynamoRuntimeComposer.Compose(instance with
        {
            DynamoParameters =
            [
                new("state", DynamoParameterKind.TagReference, TagReference: new(tagId)),
                new("invertState", DynamoParameterKind.Boolean, System.Text.Json.JsonSerializer.SerializeToElement(true))
            ]
        }, contact);
        Assert.All(inverted.Elements.Where(element => element.Key.EndsWith("-moving", StringComparison.Ordinal)), blade =>
        {
            var stateMap = Assert.Single(blade.PropertyMaps!, map => map.PropertyKey == "fillColor");
            Assert.Equal(VisualValueSourceKind.Expression, stateMap.Source.Kind);
            Assert.Equal("1 - source", stateMap.Source.Expression!.Text);
            Assert.Equal(tagId, Assert.Single(stateMap.Source.Expression.Dependencies!).TagReference.TagId);
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
    public void ReplacementThreePoleContact_CanBindEachBladeToAnIndependentBooleanSignal()
    {
        var contact = BuiltinDynamoCatalogV1.Create().Single(definition => definition.Key == "electrical.contact-tri-horizontal");
        Assert.Contains(contact.Parameters!, parameter => parameter.Key == "useIndependentPoleSignals" && parameter.Kind == DynamoParameterKind.Boolean);
        Assert.Contains(contact.Parameters!, parameter => parameter.Key == "pole1ClosedSignal" &&
            parameter.Kind == DynamoParameterKind.ValueSource && parameter.ValueSourceType == VisualExpressionValueType.Boolean);

        var signalIds = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToArray();
        var parameters = new List<DynamoParameterValueEngineeringDto>
        {
            new("state", DynamoParameterKind.ValueSource, ValueSource: new VisualValueSourceEngineeringDto(
                VisualValueSourceKind.Tag, VisualExpressionValueType.Number, TagReference: new(Guid.NewGuid()))),
            new("useIndependentPoleSignals", DynamoParameterKind.Boolean, System.Text.Json.JsonSerializer.SerializeToElement(true)),
            new("invertState", DynamoParameterKind.Boolean, System.Text.Json.JsonSerializer.SerializeToElement(true))
        };
        for (var pole = 0; pole < signalIds.Length; pole++)
            parameters.Add(new DynamoParameterValueEngineeringDto(
                $"pole{pole + 1}ClosedSignal",
                DynamoParameterKind.ValueSource,
                ValueSource: new VisualValueSourceEngineeringDto(
                    VisualValueSourceKind.Tag,
                    VisualExpressionValueType.Boolean,
                    TagReference: new(signalIds[pole]))));

        var composed = DynamoRuntimeComposer.Compose(new VisualElementEngineeringDto(
            "contacts", "dynamo", DynamoKey: contact.Key, Id: Guid.NewGuid(), DynamoParameters: parameters), contact);
        var blades = composed.Elements.Where(element => element.Key.EndsWith("-moving", StringComparison.Ordinal)).ToArray();

        Assert.Equal(3, blades.Length);
        for (var pole = 0; pole < blades.Length; pole++)
        {
            var colorSource = Assert.Single(blades[pole].PropertyMaps!, map => map.PropertyKey == "fillColor").Source;
            var rotationSource = Assert.Single(blades[pole].PropertyMaps!, map => map.PropertyKey == "rotation").Source;
            Assert.Equal(VisualValueSourceKind.Expression, colorSource.Kind);
            Assert.Equal("number(not source)", colorSource.Expression!.Text);
            Assert.Equal(signalIds[pole], Assert.Single(colorSource.Expression.Dependencies!).TagReference.TagId);
            Assert.Equal(colorSource.Kind, rotationSource.Kind);
            Assert.Equal(colorSource.Expression.Text, rotationSource.Expression!.Text);
            Assert.Equal(signalIds[pole], Assert.Single(rotationSource.Expression.Dependencies!).TagReference.TagId);
        }
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

        var booleanTagId = Guid.NewGuid();
        var booleanInstance = instance with
        {
            DynamoParameters =
            [
                new("state", DynamoParameterKind.ValueSource, ValueSource: new VisualValueSourceEngineeringDto(
                    VisualValueSourceKind.Tag, VisualExpressionValueType.Boolean,
                    TagReference: new Scada.Core.Tags.TagValueReference(booleanTagId))),
                new("invertBoolean", DynamoParameterKind.Boolean, System.Text.Json.JsonSerializer.SerializeToElement(true))
            ]
        };
        var booleanProjection = DynamoRuntimeComposer.Compose(booleanInstance, lamp);
        var booleanStateMap = Assert.Single(booleanProjection.Elements.SelectMany(element => element.PropertyMaps ?? []));
        Assert.Equal(VisualExpressionValueType.Number, booleanStateMap.Source.ValueType);
        Assert.Equal("number(not source)", booleanStateMap.Source.Expression!.Text);
        Assert.Equal(booleanTagId, Assert.Single(booleanStateMap.Source.Expression.Dependencies!).TagReference.TagId);
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
    public void ReplacementSignalLamp_CanDisableAnyAnimatedStateAndUsesOffColorForThatStage()
    {
        var lamp = BuiltinDynamoCatalogV1.Create().Single(definition => definition.Key == "indicator.lamp.round");
        var tagId = Guid.NewGuid();
        var instance = new VisualElementEngineeringDto("lamp-subset", "dynamo", DynamoKey: lamp.Key,
            Id: Guid.NewGuid(), DynamoParameters:
            [
                new("state", DynamoParameterKind.TagReference, TagReference: new(tagId)),
                new("offColor", DynamoParameterKind.String, System.Text.Json.JsonSerializer.SerializeToElement("#333333")),
                new("enableFault", DynamoParameterKind.Boolean, System.Text.Json.JsonSerializer.SerializeToElement(false))
            ]);

        var animated = DynamoRuntimeComposer.Compose(instance, lamp);
        var animatedMap = Assert.Single(animated.Elements.SelectMany(element => element.PropertyMaps ?? []));

        Assert.Equal("#333333", animatedMap.Rules.ElementAt(2).Value.GetString());
        Assert.Equal("#16A34A", animatedMap.Rules.ElementAt(1).Value.GetString());
        Assert.Contains(lamp.Parameters!, parameter => parameter.Key == "enableCommunicationBad" &&
            parameter.Kind == DynamoParameterKind.Boolean && parameter.DefaultValue?.GetBoolean() == true);

        var fixedInstance = instance with
        {
            DynamoParameters =
            [
                new("animationEnabled", DynamoParameterKind.Boolean, System.Text.Json.JsonSerializer.SerializeToElement(false)),
                new("fixedState", DynamoParameterKind.Number, System.Text.Json.JsonSerializer.SerializeToElement(2)),
                new("offColor", DynamoParameterKind.String, System.Text.Json.JsonSerializer.SerializeToElement("#333333")),
                new("enableFault", DynamoParameterKind.Boolean, System.Text.Json.JsonSerializer.SerializeToElement(false))
            ]
        };
        var fixedArtwork = DynamoRuntimeComposer.Compose(fixedInstance, lamp).Elements.Single(element => element.Type == "core.svgSymbol");
        Assert.Equal("#333333", fixedArtwork.Properties!["svgPaintOverrides"].GetProperty("slots").GetProperty("state").GetProperty("fill").GetString());
    }

    [Fact]
    public void ReplacementCatalogV1_EquipmentStatesAreNumericTagMapsWithoutLegacyCatalog()
    {
        var replacement = BuiltinDynamoCatalogV1.Create();
        Assert.Equal(26, replacement.Count);
        Assert.All(replacement, definition => Assert.Equal("active", definition.Metadata!["catalogStatus"]));
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
    public void ReplacementEquipment_AcceptsTypedNumericTagOrExpressionStateSources()
    {
        var definitions = BuiltinDynamoCatalogV1.Create()
            .Where(definition => definition.Metadata!["familyKey"] is "equipment.motor" or "equipment.valve" or "equipment.electrical")
            .ToArray();
        Assert.Equal(18, definitions.Length);
        Assert.All(definitions, definition =>
        {
            var state = Assert.Single(definition.Parameters!, parameter => parameter.Key == "state");
            Assert.Equal(DynamoParameterKind.ValueSource, state.Kind);
            Assert.Equal(VisualExpressionValueType.Number, state.ValueSourceType);
        });

        var motor = definitions.Single(definition => definition.Key == "motor.tefc");
        var tagId = Guid.NewGuid();
        var expressionSource = new VisualValueSourceEngineeringDto(
            VisualValueSourceKind.Expression,
            VisualExpressionValueType.Number,
            Expression: new VisualExpressionEngineeringDto(
                "motorState",
                VisualExpressionValueType.Number,
                [new VisualExpressionDependencyEngineeringDto(
                    "motorState", VisualExpressionDependencyKind.Tag, VisualExpressionValueType.Number, new(tagId))]));
        var expressionInstance = new VisualElementEngineeringDto(
            "motor-expression-1", "dynamo", DynamoKey: motor.Key, Id: Guid.NewGuid(),
            DynamoParameters:
            [
                new("state", DynamoParameterKind.ValueSource, ValueSource: expressionSource),
                new("command", DynamoParameterKind.Command, CommandId: Guid.NewGuid())
            ]);

        var projected = DynamoRuntimeComposer.Compose(expressionInstance, motor);
        var stateMap = Assert.Single(projected.Elements.SelectMany(element => element.PropertyMaps ?? []),
            map => map.PropertyKey == "svg.slot.state.fill");
        Assert.Equal(VisualValueSourceKind.Expression, stateMap.Source.Kind);
        Assert.Equal("motorState", stateMap.Source.Expression!.Text);
        var labelConditions = projected.Elements
            .Where(element => element.Metadata?.ContainsKey("dynamoStateLabelIndex") == true)
            .SelectMany(element => element.BooleanConditions ?? []).ToArray();
        Assert.Equal(5, labelConditions.Length);
        Assert.All(labelConditions, condition =>
        {
            Assert.Equal(VisualValueSourceKind.Expression, condition.Source.Kind);
            Assert.Equal("motorState", condition.Source.Expression!.Text);
        });
    }

    [Theory]
    [InlineData("motor.tefc", "runningSignal")]
    [InlineData("valve.gate", "openSignal")]
    public void ReplacementEquipment_ComposesIndependentBooleanSignalsWithDeterministicPriority(
        string definitionKey,
        string activeSignalKey)
    {
        var definition = BuiltinDynamoCatalogV1.Create().Single(item => item.Key == definitionKey);
        var signalIds = Enumerable.Range(0, 16).Select(_ => Guid.NewGuid()).ToArray();
        var profile = new[] { activeSignalKey, "faultSignal", "communicationBadSignal", "inhibitedSignal" };
        var parameters = new List<DynamoParameterValueEngineeringDto>
        {
            new("useDiscreteSignals", DynamoParameterKind.Boolean,
                System.Text.Json.JsonSerializer.SerializeToElement(true)),
            new("command", DynamoParameterKind.Command, CommandId: Guid.NewGuid())
        };
        for (var index = 0; index < profile.Length; index++)
        {
            var source = index == 1
                ? new VisualValueSourceEngineeringDto(
                    VisualValueSourceKind.Expression,
                    VisualExpressionValueType.Boolean,
                    Expression: new VisualExpressionEngineeringDto(
                        "trip or overload",
                        VisualExpressionValueType.Boolean,
                        [
                            new("trip", VisualExpressionDependencyKind.Tag, VisualExpressionValueType.Boolean, new(signalIds[index])),
                            new("overload", VisualExpressionDependencyKind.ClientMemory, VisualExpressionValueType.Boolean, new(signalIds[index + 10]))
                        ]))
                : new VisualValueSourceEngineeringDto(
                    VisualValueSourceKind.Tag,
                    VisualExpressionValueType.Boolean,
                    TagReference: new(signalIds[index]));
            parameters.Add(new(profile[index], DynamoParameterKind.ValueSource, ValueSource: source));
        }

        var instance = new VisualElementEngineeringDto("multi-state-equipment", "dynamo", DynamoKey: definition.Key,
            Id: Guid.NewGuid(), DynamoParameters: parameters);
        var projected = DynamoRuntimeComposer.Compose(instance, definition);
        var stateMap = Assert.Single(projected.Elements.SelectMany(element => element.PropertyMaps ?? []),
            map => map.PropertyKey == "svg.slot.state.fill");

        Assert.Equal(VisualExpressionValueType.Number, stateMap.Source.ValueType);
        Assert.Equal(VisualValueSourceKind.Expression, stateMap.Source.Kind);
        var stateExpression = stateMap.Source.Expression!;
        Assert.Contains("number(dynamo_faultSignal_dep0 or dynamo_faultSignal_dep1) * 2", stateExpression.Text);
        Assert.Contains("number((dynamo_communicationBadSignal and not (dynamo_faultSignal_dep0 or dynamo_faultSignal_dep1))) * 3", stateExpression.Text);
        Assert.Contains("number((dynamo_inhibitedSignal and not (dynamo_faultSignal_dep0 or dynamo_faultSignal_dep1 or dynamo_communicationBadSignal))) * 4", stateExpression.Text);
        Assert.Equal(5, stateExpression.Dependencies!.Count);
        Assert.Contains(stateExpression.Dependencies, dependency => dependency.Kind == VisualExpressionDependencyKind.ClientMemory);

        var labelConditions = projected.Elements
            .Where(element => element.Metadata?.ContainsKey("dynamoStateLabelIndex") == true)
            .SelectMany(element => element.BooleanConditions ?? []).ToArray();
        Assert.Equal(5, labelConditions.Length);
        Assert.All(labelConditions, condition =>
        {
            Assert.Equal(VisualExpressionValueType.Number, condition.Source.ValueType);
            Assert.Equal(stateExpression.Text, condition.Source.Expression!.Text);
        });
    }

    [Fact]
    public void ReplacementLamps_ExposeStableBezelColorAndOptionalThreeDimensionalDepth()
    {
        var lamps = BuiltinDynamoCatalogV1.Create()
            .Where(definition => definition.Metadata!["familyKey"] == "indicator.lamp")
            .ToArray();
        Assert.Equal(4, lamps.Length);
        Assert.All(lamps, lamp =>
        {
            Assert.Contains(lamp.Parameters!, parameter => parameter.Key == "bezelColor" && parameter.Kind == DynamoParameterKind.String);
            Assert.Contains(lamp.Parameters!, parameter => parameter.Key == "bezel3d" && parameter.Kind == DynamoParameterKind.Boolean);
        });

        var lamp = lamps.Single(definition => definition.Key == "indicator.lamp.round");
        var instance = new VisualElementEngineeringDto(
            "lamp-1", "dynamo", DynamoKey: lamp.Key, Id: Guid.NewGuid(),
            DynamoParameters:
            [
                new("bezelColor", DynamoParameterKind.String, System.Text.Json.JsonSerializer.SerializeToElement("#26485A")),
                new("bezel3d", DynamoParameterKind.Boolean, System.Text.Json.JsonSerializer.SerializeToElement(true))
            ]);

        var projected = DynamoRuntimeComposer.Compose(instance, lamp);
        var outlinedElements = projected.Elements.Where(element => element.Metadata?.ContainsKey("dynamoOutlineColorParameter") == true).ToArray();
        var artwork = Assert.Single(outlinedElements);
        Assert.True(artwork.Properties!["shadowEnabled"].GetBoolean());
        Assert.Equal(2, artwork.Properties["shadowBlur"].GetDouble());
        var slots = artwork.Properties["svgPaintOverrides"].GetProperty("slots");
        Assert.Equal("#26485A", slots.GetProperty("bezel").GetProperty("stroke").GetString());
        Assert.Equal("#26485A", slots.GetProperty("state").GetProperty("stroke").GetString());
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

    [Theory]
    [InlineData("command", VisualNavigationActionKind.ExecuteCommand)]
    [InlineData("set-analog", VisualNavigationActionKind.SetTagValue)]
    [InlineData("set-bool", VisualNavigationActionKind.SetTagValue)]
    [InlineData("toggle-bool", VisualNavigationActionKind.ToggleTagBoolean)]
    public void ReplacementButton_ActionModeCanBeSelectedPerInstance(
        string mode,
        VisualNavigationActionKind expectedKind)
    {
        var definition = BuiltinDynamoCatalogV1.Create().Single(item => item.Key == "operator.button.raised");
        var commandId = Guid.NewGuid();
        var targetTagId = Guid.NewGuid();
        var instance = new VisualElementEngineeringDto("configured-button", "dynamo", DynamoKey: definition.Key,
            Id: Guid.NewGuid(), DynamoParameters:
            [
                new("actionMode", DynamoParameterKind.String, System.Text.Json.JsonSerializer.SerializeToElement(mode)),
                new("command", DynamoParameterKind.Command, CommandId: commandId),
                new("targetTag", DynamoParameterKind.TagReference, TagReference: new(targetTagId)),
                new("analogValue", DynamoParameterKind.Number, System.Text.Json.JsonSerializer.SerializeToElement(37.5)),
                new("booleanValue", DynamoParameterKind.Boolean, System.Text.Json.JsonSerializer.SerializeToElement(true))
            ]);

        var action = Assert.Single(DynamoRuntimeComposer.Compose(instance, definition).Elements
            .SelectMany(element => element.Actions ?? []));

        Assert.Equal(expectedKind, action.Kind);
        if (mode == "command")
            Assert.Equal(commandId, action.CommandId);
        else
            Assert.Equal(targetTagId.ToString("D"), action.TargetKey);
        if (mode == "set-analog")
            Assert.Equal(37.5, action.Parameters!["value"].GetDouble());
        if (mode == "set-bool")
            Assert.True(action.Parameters!["value"].GetBoolean());
    }

    [Fact]
    public void ReplacementButton_PressedFeedbackAcceptsBooleanExpressionAndOptionalInversion()
    {
        var button = BuiltinDynamoCatalogV1.Create().Single(definition => definition.Key == "operator.button.illuminated");
        var stateTagId = Guid.NewGuid();
        var state = new VisualValueSourceEngineeringDto(
            VisualValueSourceKind.Expression,
            VisualExpressionValueType.Boolean,
            Expression: new VisualExpressionEngineeringDto(
                "isPressed",
                VisualExpressionValueType.Boolean,
                [new VisualExpressionDependencyEngineeringDto(
                    "isPressed", VisualExpressionDependencyKind.Tag, VisualExpressionValueType.Boolean, new(stateTagId))]));
        var instance = new VisualElementEngineeringDto(
            "button-feedback", "dynamo", DynamoKey: button.Key, Id: Guid.NewGuid(),
            DynamoParameters:
            [
                new("state", DynamoParameterKind.ValueSource, ValueSource: state),
                new("invertBoolean", DynamoParameterKind.Boolean, System.Text.Json.JsonSerializer.SerializeToElement(true)),
                new("targetTag", DynamoParameterKind.TagReference, TagReference: new(Guid.NewGuid())),
                new("command", DynamoParameterKind.Command, CommandId: Guid.NewGuid())
            ]);

        var projected = DynamoRuntimeComposer.Compose(instance, button);
        var feedback = Assert.Single(projected.Elements.SelectMany(element => element.PropertyMaps ?? []),
            map => map.PropertyKey == "svg.slot.state.fill");
        Assert.Equal(VisualExpressionValueType.Number, feedback.Source.ValueType);
        Assert.Equal("number(not (isPressed))", feedback.Source.Expression!.Text);
        Assert.Equal(stateTagId, Assert.Single(feedback.Source.Expression.Dependencies!).TagReference.TagId);
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

    [Theory]
    [InlineData("motor.tefc", "enableFault", 2)]
    [InlineData("valve.gate", "enableOpen", 1)]
    public void ReplacementEquipment_CanDisableAnimatedAndFixedStateStages(
        string definitionKey,
        string disabledStageParameter,
        int disabledStageIndex)
    {
        var definition = BuiltinDynamoCatalogV1.Create().Single(item => item.Key == definitionKey);
        var tagId = Guid.NewGuid();
        var instance = new VisualElementEngineeringDto("equipment-subset", "dynamo", DynamoKey: definition.Key,
            Id: Guid.NewGuid(), DynamoParameters:
            [
                new("state", DynamoParameterKind.TagReference, TagReference: new(tagId)),
                new("offColor", DynamoParameterKind.String, System.Text.Json.JsonSerializer.SerializeToElement("#333333")),
                new(disabledStageParameter, DynamoParameterKind.Boolean, System.Text.Json.JsonSerializer.SerializeToElement(false)),
                new("command", DynamoParameterKind.Command, CommandId: Guid.NewGuid())
            ]);

        var animated = DynamoRuntimeComposer.Compose(instance, definition);
        var animatedMap = Assert.Single(animated.Elements.SelectMany(element => element.PropertyMaps ?? []),
            map => map.PropertyKey == "svg.slot.state.fill");
        Assert.Equal("#333333", animatedMap.Rules.ElementAt(disabledStageIndex).Value.GetString());
        Assert.Equal(tagId, animatedMap.Source.TagReference!.TagId);
        var disabledAnimatedLabel = Assert.Single(animated.Elements,
            element => element.Metadata?.GetValueOrDefault("dynamoStateLabelIndex") == disabledStageIndex.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Null(disabledAnimatedLabel.BooleanConditions);
        Assert.False(disabledAnimatedLabel.Properties!["visible"].GetBoolean());

        var fixedInstance = instance with
        {
            DynamoParameters =
            [
                new("animationEnabled", DynamoParameterKind.Boolean, System.Text.Json.JsonSerializer.SerializeToElement(false)),
                new("fixedState", DynamoParameterKind.Number, System.Text.Json.JsonSerializer.SerializeToElement(disabledStageIndex)),
                new("offColor", DynamoParameterKind.String, System.Text.Json.JsonSerializer.SerializeToElement("#333333")),
                new(disabledStageParameter, DynamoParameterKind.Boolean, System.Text.Json.JsonSerializer.SerializeToElement(false)),
                new("command", DynamoParameterKind.Command, CommandId: Guid.NewGuid())
            ]
        };
        var fixedArtwork = DynamoRuntimeComposer.Compose(fixedInstance, definition)
            .Elements.Single(element => element.Key == "artwork");
        Assert.Equal("#333333", fixedArtwork.Properties!["svgPaintOverrides"].GetProperty("slots")
            .GetProperty("state").GetProperty("fill").GetString());
        var disabledFixedLabel = Assert.Single(DynamoRuntimeComposer.Compose(fixedInstance, definition).Elements,
            element => element.Metadata?.GetValueOrDefault("dynamoStateLabelIndex") == disabledStageIndex.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Null(disabledFixedLabel.BooleanConditions);
        Assert.False(disabledFixedLabel.Properties!["visible"].GetBoolean());
    }

    [Fact]
    public void Workspace_SeedsTheBuiltInLibraryAndKeepsEquipmentBindingsParameterized()
    {
        using var workspace = new EngineeringWorkspace();

        var definitions = workspace.Assets.SnapshotDynamos();
        Assert.Equal(BuiltinDynamoCatalogV1.Create().Count, definitions.Count);
        Assert.DoesNotContain(definitions, definition => definition.Metadata?.GetValueOrDefault("catalogStatus") == "legacy");
        Assert.DoesNotContain(definitions, definition =>
            definition.Metadata?.GetValueOrDefault("assetOrigin") == "elipse-e3-import");
        Assert.Contains(definitions, definition => definition.Parameters?.Any(parameter =>
            parameter.Kind == DynamoParameterKind.ValueSource) == true);
    }

}
